// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Numerics;
using Content.Server.Actions;
using Content.Server.Cargo.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Systems;
using Content.Shared._Mini.TypanWar;
using Content.Shared.GameTicking;
using Content.Shared.Station.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Mini.TypanWar;

public sealed class TypanWarMinimapSystem : EntitySystem
{
    private static readonly EntProtoId MinimapAction = "ActionTypanWarMinimap";

    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StationSystem _station = default!;

    private readonly Dictionary<EntityUid, CachedShape> _shapeCache = new();
    private readonly List<Vector2> _vertScratch = new();
    private readonly List<Vector2i> _tileListScratch = new();
    private readonly HashSet<Vector2i> _tileSetScratch = new();
    private readonly List<(Vector2 Start, Vector2 End)> _edgeScratch = new();
    private readonly (DirectionFlag Dir, Vector2i Offset)[] _neighborDirections = TypanWarMinimapMesh.CreateNeighborDirections();

    private sealed class CachedShape
    {
        public GameTick Tick;
        public Vector2[] Vertices = Array.Empty<Vector2>();
        public int EdgeIndex;
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TypanWarMinimapActionEvent>(_ => { });
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _shapeCache.Clear());
    }

    public void EnsureMinimapAction(EntityUid uid)
    {
        if (!_prototypes.HasIndex(MinimapAction))
        {
            Log.Error("Typan war minimap action prototype is missing: {Prototype}", MinimapAction);
            return;
        }

        var comp = EnsureComp<TypanWarMinimapComponent>(uid);

        if (comp.ActionEntity != null && Exists(comp.ActionEntity))
            return;

        _actions.AddAction(uid, ref comp.ActionEntity, MinimapAction);
    }

    public void RemoveMinimapAction(EntityUid uid)
    {
        if (!TryComp<TypanWarMinimapComponent>(uid, out var comp))
            return;

        if (comp.ActionEntity is { } action)
            _actions.RemoveAction(uid, action);

        RemComp<TypanWarMinimapComponent>(uid);
    }

    /// <summary>
    /// Collects station / shuttle silhouettes. When <paramref name="forceShapes"/> is false,
    /// vertices are only resent for grids whose tiles changed (clients keep prior mesh).
    /// </summary>
    public TypanWarMinimapGrid[] CollectMinimapGrids(TypanStationWarRuleComponent rule, bool forceShapes)
    {
        if (rule.Phase != TypanWarPhase.Active || !rule.LayoutApplied)
            return Array.Empty<TypanWarMinimapGrid>();

        var list = new List<TypanWarMinimapGrid>();

        if (rule.NtStation is { } ntStation && TryComp<StationDataComponent>(ntStation, out var ntData))
            CollectStationGrids(ntStation, ntData, TypanWarMinimapGridKind.NtStation, TypanWarMinimapGridKind.NtShuttle, forceShapes, list);

        if (rule.TypanStation is { } typanStation && TryComp<StationDataComponent>(typanStation, out var typanData))
            CollectStationGrids(typanStation, typanData, TypanWarMinimapGridKind.TypanStation, TypanWarMinimapGridKind.TypanShuttle, forceShapes, list);

        return list.ToArray();
    }

    private void CollectStationGrids(
        EntityUid station,
        StationDataComponent stationData,
        TypanWarMinimapGridKind stationKind,
        TypanWarMinimapGridKind shuttleKind,
        bool forceShapes,
        List<TypanWarMinimapGrid> list)
    {
        var largest = _station.GetLargestGrid((station, stationData));

        foreach (var gridUid in stationData.Grids)
        {
            if (!TryComp<MapGridComponent>(gridUid, out var mapGrid))
                continue;

            var aabb = _lookup.GetWorldAABB(gridUid);
            if (aabb.Size == Vector2.Zero)
                continue;

            var kind = gridUid switch
            {
                _ when HasComp<TradeStationComponent>(gridUid) => TypanWarMinimapGridKind.Trade,
                _ when gridUid != largest && HasComp<ShuttleComponent>(gridUid) => shuttleKind,
                _ => stationKind,
            };

            var name = kind is TypanWarMinimapGridKind.NtShuttle or TypanWarMinimapGridKind.TypanShuttle
                ? MetaData(gridUid).EntityName
                : string.Empty;

            var worldMatrix = _transform.GetWorldMatrix(gridUid);
            var shape = GetOrBuildShape(gridUid, mapGrid, forceShapes, out var sendVertices);

            list.Add(new TypanWarMinimapGrid(
                GetNetEntity(gridUid),
                aabb.Left,
                aabb.Bottom,
                aabb.Right,
                aabb.Top,
                kind,
                name,
                sendVertices ? shape.Vertices : null,
                shape.EdgeIndex,
                shape.Tick.Value,
                worldMatrix));
        }
    }

    private CachedShape GetOrBuildShape(EntityUid gridUid, MapGridComponent mapGrid, bool forceShapes, out bool sendVertices)
    {
        if (_shapeCache.TryGetValue(gridUid, out var cached) && cached.Tick >= mapGrid.LastTileModifiedTick)
        {
            sendVertices = forceShapes;
            return cached;
        }

        TypanWarMinimapMesh.Build(
            gridUid,
            mapGrid,
            _maps,
            _vertScratch,
            out var edgeIndex,
            _tileListScratch,
            _tileSetScratch,
            _edgeScratch,
            _neighborDirections);

        cached = new CachedShape
        {
            Tick = mapGrid.LastTileModifiedTick,
            Vertices = _vertScratch.ToArray(),
            EdgeIndex = edgeIndex,
        };
        _shapeCache[gridUid] = cached;
        sendVertices = true;
        return cached;
    }
}
