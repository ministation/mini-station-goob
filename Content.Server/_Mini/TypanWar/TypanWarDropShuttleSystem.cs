using Content.Shared.Destructible;
// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Server.Pinpointer;
using Content.Server.Shuttles;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Mini.TypanWar;
using Content.Shared.Construction;
using Content.Shared.Localizations;
using Content.Shared.Station.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Mini.TypanWar;

/// <summary>
/// Spawns war drop shuttles, docks them to free ports, and replaces them when the pilot console is lost.
/// </summary>
public sealed class TypanWarDropShuttleSystem : EntitySystem
{
    private const float DockRetryIntervalSeconds = 60f;

    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DockingSystem _dock = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TypanWarLayoutReadyEvent>(OnLayoutReady);

        SubscribeLocalEvent<TypanWarDropShuttleConsoleComponent, EntityTerminatingEvent>(OnConsoleTerminating);
        SubscribeLocalEvent<TypanWarDropShuttleConsoleComponent, MachineDeconstructedEvent>(OnConsoleDeconstructed);
        SubscribeLocalEvent<TypanWarDropShuttleConsoleComponent, DamageThresholdReached>(OnConsoleDamaged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<TypanStationWarRuleComponent>();
        while (query.MoveNext(out var ruleUid, out var rule))
        {
            if (rule.Phase != TypanWarPhase.Active)
                continue;

            TryProcessRespawn(ruleUid, rule, TypanWarSide.Nanotrasen);
            TryProcessRespawn(ruleUid, rule, TypanWarSide.Typan);
        }
    }

    /// <summary>
    /// Clears tracked shuttles and pending respawns when the war ends.
    /// </summary>
    public void ClearDropShuttleState(TypanStationWarRuleComponent rule)
    {
        rule.NtDropShuttle = null;
        rule.TypanDropShuttle = null;
        rule.NtDropShuttleRespawnAt = null;
        rule.TypanDropShuttleRespawnAt = null;
    }

    private void OnLayoutReady(TypanWarLayoutReadyEvent ev)
    {
        if (!TryComp<TypanStationWarRuleComponent>(ev.Rule, out var rule))
            return;

        if (rule.DropShuttlePath == default && rule.NtDropShuttlePath == default)
            return;

        if (rule.DropShuttlePath != default)
        {
            TrySpawnDropShuttle(
                ev.Rule,
                rule,
                ev.TypanStation,
                rule.DropShuttlePath,
                TypanWarSide.Typan,
                "typan-war-drop-shuttle-docked-typan");
        }

        if (rule.NtDropShuttlePath != default)
        {
            TrySpawnDropShuttle(
                ev.Rule,
                rule,
                ev.NtStation,
                rule.NtDropShuttlePath,
                TypanWarSide.Nanotrasen,
                "typan-war-drop-shuttle-docked-nt");
        }
    }

    private void OnConsoleTerminating(Entity<TypanWarDropShuttleConsoleComponent> ent, ref EntityTerminatingEvent args)
    {
        OnConsoleLost(ent);
    }

    private void OnConsoleDeconstructed(Entity<TypanWarDropShuttleConsoleComponent> ent, ref MachineDeconstructedEvent args)
    {
        OnConsoleLost(ent);
    }

    private void OnConsoleDamaged(Entity<TypanWarDropShuttleConsoleComponent> ent, ref DamageThresholdReached args)
    {
        if (!args.Threshold.Behaviors.Any(b =>
                b is DoActsBehavior behavior &&
                (behavior.HasAct(ThresholdActs.Breakage) || behavior.HasAct(ThresholdActs.Destruction))))
        {
            return;
        }

        OnConsoleLost(ent);
    }

    private void OnConsoleLost(Entity<TypanWarDropShuttleConsoleComponent> ent)
    {
        if (!TryComp<TypanStationWarRuleComponent>(ent.Comp.Rule, out var rule))
            return;

        if (rule.Phase != TypanWarPhase.Active)
            return;

        if (!IsTrackedConsole(rule, ent.Comp.Side, ent.Owner))
            return;

        ClearTrackedShuttle(rule, ent.Comp.Side);
        ScheduleRespawn(rule, ent.Comp.Side);
        AnnounceShuttleLost(ent.Comp.Side);
    }

    private void TryProcessRespawn(EntityUid ruleUid, TypanStationWarRuleComponent rule, TypanWarSide side)
    {
        if (HasWorkingDropShuttle(rule, side))
        {
            ClearRespawnSchedule(rule, side);
            return;
        }

        var respawnAt = GetRespawnAt(rule, side);
        if (respawnAt == null || _timing.CurTime < respawnAt)
            return;

        var station = side == TypanWarSide.Nanotrasen ? rule.NtStation : rule.TypanStation;
        var shuttlePath = side == TypanWarSide.Nanotrasen ? rule.NtDropShuttlePath : rule.DropShuttlePath;
        var announcementKey = side == TypanWarSide.Nanotrasen
            ? "typan-war-drop-shuttle-docked-nt"
            : "typan-war-drop-shuttle-docked-typan";

        if (station == null || shuttlePath == default)
        {
            ClearRespawnSchedule(rule, side);
            return;
        }

        if (TrySpawnDropShuttle(ruleUid, rule, station.Value, shuttlePath, side, announcementKey))
            ClearRespawnSchedule(rule, side);
        else
            SetRespawnAt(rule, side, _timing.CurTime + TimeSpan.FromSeconds(DockRetryIntervalSeconds));
    }

    private bool TrySpawnDropShuttle(
        EntityUid ruleUid,
        TypanStationWarRuleComponent rule,
        EntityUid station,
        ResPath shuttlePath,
        TypanWarSide side,
        string announcementKey)
    {
        if (!SpawnAndDockDropShuttle(station, shuttlePath, announcementKey, out var shuttleUid))
            return false;

        if (!TryFindShuttleConsole(shuttleUid, out var consoleUid))
        {
            Log.Warning($"War drop shuttle: no pilot console found on {shuttlePath}.");
            Del(shuttleUid);
            return false;
        }

        RegisterDropShuttle(ruleUid, rule, side, shuttleUid, consoleUid);
        return true;
    }

    private bool SpawnAndDockDropShuttle(
        EntityUid station,
        ResPath shuttlePath,
        string announcementKey,
        out EntityUid shuttleUid)
    {
        shuttleUid = EntityUid.Invalid;

        if (!TryComp<StationDataComponent>(station, out var stationData) || stationData.Grids.Count == 0)
        {
            Log.Error($"War drop shuttle: station {station} has no grids.");
            return false;
        }

        // Load onto an initialized temp map so MapLoader runs RecursiveMapInit on the grid.
        // Drop shuttle YAMLs must stay pre-init (no mapInit: true) so door boards / turret HTN fill.
        _map.CreateMap(out var mapId);

        if (!_loader.TryLoadGrid(mapId, shuttlePath, out var shuttleGrid) ||
            !TryComp<ShuttleComponent>(shuttleGrid, out _) ||
            !TryComp<TransformComponent>(shuttleGrid, out var shuttleXform))
        {
            Log.Error($"War drop shuttle: failed to load grid from {shuttlePath}.");
            _map.DeleteMap(mapId);
            return false;
        }

        shuttleUid = shuttleGrid.Value;
        // Belt-and-suspenders: ensure ContainerFill / HTN still run if a map was re-saved post-init.
        _map.RecursiveMapInit(shuttleUid);

        if (!TryDockToFreePort(shuttleUid, shuttleXform, station, stationData, out var config, out var targetGrid))
        {
            Log.Warning($"War drop shuttle: no free docking port on station {station} for {shuttlePath}.");
            Del(shuttleUid);
            _map.DeleteMap(mapId);
            shuttleUid = EntityUid.Invalid;
            return false;
        }

        _station.AddGridToStation(station, shuttleUid);
        _map.DeleteMap(mapId);

        AnnounceDocking(station, shuttleUid, shuttleXform, targetGrid, config, announcementKey);
        return true;
    }

    private void RegisterDropShuttle(
        EntityUid ruleUid,
        TypanStationWarRuleComponent rule,
        TypanWarSide side,
        EntityUid shuttleUid,
        EntityUid consoleUid)
    {
        var drop = EnsureComp<TypanWarDropShuttleComponent>(shuttleUid);
        drop.Side = side;
        drop.Rule = ruleUid;
        drop.Console = consoleUid;
        Dirty(shuttleUid, drop);

        var console = EnsureComp<TypanWarDropShuttleConsoleComponent>(consoleUid);
        console.Side = side;
        console.Rule = ruleUid;
        console.ShuttleGrid = shuttleUid;
        Dirty(consoleUid, console);

        SetTrackedShuttle(rule, side, shuttleUid);
    }

    private bool TryFindShuttleConsole(EntityUid gridUid, out EntityUid consoleUid)
    {
        consoleUid = EntityUid.Invalid;

        var query = EntityQueryEnumerator<ShuttleConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;

            consoleUid = uid;
            return true;
        }

        return false;
    }

    private bool IsTrackedConsole(TypanStationWarRuleComponent rule, TypanWarSide side, EntityUid console)
    {
        var shuttle = GetTrackedShuttle(rule, side);
        if (shuttle == null || !TryComp<TypanWarDropShuttleComponent>(shuttle, out var drop))
            return false;

        return drop.Console == console;
    }

    private bool HasWorkingDropShuttle(TypanStationWarRuleComponent rule, TypanWarSide side)
    {
        var shuttle = GetTrackedShuttle(rule, side);
        if (shuttle == null || !Exists(shuttle))
            return false;

        if (!TryComp<TypanWarDropShuttleComponent>(shuttle, out var drop))
            return false;

        return Exists(drop.Console) && HasComp<ShuttleConsoleComponent>(drop.Console);
    }

    private void ScheduleRespawn(TypanStationWarRuleComponent rule, TypanWarSide side)
    {
        if (GetRespawnAt(rule, side) != null)
            return;

        SetRespawnAt(rule, side, _timing.CurTime + TimeSpan.FromSeconds(rule.DropShuttleRespawnDelaySeconds));
    }

    private static EntityUid? GetTrackedShuttle(TypanStationWarRuleComponent rule, TypanWarSide side) =>
        side == TypanWarSide.Nanotrasen ? rule.NtDropShuttle : rule.TypanDropShuttle;

    private static void SetTrackedShuttle(TypanStationWarRuleComponent rule, TypanWarSide side, EntityUid shuttle)
    {
        if (side == TypanWarSide.Nanotrasen)
            rule.NtDropShuttle = shuttle;
        else
            rule.TypanDropShuttle = shuttle;
    }

    private static void ClearTrackedShuttle(TypanStationWarRuleComponent rule, TypanWarSide side)
    {
        if (side == TypanWarSide.Nanotrasen)
            rule.NtDropShuttle = null;
        else
            rule.TypanDropShuttle = null;
    }

    private static TimeSpan? GetRespawnAt(TypanStationWarRuleComponent rule, TypanWarSide side) =>
        side == TypanWarSide.Nanotrasen ? rule.NtDropShuttleRespawnAt : rule.TypanDropShuttleRespawnAt;

    private static void SetRespawnAt(TypanStationWarRuleComponent rule, TypanWarSide side, TimeSpan at)
    {
        if (side == TypanWarSide.Nanotrasen)
            rule.NtDropShuttleRespawnAt = at;
        else
            rule.TypanDropShuttleRespawnAt = at;
    }

    private static void ClearRespawnSchedule(TypanStationWarRuleComponent rule, TypanWarSide side)
    {
        if (side == TypanWarSide.Nanotrasen)
            rule.NtDropShuttleRespawnAt = null;
        else
            rule.TypanDropShuttleRespawnAt = null;
    }

    private bool TryDockToFreePort(
        EntityUid shuttleUid,
        TransformComponent shuttleXform,
        EntityUid station,
        StationDataComponent stationData,
        [NotNullWhen(true)] out DockingConfig? config,
        out EntityUid targetGrid)
    {
        config = null;
        targetGrid = EntityUid.Invalid;

        var grids = stationData.Grids.ToList();

        if (_station.GetLargestGrid(station) is { } largest)
        {
            grids.Remove(largest);
            grids.Insert(0, largest);
        }

        foreach (var grid in grids)
        {
            var dockConfig = _dock.GetDockingConfig(shuttleUid, grid);

            if (dockConfig == null)
                continue;

            // Clear stale DockedWith / DockJointId before FTLDock recreates welds
            // (map merges ClearJoints without undocking and cause "joint already existed").
            foreach (var (dockAUid, dockBUid, dockA, dockB) in dockConfig.Docks)
            {
                if (dockA.DockedWith != null)
                    _dock.Undock((dockAUid, dockA));
                if (dockB.DockedWith != null)
                    _dock.Undock((dockBUid, dockB));

                dockA.DockJointId = null;
                dockB.DockJointId = null;
            }

            _shuttle.FTLDock((shuttleUid, shuttleXform), dockConfig);
            config = dockConfig;
            targetGrid = grid;
            return true;
        }

        return false;
    }

    private void AnnounceShuttleLost(TypanWarSide side)
    {
        var isNt = side == TypanWarSide.Nanotrasen;
        var owner = isNt ? TypanWarCaptureOwner.Nanotrasen : TypanWarCaptureOwner.Typan;
        var key = isNt ? "typan-war-drop-shuttle-lost-nt" : "typan-war-drop-shuttle-lost-typan";

        _chat.DispatchGlobalAnnouncement(
            Loc.GetString(key),
            Loc.GetString(TypanWarColors.SenderLocId(owner)),
            announcementSound: TypanWarSounds.HeadquartersAlert,
            colorOverride: TypanWarColors.ForCaptureOwner(owner));
    }

    private void AnnounceDocking(
        EntityUid station,
        EntityUid shuttleUid,
        TransformComponent shuttleXform,
        EntityUid targetGrid,
        DockingConfig config,
        string announcementKey)
    {
        var targetXform = Transform(targetGrid);
        var angle = _dock.GetAngle(shuttleUid, shuttleXform, targetGrid, targetXform);
        var direction = ContentLocalizationManager.FormatDirection(angle.GetDir());
        var location = GetStationDockLocation(config, targetGrid);

        var isNt = announcementKey == "typan-war-drop-shuttle-docked-nt";
        var factionColor = isNt ? TypanWarColors.Nanotrasen : TypanWarColors.Typan;
        var sender = Loc.GetString(isNt ? "typan-war-sender-nt" : "typan-war-sender-typan");

        _chat.DispatchStationAnnouncement(
            station,
            Loc.GetString(announcementKey, ("direction", direction), ("location", location)),
            sender,
            announcementSound: TypanWarSounds.HeadquartersAlert,
            colorOverride: factionColor);
    }

    private string GetStationDockLocation(DockingConfig config, EntityUid stationGrid)
    {
        foreach (var (dockAUid, dockBUid, _, _) in config.Docks)
        {
            if (Transform(dockAUid).GridUid == stationGrid)
            {
                return FormattedMessage.RemoveMarkupPermissive(
                    _navMap.GetNearestBeaconString(dockAUid, onlyName: true));
            }

            if (Transform(dockBUid).GridUid == stationGrid)
            {
                return FormattedMessage.RemoveMarkupPermissive(
                    _navMap.GetNearestBeaconString(dockBUid, onlyName: true));
            }
        }

        return FormattedMessage.RemoveMarkupPermissive(
            _navMap.GetNearestBeaconString(_transform.ToMapCoordinates(config.Coordinates), onlyName: true));
    }
}
