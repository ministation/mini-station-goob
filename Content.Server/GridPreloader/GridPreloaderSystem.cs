// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Server._Mini.Diagnostics;
using Content.Shared.CCVar;
using Content.Shared.GridPreloader.Prototypes;
using Content.Shared.GridPreloader.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using System.Numerics;
using Content.Server.GameTicking;
using Content.Shared._Mini.MiniCCVars;
using Content.Shared.GameTicking;
using JetBrains.Annotations;
using Robust.Shared.EntitySerialization.Systems;

namespace Content.Server.GridPreloader;

public sealed class GridPreloaderSystem : SharedGridPreloaderSystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    /// <summary>
    /// Whether the preloading CVar is set or not.
    /// </summary>
    public bool PreloadingEnabled;

    private float _globalXOffset;
    private bool _syncPreloadDone;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        // Create the empty preloader map early if a station loads before staged GridPreloadCreate.
        SubscribeLocalEvent<PostGameMapLoad>(OnPostGameMapLoad);

        Subs.CVar(_cfg, CCVars.PreloadGrids, value => PreloadingEnabled = value, true);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _globalXOffset = 0f;
        _syncPreloadDone = false;
        var ent = GetPreloaderEntity();
        if (ent == null)
            return;

        Del(ent.Value.Owner);
    }

    private void OnPostGameMapLoad(PostGameMapLoad ev)
    {
        if (!EnsureEmptyPreloaderMap())
            return;

        // With staged preload on, GameTicker queues grid copies and they are loaded across ticks.
        // With it off, still honor the grid preload switch by loading every copy synchronously.
        if (!_cfg.GetCVar(MiniCCVars.MapPreloadEnabled) &&
            _cfg.GetCVar(MiniCCVars.MapPreloadGrids))
        {
            PreloadAllGrids();
        }
    }

    /// <summary>
    /// Synchronously loads every configured <see cref="PreloadedGridPrototype"/> copy. Used by the
    /// non-staged fallback path; safe to call more than once per round.
    /// </summary>
    public void PreloadAllGrids()
    {
        if (_syncPreloadDone)
            return;

        _syncPreloadDone = true;

        foreach (var gridId in EnumeratePreloadJobs())
        {
            TryLoadOnePreloadedGrid(gridId);
        }
    }

    /// <summary>
    /// One PreloadedGridPrototype ID per copy that still needs loading.
    /// </summary>
    public IEnumerable<ProtoId<PreloadedGridPrototype>> EnumeratePreloadJobs()
    {
        if (!PreloadingEnabled)
            yield break;

        foreach (var proto in _prototype.EnumeratePrototypes<PreloadedGridPrototype>())
        {
            for (var i = 0; i < proto.Copies; i++)
                yield return proto.ID;
        }
    }

    /// <summary>
    /// Creates the paused preloader map without loading any grids.
    /// </summary>
    public bool EnsureEmptyPreloaderMap()
    {
        if (GetPreloaderEntity() != null)
            return true;

        if (!PreloadingEnabled)
            return false;

        var mapUid = _map.CreateMap(out var mapId, false);
        EnsureComp<GridPreloaderComponent>(mapUid);
        _meta.SetEntityName(mapUid, "GridPreloader Map");
        _map.SetPaused(mapId, true);
        _globalXOffset = 0f;
        return true;
    }

    /// <summary>
    /// Loads a single preloaded-grid copy onto the preloader map.
    /// </summary>
    public bool TryLoadOnePreloadedGrid(ProtoId<PreloadedGridPrototype> protoId)
    {
        if (!PreloadingEnabled)
            return false;

        if (!_prototype.TryIndex(protoId, out var proto))
        {
            Log.Error($"Failed to preload grid prototype {protoId}: missing prototype.");
            return false;
        }

        if (!EnsureEmptyPreloaderMap())
            return false;

        var preloaderEnt = GetPreloaderEntity();
        if (preloaderEnt == null)
            return false;

        var (mapUid, preloader) = preloaderEnt.Value;
        var mapId = Comp<MapComponent>(mapUid).MapId;

        bool loaded = false;
        Entity<MapGridComponent>? grid = null;
        SlowOpLog.Run($"Grid preload: '{protoId}'",
            () => loaded = _mapLoader.TryLoadGrid(mapId, proto.Path, out grid));
        if (!loaded || grid == null)
        {
            Log.Error($"Failed to preload grid prototype {proto.ID}");
            return false;
        }

        var gridUid = grid.Value.Owner;
        var mapGrid = grid.Value.Comp;

        if (!TryComp<PhysicsComponent>(gridUid, out var physics))
            return false;

        _globalXOffset += mapGrid.LocalAABB.Width / 2;

        var coords = new Vector2(-physics.LocalCenter.X + _globalXOffset, -physics.LocalCenter.Y);
        _transform.SetCoordinates(gridUid, new EntityCoordinates(mapUid, coords));

        _globalXOffset += (mapGrid.LocalAABB.Width / 2) + 1;

        if (!preloader.PreloadedGrids.ContainsKey(proto.ID))
            preloader.PreloadedGrids[proto.ID] = new();
        preloader.PreloadedGrids[proto.ID].Add(gridUid);

        return true;
    }

    /// <summary>
    ///     Should be a singleton no matter station count, so we can assume 1
    ///     (better support for singleton component in engine at some point i guess)
    /// </summary>
    public Entity<GridPreloaderComponent>? GetPreloaderEntity()
    {
        var query = AllEntityQuery<GridPreloaderComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            return (uid, comp);
        }

        return null;
    }

    /// <summary>
    /// An attempt to get a certain preloaded shuttle. If there are no more such shuttles left, returns null
    /// </summary>
    [PublicAPI]
    public bool TryGetPreloadedGrid(ProtoId<PreloadedGridPrototype> proto, [NotNullWhen(true)] out EntityUid? preloadedGrid, GridPreloaderComponent? preloader = null)
    {
        preloadedGrid = null;

        if (preloader == null)
        {
            preloader = GetPreloaderEntity();
            if (preloader == null)
                return false;
        }

        if (!preloader.PreloadedGrids.TryGetValue(proto, out var list) || list.Count <= 0)
            return false;

        preloadedGrid = list[0];

        list.RemoveAt(0);
        if (list.Count == 0)
            preloader.PreloadedGrids.Remove(proto);

        return true;
    }

    /// <summary>
    /// Takes a preloaded copy if one exists, otherwise loads the prototype on demand. This keeps
    /// event shuttles working when lobby grid preloading is disabled: memory is only paid for the
    /// grids a rule actually asks for.
    /// </summary>
    public bool TryGetOrLoadPreloadedGrid(ProtoId<PreloadedGridPrototype> proto,
        [NotNullWhen(true)] out EntityUid? preloadedGrid,
        GridPreloaderComponent? preloader = null)
    {
        if (TryGetPreloadedGrid(proto, out preloadedGrid, preloader))
            return true;

        if (!PreloadingEnabled)
            return false;

        if (!TryLoadOnePreloadedGrid(proto))
            return false;

        return TryGetPreloadedGrid(proto, out preloadedGrid, preloader);
    }
}
