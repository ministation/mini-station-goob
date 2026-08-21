// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
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
        var ent = GetPreloaderEntity();
        if (ent == null)
            return;

        Del(ent.Value.Owner);
    }

    private void OnPostGameMapLoad(PostGameMapLoad ev)
    {
        // Lightweight: map shell only. Grid YAML copies are staged via GameTicker.
        EnsureEmptyPreloaderMap();
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

        if (!_mapLoader.TryLoadGrid(mapId, proto.Path, out var grid))
        {
            Log.Error($"Failed to preload grid prototype {proto.ID}");
            return false;
        }

        var (gridUid, mapGrid) = grid.Value;

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
}
