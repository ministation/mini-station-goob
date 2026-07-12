// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._TT.StationHandleJob;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Shared._Mini.TypanWar;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Mini.TypanWar;

public sealed class TypanWarCaptureZoneSystem : SharedTypanWarCaptureZoneSystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TypanWarCaptureZoneSpawnSystem _spawn = default!;
    [Dependency] private readonly TypanWarCaptureZoneProtectionSystem _zoneProtection = default!;
    [Dependency] private readonly TypanStationWarRuleSystem _warRule = default!;
    [Dependency] private readonly TTStationHandleJobSystem _typanJobs = default!;

    private readonly HashSet<EntityUid> _lookupEnts = new();

    private sealed class ZoneRuntimeState
    {
        public float PointAccumulator;
        public float LootAccumulator;
        public TypanWarCaptureOwner? CapturingToward;
    }

    private readonly Dictionary<EntityUid, ZoneRuntimeState> _runtime = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TypanWarCaptureZoneComponent, ComponentStartup>(OnZoneStartup);
        SubscribeLocalEvent<TypanWarLayoutReadyEvent>(OnLayoutReady);
    }

    private void OnLayoutReady(TypanWarLayoutReadyEvent ev)
    {
        if (!TryComp<TypanStationWarRuleComponent>(ev.Rule, out var rule) || rule.CaptureZonesActivated)
            return;

        rule.CaptureZonesActivated = true;
        ActivateAllZones(ev.NtStation, ev.TypanStation);
    }

    private void OnZoneStartup(EntityUid uid, TypanWarCaptureZoneComponent component, ComponentStartup args)
    {
        _runtime.TryAdd(uid, new ZoneRuntimeState());

        if (component.FlagEntity is { } flag && flag.IsValid())
        {
            EnsureComp<TypanWarCaptureFlagComponent>(flag);
            var flagComp = Comp<TypanWarCaptureFlagComponent>(flag);
            flagComp.Zone = uid;
            flagComp.CaptureOwner = component.CaptureOwner;
            Dirty(flag, flagComp);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!TypanStationWarRuleSystem.IsWarActive)
            return;

        var query = EntityQueryEnumerator<TypanWarCaptureZoneComponent>();
        while (query.MoveNext(out var uid, out var zone))
        {
            if (!zone.Active)
                continue;

            UpdateZone(uid, zone, frameTime);
        }
    }

    public void InitializeSpawnedZone(
        EntityUid zoneUid,
        TypanWarCaptureOwner homeFaction,
        string label,
        string displayName,
        EntityUid flagEntity)
    {
        var zone = Comp<TypanWarCaptureZoneComponent>(zoneUid);
        zone.HomeFaction = homeFaction;
        zone.ZoneLabel = label;
        zone.ZoneDisplayName = displayName;
        zone.ZoneLocaleKey = homeFaction switch
        {
            TypanWarCaptureOwner.Nanotrasen => "nt",
            TypanWarCaptureOwner.Typan => "typan",
            _ => "trade",
        };
        zone.FlagEntity = flagEntity;
        Dirty(zoneUid, zone);

        EnsureComp<TypanWarCaptureFlagComponent>(flagEntity);
        var flagComp = Comp<TypanWarCaptureFlagComponent>(flagEntity);
        flagComp.Zone = zoneUid;
        flagComp.CaptureOwner = TypanWarCaptureOwner.Neutral;
        Dirty(flagEntity, flagComp);
    }

    public void ActivateAllZones(EntityUid ntStation, EntityUid typanStation)
    {
        if (!_spawn.TrySpawnWarZones(ntStation, typanStation, out var zones) || zones.Count == 0)
        {
            Log.Warning("Typan station war: failed to spawn capture zones.");
            return;
        }

        var lines = new List<string>();
        foreach (var spawned in zones.OrderBy(z => z.Label))
        {
            InitializeSpawnedZone(
                spawned.ZoneUid,
                spawned.HomeFaction,
                spawned.Label,
                spawned.DisplayName,
                spawned.FlagUid);

            if (!TryComp<TypanWarCaptureZoneComponent>(spawned.ZoneUid, out var zone))
                continue;

            zone.Active = true;
            zone.CaptureProgress = 0f;
            zone.CaptureOwner = TypanWarCaptureOwner.Neutral;
            Dirty(spawned.ZoneUid, zone);

            UpdateFlagVisual(spawned.ZoneUid, zone);

            lines.Add(Loc.GetString("typan-war-capture-zones-active-line",
                ("label", zone.ZoneLabel),
                ("location", zone.ZoneDisplayName)));
        }

        _zoneProtection.RefreshAllZoneProtection();

        var body = Loc.GetString("typan-war-capture-zones-active-header")
            + "\n"
            + string.Join("\n", lines);

        _chat.DispatchGlobalAnnouncement(body, Loc.GetString("typan-war-sender"), announcementSound: TypanWarSounds.HeadquartersAlert, colorOverride: TypanWarColors.Neutral);
    }

    public TypanWarCaptureZoneStatus[] GetZoneStatuses()
    {
        var list = new List<TypanWarCaptureZoneStatus>();
        var query = EntityQueryEnumerator<TypanWarCaptureZoneComponent>();
        while (query.MoveNext(out var uid, out var zone))
        {
            var worldPos = _transform.GetWorldPosition(uid);
            list.Add(new TypanWarCaptureZoneStatus(
                zone.ZoneLabel,
                zone.ZoneDisplayName,
                zone.ZoneLocaleKey,
                zone.CaptureOwner,
                zone.HomeFaction,
                zone.CaptureProgress,
                zone.Active,
                worldPos.X,
                worldPos.Y));
        }

        return list.OrderBy(z => z.ZoneLabel).ToArray();
    }

    public IEnumerable<(EntityCoordinates Coordinates, string Label, string DisplayName)> GetOwnedZoneCoordinates(
        TypanWarCaptureOwner owner)
    {
        var query = EntityQueryEnumerator<TypanWarCaptureZoneComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var zone, out var xform))
        {
            if (!zone.Active || zone.CaptureOwner != owner)
                continue;

            yield return (xform.Coordinates, zone.ZoneLabel, zone.ZoneDisplayName);
        }
    }

    private void UpdateZone(EntityUid uid, TypanWarCaptureZoneComponent zone, float frameTime)
    {
        if (!TryGetZoneTiles(uid, zone, out var gridUid, out var grid, out var centerTile))
            return;

        CountFactionPlayers(gridUid, grid, centerTile, zone.ZoneHalfExtents, out var ntCount, out var typanCount);

        var runtime = GetRuntime(uid);
        var contested = ntCount > 0 && typanCount > 0;
        TypanWarCaptureOwner? capturing = null;

        if (contested)
        {
            runtime.CapturingToward = null;
            zone.CapturingOwner = null;
            if (zone.CaptureProgress > 0f)
            {
                zone.CaptureProgress = Math.Max(0f,
                    zone.CaptureProgress - frameTime / (zone.CaptureTimeSeconds * zone.ContestDecayMultiplier));
                Dirty(uid, zone);
            }

            return;
        }

        if (ntCount > typanCount)
            capturing = TypanWarCaptureOwner.Nanotrasen;
        else if (typanCount > ntCount)
            capturing = TypanWarCaptureOwner.Typan;
        else
        {
            runtime.CapturingToward = null;
            zone.CapturingOwner = null;
            if (zone.CaptureProgress > 0f && zone.CaptureOwner != TypanWarCaptureOwner.Neutral)
            {
                zone.CaptureProgress = Math.Max(0f, zone.CaptureProgress - frameTime / zone.CaptureTimeSeconds);
                Dirty(uid, zone);
            }

            TryAwardCapturePoints(uid, zone, runtime, frameTime);
            TrySpawnLootCrate(uid, zone, runtime, frameTime);
            return;
        }

        if (capturing == zone.CaptureOwner)
        {
            zone.CaptureProgress = 0f;
            zone.CapturingOwner = null;
            runtime.CapturingToward = null;
            Dirty(uid, zone);
            TryAwardCapturePoints(uid, zone, runtime, frameTime);
            TrySpawnLootCrate(uid, zone, runtime, frameTime);
            return;
        }

        if (runtime.CapturingToward != capturing)
            zone.CaptureProgress = 0f;

        runtime.CapturingToward = capturing;
        zone.CapturingOwner = capturing;
        zone.CaptureProgress += frameTime / zone.CaptureTimeSeconds;
        Dirty(uid, zone);

        if (zone.CaptureProgress < 1f)
            return;

        CompleteCapture(uid, zone, capturing.Value);
    }

    private void CompleteCapture(
        EntityUid uid,
        TypanWarCaptureZoneComponent zone,
        TypanWarCaptureOwner newOwner)
    {
        var runtime = GetRuntime(uid);
        zone.CaptureOwner = newOwner;
        zone.CaptureProgress = 0f;
        zone.CapturingOwner = null;
        runtime.CapturingToward = null;
        runtime.PointAccumulator = 0f;
        runtime.LootAccumulator = 0f;
        Dirty(uid, zone);

        UpdateFlagVisual(uid, zone);
        AnnounceCapture(uid, zone, newOwner);
    }

    private void TryAwardCapturePoints(EntityUid uid, TypanWarCaptureZoneComponent zone, ZoneRuntimeState runtime, float frameTime)
    {
        if (zone.CaptureOwner == TypanWarCaptureOwner.Neutral)
            return;

        if (!TryGetActiveRule(out var rule))
            return;

        runtime.PointAccumulator += frameTime;
        if (runtime.PointAccumulator < rule.CapturePointIntervalSeconds)
            return;

        runtime.PointAccumulator = 0f;
        _warRule.AddCapturePoints(zone.CaptureOwner, 1f);
    }

    private void TrySpawnLootCrate(EntityUid uid, TypanWarCaptureZoneComponent zone, ZoneRuntimeState runtime, float frameTime)
    {
        if (zone.CaptureOwner == TypanWarCaptureOwner.Neutral)
            return;

        runtime.LootAccumulator += frameTime;
        if (runtime.LootAccumulator < zone.LootIntervalSeconds)
            return;

        runtime.LootAccumulator = 0f;

        if (!TryPickLootSpawnCoordinates(uid, zone, out var spawnCoords))
            return;

        var crateProto = zone.CaptureOwner switch
        {
            TypanWarCaptureOwner.Nanotrasen => zone.NtLootCrate,
            TypanWarCaptureOwner.Typan => zone.TypanLootCrate,
            _ => default(EntProtoId?),
        };

        if (crateProto == null)
            return;

        Spawn(crateProto.Value, spawnCoords);

        var location = FormattedMessage.RemoveMarkupPermissive(zone.ZoneDisplayName);
        switch (zone.CaptureOwner)
        {
            case TypanWarCaptureOwner.Nanotrasen:
                _chat.DispatchGlobalAnnouncement(
                    Loc.GetString("typan-war-capture-loot-nt", ("label", zone.ZoneLabel), ("location", location)),
                    Loc.GetString(TypanWarColors.SenderLocId(TypanWarCaptureOwner.Nanotrasen)),
                    announcementSound: TypanWarSounds.HeadquartersAlert,
                    colorOverride: TypanWarColors.ForCaptureOwner(TypanWarCaptureOwner.Nanotrasen));
                break;
            case TypanWarCaptureOwner.Typan:
                _chat.DispatchGlobalAnnouncement(
                    Loc.GetString("typan-war-capture-loot-typan", ("label", zone.ZoneLabel), ("location", location)),
                    Loc.GetString(TypanWarColors.SenderLocId(TypanWarCaptureOwner.Typan)),
                    announcementSound: TypanWarSounds.HeadquartersAlert,
                    colorOverride: TypanWarColors.ForCaptureOwner(TypanWarCaptureOwner.Typan));
                break;
        }
    }

    private bool TryPickLootSpawnCoordinates(
        EntityUid uid,
        TypanWarCaptureZoneComponent zone,
        out EntityCoordinates spawnCoords)
    {
        spawnCoords = EntityCoordinates.Invalid;

        if (!TryGetZoneTiles(uid, zone, out var gridUid, out var grid, out var centerTile))
            return false;

        var candidates = new List<Vector2i> { centerTile };
        for (var dx = -zone.ZoneHalfExtents.X; dx <= zone.ZoneHalfExtents.X; dx++)
        {
            for (var dy = -zone.ZoneHalfExtents.Y; dy <= zone.ZoneHalfExtents.Y; dy++)
            {
                var tile = centerTile + new Vector2i(dx, dy);
                if (tile != centerTile)
                    candidates.Add(tile);
            }
        }

        _random.Shuffle(candidates);

        foreach (var tile in candidates)
        {
            if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
                continue;

            spawnCoords = _map.GridTileToLocal(gridUid, grid, tile);
            return true;
        }

        return false;
    }

    private string FormatZoneName(TypanWarCaptureZoneComponent zone)
    {
        if (!string.IsNullOrEmpty(zone.ZoneLabel) && !string.IsNullOrEmpty(zone.ZoneDisplayName))
            return Loc.GetString("typan-war-capture-zone-named", ("label", zone.ZoneLabel), ("location", zone.ZoneDisplayName));

        return Loc.GetString($"typan-war-capture-zone-{zone.ZoneLocaleKey}");
    }

    private void AnnounceCapture(EntityUid uid, TypanWarCaptureZoneComponent zone, TypanWarCaptureOwner owner)
    {
        var zoneName = FormatZoneName(zone);
        var key = owner switch
        {
            TypanWarCaptureOwner.Nanotrasen => "typan-war-capture-nt",
            TypanWarCaptureOwner.Typan => "typan-war-capture-typan",
            _ => "typan-war-capture-neutral",
        };

        var message = Loc.GetString(key, ("zone", zoneName));
        _chat.DispatchGlobalAnnouncement(
            message,
            Loc.GetString(TypanWarColors.SenderLocId(owner)),
            announcementSound: TypanWarSounds.HeadquartersAlert,
            colorOverride: TypanWarColors.ForCaptureOwner(owner));
    }

    private bool TryGetActiveRule([NotNullWhen(true)] out TypanStationWarRuleComponent? rule)
    {
        rule = null;
        var query = EntityQueryEnumerator<TypanStationWarRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var ruleUid, out var comp, out var gameRule))
        {
            if (!_ticker.IsGameRuleActive(ruleUid, gameRule) || comp.Phase != TypanWarPhase.Active)
                continue;

            rule = comp;
            return true;
        }

        return false;
    }

    private void CountFactionPlayers(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i centerTile,
        Vector2i halfExtents,
        out int ntCount,
        out int typanCount)
    {
        ntCount = 0;
        typanCount = 0;

        for (var dx = -halfExtents.X; dx <= halfExtents.X; dx++)
        {
            for (var dy = -halfExtents.Y; dy <= halfExtents.Y; dy++)
            {
                var tile = centerTile + new Vector2i(dx, dy);
                _lookupEnts.Clear();
                _lookup.GetLocalEntitiesIntersecting(gridUid, tile, _lookupEnts, 0f);

                foreach (var ent in _lookupEnts)
                {
                    if (!TryComp<MindContainerComponent>(ent, out var mindContainer) || mindContainer.Mind == null)
                        continue;

                    if (!TryComp<MindComponent>(mindContainer.Mind, out var mind) || !IsMindAlive(mind))
                        continue;

                    if (_typanJobs.MindHasHandledJob(mindContainer.Mind.Value))
                        typanCount++;
                    else if (_jobs.MindTryGetJobId(mindContainer.Mind.Value, out var jobId) && jobId != null
                             && !_typanJobs.IsHandledJob(jobId.Value))
                        ntCount++;
                }
            }
        }
    }

    private bool IsMindAlive(MindComponent mind)
    {
        var entity = mind.CurrentEntity;
        if (entity == null || !entity.Value.IsValid())
            return false;

        if (HasComp<GhostComponent>(entity))
            return false;

        if (TryComp<MobStateComponent>(entity, out var mobState))
            return mobState.CurrentState != MobState.Dead;

        return true;
    }

    private bool TryGetZoneTiles(
        EntityUid uid,
        TypanWarCaptureZoneComponent zone,
        out EntityUid gridUid,
        out MapGridComponent grid,
        out Vector2i centerTile)
    {
        gridUid = EntityUid.Invalid;
        grid = default!;
        centerTile = default;

        if (!TryComp<TransformComponent>(uid, out var xform) || xform.GridUid is not { } gridEnt)
            return false;

        if (!TryComp<MapGridComponent>(gridEnt, out var gridComp))
            return false;

        gridUid = gridEnt;
        grid = gridComp;
        centerTile = _transform.GetGridOrMapTilePosition(uid, xform);
        return true;
    }

    private void UpdateFlagVisual(EntityUid uid, TypanWarCaptureZoneComponent zone)
    {
        if (zone.FlagEntity is not { } flag || !Exists(flag))
            return;

        if (!TryComp<TypanWarCaptureFlagComponent>(flag, out var flagComp))
            return;

        flagComp.CaptureOwner = zone.CaptureOwner;
        Dirty(flag, flagComp);
    }

    private ZoneRuntimeState GetRuntime(EntityUid uid)
    {
        if (!_runtime.TryGetValue(uid, out var state))
        {
            state = new ZoneRuntimeState();
            _runtime[uid] = state;
        }

        return state;
    }
}
