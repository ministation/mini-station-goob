// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Lavaland.Procedural.Systems;
using Content.Server._Mini.Diagnostics;
using Content.Server.GridPreloader;
using Content.Server.Maps;
using Content.Shared._Lavaland.Procedural.Components;
using Content.Shared._Lavaland.Procedural.Prototypes;
using Content.Shared._Mini.MiniCCVars;
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

    /// <summary>Staged preload master switch, from <see cref="MiniCCVars.MapPreloadEnabled"/>.</summary>
    [ViewVariables]
    public bool StagedMapPreloadEnabled => _mapPreloadEnabled;

    /// <summary>
    /// Lavaland is staged during the lobby countdown on its own switch: it is needed by every
    /// round anyway, while station/grid staging behind <see cref="StagedMapPreloadEnabled"/> is
    /// what doubles the preload working set.
    /// </summary>
    public bool StagedLavalandPreloadEnabled => _mapPreloadLavaland;

    private bool _mapPreloadEnabled;
    private bool _mapPreloadLavaland;
    private bool _mapPreloadGrids;
    private int _mapPreloadMaxStations = 2;

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
        LavalandRuins,
        GridPreloadCreate,
        GridPreloadOne,
        Finalize,
    }

    /// <summary>
    /// Max main-thread time per Lavaland ruin-batch stage. A single ruin load is atomic and can
    /// exceed it, but the batch re-queues so the total never blocks one tick like before.
    /// </summary>
    private static readonly TimeSpan RuinBatchBudget = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Schedule a Lavaland planet as its own tick stage. Safe to call during votes —
    /// default Lavaland is started early via <see cref="BeginEarlyLavalandPreload"/>.
    /// </summary>
    public void EnqueueLavalandPlanet(ProtoId<LavalandMapPrototype> planet)
    {
        // Only staged mode queues planets. Otherwise (game.map_preload_lavaland = false)
        // LavalandSystem loads the planet synchronously from LoadingMapsEvent.
        if (!StagedLavalandPreloadEnabled)
            return;

        // Every gameMap prototype defaults to Planets = { Lavaland }, so maps queue it even when
        // Lavaland is off (lavaland.enabled = false, e.g. the dev preset). SetupLavalandPlanet
        // would bail out on that cvar and the stage would only log a warning, so don't queue at all.
        if (!EntityManager.System<LavalandSystem>().LavalandEnabled)
            return;

        if (_completedLavalandPlanets.Contains(planet) || !_queuedLavalandPlanets.Add(planet))
            return;

        _mapLoadQueue.Enqueue(new MapLoadStage(MapLoadStageKind.LavalandPlanet, LavalandPlanet: planet));
        _mapLoadQueue.Enqueue(new MapLoadStage(MapLoadStageKind.LavalandRuins));
    }

    /// <summary>
    /// Lavaland does not depend on the voted station map — start it as soon as the lobby countdown runs.
    /// </summary>
    private void BeginEarlyLavalandPreload()
    {
        if (_earlyLavalandStarted)
            return;

        _earlyLavalandStarted = true;

        // With Lavaland staging disabled it is generated synchronously at T-RoundPreloadTime.
        if (!StagedLavalandPreloadEnabled)
            return;

        var lavaland = EntityManager.System<LavalandSystem>();
        if (!lavaland.LavalandEnabled)
            return;

        lavaland.EnsurePreloaderMap();
        EnqueueLavalandPlanet(DefaultLavalandPlanet);
        _sawmill.Info("Early Lavaland preload queued (lobby / map-vote independent).");
    }

    /// <summary>
    /// Station maps + grid preloader + finalize. Only after map vote window (T-RoundPreloadTime).
    /// With <see cref="MiniCCVars.MapPreloadEnabled"/> off this loads everything synchronously,
    /// which is the pre-feature path: the round still starts, it just blocks one tick longer.
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

        var maps = SelectStationMaps();
        _loadedStationMapIds.Clear();

        if (maps.Count == 0)
        {
            _map.CreateMap(out var mapId, runMapInit: false);
            DefaultMap = mapId;
            _loadedStationMapIds.Add(mapId);

            if (_mapPreloadEnabled)
                _mapLoadQueue.Enqueue(new MapLoadStage(MapLoadStageKind.Finalize));
            else
                MapsReady = true;

            _mapLoadQueueBuilt = true;
            return;
        }

        if (!_mapPreloadEnabled)
        {
            LoadStationMapsSynchronously(maps);
            return;
        }

        // Never let a bug or an over-eager LoadingMapsEvent handler turn the lobby into a
        // "load every map at once" memory spike. Extra maps are loaded now, not dropped, so
        // war presets still get their second station.
        var stagedCount = Math.Min(maps.Count, Math.Max(1, _mapPreloadMaxStations));
        for (var i = 0; i < stagedCount; i++)
        {
            _mapLoadQueue.Enqueue(new MapLoadStage(MapLoadStageKind.GameMap, GameMap: maps[i], IsMain: i == 0));
        }

        if (stagedCount < maps.Count)
        {
            _sawmill.Warning($"Staged map preload is limited to {stagedCount} station map(s); " +
                             $"loading {maps.Count - stagedCount} extra map(s) synchronously.");

            for (var i = stagedCount; i < maps.Count; i++)
                LoadStationMapSynchronously(maps[i], isMain: false);
        }

        var gridPreloader = EntityManager.System<GridPreloaderSystem>();
        if (_mapPreloadGrids && gridPreloader.PreloadingEnabled)
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

    /// <summary>
    /// Picks the station maps for this round and lets game rules/Lavaland add their own.
    /// </summary>
    private List<GameMapPrototype> SelectStationMaps()
    {
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

        // May add extra planets/stations (skipped if already early-loaded / queued).
        RaiseLocalEvent(new LoadingMapsEvent(maps));

        return maps;
    }

    private void LoadStationMapsSynchronously(List<GameMapPrototype> maps)
    {
        for (var i = 0; i < maps.Count; i++)
        {
            LoadStationMapSynchronously(maps[i], isMain: i == 0);
        }

        CompleteSynchronousMapLoad();

        _sawmill.Info($"Synchronous map load complete ({maps.Count} station map(s)).");
    }

    /// <summary>
    /// Marks the synchronous map load done. If Lavaland staging still has stages queued (slow
    /// lobby or a late start), let the per-tick queue drain first: the round must not start on
    /// a half-generated planet.
    /// </summary>
    private void CompleteSynchronousMapLoad()
    {
        _mapLoadQueueBuilt = true;

        if (_mapLoadQueue.Count == 0)
        {
            MapsReady = true;
            return;
        }

        _mapLoadQueue.Enqueue(new MapLoadStage(MapLoadStageKind.Finalize));
        _sawmill.Info($"Waiting for {_mapLoadQueue.Count - 1} staged Lavaland stage(s) before round start.");
    }

    private void LoadStationMapSynchronously(GameMapPrototype gameMap, bool isMain)
    {
        _sawmill.Info($"Map preload: game map '{gameMap.ID}' (synchronous)");

        MapId mapId = default;
        SlowOpLog.Run(_sawmill, $"Map preload: game map '{gameMap.ID}'", () => LoadGameMap(gameMap, out mapId));
        DebugTools.Assert(!_map.IsInitialized(mapId));

        _loadedStationMapIds.Add(mapId);
        if (isMain)
            DefaultMap = mapId;
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
                MapId mapId = default;
                SlowOpLog.Run(_sawmill, $"Map preload: game map '{stage.GameMap.ID}'", () => LoadGameMap(stage.GameMap!, out mapId));
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
                Entity<LavalandMapComponent>? planetMap = null;
                SlowOpLog.Run(_sawmill, $"Map preload: Lavaland '{planet}'", () =>
                {
                    if (lavaland.SetupLavalandPlanet(planet, out var map, withRuins: false))
                        planetMap = map;
                });
                if (planetMap != null)
                    _completedLavalandPlanets.Add(planet);
                else
                    _sawmill.Warning($"Failed to setup Lavaland planet '{planet}' during staged preload.");
                break;
            }
            case MapLoadStageKind.LavalandRuins:
            {
                var lavaland = EntityManager.System<LavalandSystem>();
                var done = true;
                SlowOpLog.Run(_sawmill, "Map preload: Lavaland ruins batch", () => done = lavaland.ProcessRuinsBatch(RuinBatchBudget));
                if (!done)
                    _mapLoadQueue.Enqueue(new MapLoadStage(MapLoadStageKind.LavalandRuins));
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
                SlowOpLog.Run(_sawmill, $"Map preload: grid '{stage.PreloadedGrid.Value}'", () =>
                    EntityManager.System<GridPreloaderSystem>().TryLoadOnePreloadedGrid(stage.PreloadedGrid.Value));
                break;
            }
            case MapLoadStageKind.Finalize:
            {
                // Batched stages (Lavaland ruins) re-queue themselves behind Finalize, so the
                // queue must be empty for the map load to be really done.
                if (_mapLoadQueue.Count > 0)
                {
                    _mapLoadQueue.Enqueue(new MapLoadStage(MapLoadStageKind.Finalize));
                    break;
                }

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

            SlowOpLog.Run(_sawmill, $"Map init: map {mapId}", () => _map.InitializeMap(mapId));
            YieldNetworkDuringMapLoad();
        }
    }

    private void InitializeMapPreloadCVars()
    {
        Subs.CVar(_cfg, MiniCCVars.MapPreloadEnabled, value => _mapPreloadEnabled = value, true);
        Subs.CVar(_cfg, MiniCCVars.MapPreloadLavaland, value => _mapPreloadLavaland = value, true);
        Subs.CVar(_cfg, MiniCCVars.MapPreloadGrids, value => _mapPreloadGrids = value, true);
        Subs.CVar(_cfg, MiniCCVars.MapPreloadMaxStations,
            value => _mapPreloadMaxStations = Math.Max(1, value), true);
    }

    /// <summary>
    /// Frees the round that just ended before the next lobby preload allocates its maps.
    /// Deleting entities only makes them garbage; without a full collection the previous and
    /// the next working sets overlap in RSS and the host ends up in swap.
    /// </summary>
    private void CollectRoundGarbage()
    {
        SlowOpLog.Run(_sawmill, "Memory: GC after round cleanup", () =>
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        });
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
