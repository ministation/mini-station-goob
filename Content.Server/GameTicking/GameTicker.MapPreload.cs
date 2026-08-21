// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Lavaland.Procedural.Systems;
using Content.Server.GridPreloader;
using Content.Server.Maps;
using Content.Shared._Lavaland.Procedural.Prototypes;
using Content.Shared.GridPreloader.Prototypes;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    [Dependency] private readonly IServerNetManager _netManager = default!;

    private static readonly ProtoId<LavalandMapPrototype> DefaultLavalandPlanet = "Lavaland";

    /// <summary>
    /// True once station maps + late preload stages have finished.
    /// </summary>
    [ViewVariables]
    public bool MapsReady { get; private set; }

    /// <summary>
    /// True while any staged preload work remains (early Lavaland and/or late station maps).
    /// </summary>
    [ViewVariables]
    public bool MapLoadInProgress => (!MapsReady && _mapLoadQueue.Count > 0) ||
                                     (_mapLoadQueueBuilt && !MapsReady);

    private bool _pendingStartRound;
    private bool _pendingStartRoundForce;

    /// <summary>Lavaland stages queued at lobby start (map-vote independent).</summary>
    private bool _earlyLavalandStarted;

    /// <summary>Station/grid/finalize stages queued (T-20s / force-start).</summary>
    private bool _mapLoadQueueBuilt;

    private readonly Queue<MapLoadStage> _mapLoadQueue = new();
    private readonly List<MapId> _loadedStationMapIds = new();
    private readonly HashSet<ProtoId<LavalandMapPrototype>> _completedLavalandPlanets = new();
    private readonly HashSet<ProtoId<LavalandMapPrototype>> _queuedLavalandPlanets = new();

    private readonly record struct MapLoadStage(
        MapLoadStageKind Kind,
        GameMapPrototype? GameMap = null,
        bool IsMain = false,
        ProtoId<LavalandMapPrototype>? LavalandPlanet = null,
        ProtoId<PreloadedGridPrototype>? PreloadedGrid = null);

    private enum MapLoadStageKind : byte
    {
        GameMap,
        LavalandPlanet,
        GridPreloadCreate,
        GridPreloadOne,
        Finalize,
    }

    /// <summary>
    /// Schedule a Lavaland planet as its own tick stage. Safe to call during votes —
    /// default Lavaland is started early via <see cref="BeginEarlyLavalandPreload"/>.
    /// </summary>
    public void EnqueueLavalandPlanet(ProtoId<LavalandMapPrototype> planet)
    {
        if (_completedLavalandPlanets.Contains(planet) || !_queuedLavalandPlanets.Add(planet))
            return;

        _mapLoadQueue.Enqueue(new MapLoadStage(MapLoadStageKind.LavalandPlanet, LavalandPlanet: planet));
    }

    /// <summary>
    /// Lavaland does not depend on the voted station map — start it as soon as the lobby countdown runs.
    /// </summary>
    private void BeginEarlyLavalandPreload()
    {
        if (_earlyLavalandStarted)
            return;

        _earlyLavalandStarted = true;

        var lavaland = EntityManager.System<LavalandSystem>();
        if (!lavaland.LavalandEnabled)
            return;

        lavaland.EnsurePreloaderMap();
        EnqueueLavalandPlanet(DefaultLavalandPlanet);
        _sawmill.Info("Early Lavaland preload queued (lobby / map-vote independent).");
    }

    /// <summary>
    /// Station maps + grid preloader + finalize. Only after map vote window (T-RoundPreloadTime).
    /// </summary>
    private void BeginMapPreload()
    {
        if (MapsReady || _mapLoadQueueBuilt)
            return;

        if (_map.MapExists(DefaultMap) && _loadedStationMapIds.Count > 0)
        {
            MapsReady = true;
            return;
        }

        AddGamePresetRules();

        var maps = new List<GameMapPrototype>();

        var mainStationMap = _gameMapManager.GetSelectedMap();
        if (mainStationMap == null)
        {
            _gameMapManager.SelectMapByConfigRules();
            mainStationMap = _gameMapManager.GetSelectedMap();
        }

        if (mainStationMap != null)
        {
            maps.Add(mainStationMap);
            _gameMapManager.RegisterPlayedMap(mainStationMap.ID);
        }
        else
        {
            throw new Exception("invalid config; couldn't select a valid station map!");
        }

        if (CurrentPreset?.MapPool != null &&
            ProtoMan.TryIndex<GameMapPoolPrototype>(CurrentPreset.MapPool, out var pool) &&
            !pool.Maps.Contains(mainStationMap.ID))
        {
            var msg = Loc.GetString("game-ticker-start-round-invalid-map",
                ("map", mainStationMap.MapName),
                ("mode", Loc.GetString(CurrentPreset.ModeTitle)));
            Log.Debug(msg);
            SendServerMessage(msg);
        }

        // May enqueue extra planets (skipped if already early-loaded / queued).
        RaiseLocalEvent(new LoadingMapsEvent(maps));

        _loadedStationMapIds.Clear();

        if (maps.Count == 0)
        {
            _map.CreateMap(out var mapId, runMapInit: false);
            DefaultMap = mapId;
            _loadedStationMapIds.Add(mapId);
            _mapLoadQueue.Enqueue(new MapLoadStage(MapLoadStageKind.Finalize));
            _mapLoadQueueBuilt = true;
            return;
        }

        for (var i = 0; i < maps.Count; i++)
        {
            _mapLoadQueue.Enqueue(new MapLoadStage(MapLoadStageKind.GameMap, GameMap: maps[i], IsMain: i == 0));
        }

        var gridPreloader = EntityManager.System<GridPreloaderSystem>();
        if (gridPreloader.PreloadingEnabled)
        {
            _mapLoadQueue.Enqueue(new MapLoadStage(MapLoadStageKind.GridPreloadCreate));
            foreach (var gridId in gridPreloader.EnumeratePreloadJobs())
            {
                _mapLoadQueue.Enqueue(new MapLoadStage(MapLoadStageKind.GridPreloadOne, PreloadedGrid: gridId));
            }
        }

        _mapLoadQueue.Enqueue(new MapLoadStage(MapLoadStageKind.Finalize));
        _mapLoadQueueBuilt = true;

        _sawmill.Info($"Late map preload: station/grid stages queued ({_mapLoadQueue.Count} total in queue).");
    }

    private void RequestStartRound(bool force = false)
    {
        _pendingStartRound = true;
        _pendingStartRoundForce = force;
        BeginEarlyLavalandPreload();
        BeginMapPreload();
    }

    private void TryConsumePendingStartRound()
    {
        if (!_pendingStartRound || !MapsReady || _startingRound)
            return;

        _pendingStartRound = false;
        var force = _pendingStartRoundForce;
        _pendingStartRoundForce = false;
        StartRound(force);
    }

    private void ProcessOneMapLoadStage()
    {
        if (MapsReady)
            return;

        if (_mapLoadQueue.Count == 0)
        {
            // Early Lavaland finished but station stages not queued yet — wait for T-20s BeginMapPreload.
            if (_mapLoadQueueBuilt)
                MapsReady = true;
            return;
        }

        YieldNetworkDuringMapLoad();

        var stage = _mapLoadQueue.Dequeue();

        switch (stage.Kind)
        {
            case MapLoadStageKind.GameMap:
            {
                DebugTools.Assert(stage.GameMap != null);
                _sawmill.Info($"Map preload stage: game map '{stage.GameMap.ID}'");
                LoadGameMap(stage.GameMap, out var mapId);
                DebugTools.Assert(!_map.IsInitialized(mapId));
                _loadedStationMapIds.Add(mapId);
                if (stage.IsMain)
                    DefaultMap = mapId;
                break;
            }
            case MapLoadStageKind.LavalandPlanet:
            {
                DebugTools.Assert(stage.LavalandPlanet != null);
                var planet = stage.LavalandPlanet.Value;
                if (_completedLavalandPlanets.Contains(planet))
                    break;

                _sawmill.Info($"Map preload stage: Lavaland '{planet}'");
                var lavaland = EntityManager.System<LavalandSystem>();
                lavaland.EnsurePreloaderMap();
                if (!lavaland.SetupLavalandPlanet(planet, out _))
                    _sawmill.Warning($"Failed to setup Lavaland planet '{planet}' during staged preload.");
                else
                    _completedLavalandPlanets.Add(planet);
                break;
            }
            case MapLoadStageKind.GridPreloadCreate:
            {
                EntityManager.System<GridPreloaderSystem>().EnsureEmptyPreloaderMap();
                break;
            }
            case MapLoadStageKind.GridPreloadOne:
            {
                DebugTools.Assert(stage.PreloadedGrid != null);
                EntityManager.System<GridPreloaderSystem>().TryLoadOnePreloadedGrid(stage.PreloadedGrid.Value);
                break;
            }
            case MapLoadStageKind.Finalize:
            {
                MapsReady = true;
                _sawmill.Info("Staged map preload complete.");
                break;
            }
        }

        YieldNetworkDuringMapLoad();
    }

    private void YieldNetworkDuringMapLoad()
    {
        try
        {
            _netManager.ProcessPackets();
            _taskManager.ProcessPendingTasks();
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error while yielding network during map preload:\n{e}");
        }
    }

    private void InitializeLoadedStationMaps()
    {
        foreach (var mapId in _loadedStationMapIds)
        {
            if (!_map.MapExists(mapId) || _map.IsInitialized(mapId))
                continue;

            _map.InitializeMap(mapId);
            YieldNetworkDuringMapLoad();
        }
    }

    private void ResetMapPreloadState()
    {
        MapsReady = false;
        _earlyLavalandStarted = false;
        _mapLoadQueueBuilt = false;
        _pendingStartRound = false;
        _pendingStartRoundForce = false;
        _mapLoadQueue.Clear();
        _loadedStationMapIds.Clear();
        _completedLavalandPlanets.Clear();
        _queuedLavalandPlanets.Clear();
    }
}
