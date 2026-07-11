using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Pinpointer;
using Content.Server.Shuttles;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Mini.TypanWar;
using Content.Shared.Localizations;
using Content.Shared.Station.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Server._Mini.TypanWar;

/// <summary>
/// Spawns war drop shuttles and docks them to free ports on each station when combat begins.
/// </summary>
public sealed class TypanWarDropShuttleSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DockingSystem _dock = default!;
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
    }

    private void OnLayoutReady(TypanWarLayoutReadyEvent ev)
    {
        if (!TryComp<TypanStationWarRuleComponent>(ev.Rule, out var rule))
            return;

        if (rule.DropShuttlePath == default && rule.NtDropShuttlePath == default)
            return;

        if (rule.DropShuttlePath != default)
        {
            SpawnAndDockDropShuttle(
                ev.TypanStation,
                rule.DropShuttlePath,
                "typan-war-drop-shuttle-docked-typan");
        }

        if (rule.NtDropShuttlePath != default)
        {
            SpawnAndDockDropShuttle(
                ev.NtStation,
                rule.NtDropShuttlePath,
                "typan-war-drop-shuttle-docked-nt");
        }
    }

    private void SpawnAndDockDropShuttle(EntityUid station, ResPath shuttlePath, string announcementKey)
    {
        if (!TryComp<StationDataComponent>(station, out var stationData) || stationData.Grids.Count == 0)
        {
            Log.Error($"War drop shuttle: station {station} has no grids.");
            return;
        }

        _map.CreateMap(out var mapId);

        if (!_loader.TryLoadGrid(mapId, shuttlePath, out var shuttleGrid) ||
            !TryComp<ShuttleComponent>(shuttleGrid, out _) ||
            !TryComp<TransformComponent>(shuttleGrid, out var shuttleXform))
        {
            Log.Error($"War drop shuttle: failed to load grid from {shuttlePath}.");
            _map.DeleteMap(mapId);
            return;
        }

        var shuttleUid = shuttleGrid.Value;

        if (!TryDockToFreePort(shuttleUid, shuttleXform, station, stationData, out var config, out var targetGrid))
        {
            Log.Warning($"War drop shuttle: no free docking port on station {station} for {shuttlePath}.");
            Del(shuttleUid);
            _map.DeleteMap(mapId);
            return;
        }

        _station.AddGridToStation(station, shuttleUid);
        _map.DeleteMap(mapId);

        AnnounceDocking(station, shuttleUid, shuttleXform, targetGrid, config, announcementKey);
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

            _shuttle.FTLDock((shuttleUid, shuttleXform), dockConfig);
            config = dockConfig;
            targetGrid = grid;
            return true;
        }

        return false;
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
