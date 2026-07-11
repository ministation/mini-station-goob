// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.Resources;
using Content.Shared._Mini.TypanWar;
using Content.Shared.Maps;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Threading;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Mini.TypanWar;

public sealed class TypanWarMinimapControl : Control
{
    private const float MinZoom = 0.5f;
    private const float MaxZoom = 4f;
    private const float ZoomStep = 0.12f;
    private const float BlipSize = 11f;
    private const float DrawBatchSize = 3f * 4096;

    private static readonly Color Background = Color.FromHex("#0A0A10").WithAlpha(0.95f);
    private static readonly Color Border = Color.FromHex("#5A5670").WithAlpha(0.7f);

    private static readonly Color NtStationFill = Color.FromHex("#1A2848").WithAlpha(0.88f);
    private static readonly Color NtStationEdge = Color.FromHex("#5A9FFF");
    private static readonly Color TypanStationFill = Color.FromHex("#421E1E").WithAlpha(0.88f);
    private static readonly Color TypanStationEdge = Color.FromHex("#FF6666");
    private static readonly Color TradeFill = Color.FromHex("#423818").WithAlpha(0.88f);
    private static readonly Color TradeEdge = Color.FromHex("#D8C860");
    private static readonly Color NtShuttleFill = Color.FromHex("#1A2848").WithAlpha(0.9f);
    private static readonly Color NtShuttleEdge = Color.FromHex("#5A9FFF");
    private static readonly Color TypanShuttleFill = Color.FromHex("#3A1818").WithAlpha(0.9f);
    private static readonly Color TypanShuttleEdge = Color.FromHex("#E85050");

    private static readonly Color NtBlip = Color.FromHex("#5A9FFF");
    private static readonly Color TypanBlip = Color.FromHex("#FF6060");
    private static readonly Color NtAllyBlip = Color.FromHex("#8CC4FF");
    private static readonly Color TypanAllyBlip = Color.FromHex("#FF9090");
    private static readonly Color SelfBlip = Color.FromHex("#66FF66");

    [Dependency] private readonly IEntityManager _ent = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IParallelManager _parallel = default!;

    private SharedMapSystem? _map;
    private TurfSystem? _turf;
    private TransformSystem? _xform;
    private VectorFont? _font;
    private VectorFont? _zoneFont;

    private TypanWarMinimapGrid[] _grids = [];
    private TypanWarCaptureZoneStatus[] _zones = [];
    private TypanWarAllyBlip[] _allies = [];
    private NetEntity[] _cachedGridKeys = [];

    private readonly Dictionary<EntityUid, CachedGridShape> _shapeCache = new();
    private readonly List<TypanWarMinimapGrid> _buildQueue = new();
    private readonly List<Vector2> _screenVerts = new();
    private readonly HashSet<Vector2i> _tileSet = new();
    private readonly List<Vector2i> _tileList = new();
    private readonly List<(Vector2 Start, Vector2 End)> _edges = new();
    private readonly (DirectionFlag Dir, Vector2i Offset)[] _neighborDirections = new (DirectionFlag, Vector2i)[4];

    private GridDrawJob _drawJob;
    private Vector2[] _scaledVerts = Array.Empty<Vector2>();

    private float _zoom = 1.15f;
    private Vector2 _pan;
    private bool _dragging;
    private Vector2 _dragStart;
    private Vector2 _panStart;

    private bool _hasViewBounds;
    private float _viewMinX;
    private float _viewMaxX;
    private float _viewMinY;
    private float _viewMaxY;

    public TypanWarMinimapControl()
    {
        IoCManager.InjectDependencies(this);

        for (var i = 0; i < 4; i++)
        {
            var dir = (DirectionFlag) Math.Pow(2, i);
            _neighborDirections[i] = (dir, dir.AsDir().ToIntVec());
        }

        _drawJob = new GridDrawJob
        {
            ScaledVertices = _scaledVerts,
        };

        MouseFilter = MouseFilterMode.Stop;
    }

    public void Update(TypanWarMinimapGrid[] grids, TypanWarCaptureZoneStatus[] zones, TypanWarAllyBlip[] allies)
    {
        var gridsChanged = !GridKeysEqual(_cachedGridKeys, grids);

        _grids = grids;
        _zones = zones;
        _allies = allies;

        if (gridsChanged)
        {
            _hasViewBounds = false;
            _cachedGridKeys = grids.Select(g => g.Grid).ToArray();
            QueueShapeRebuild();
        }

        if (_grids.Length > 0)
            UpdateViewBounds();
    }

    /// <summary>
    /// Warm up bounds and start tile mesh building before the window becomes visible.
    /// </summary>
    public void PrepareForDisplay()
    {
        if (_grids.Length > 0)
            UpdateViewBounds();

        QueueShapeRebuild();
        ProcessBuildQueue(int.MaxValue);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        ProcessBuildQueue(maxGrids: 1);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.Use)
            return;

        _dragging = true;
        _dragStart = args.RelativePixelPosition;
        _panStart = _pan;
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function == EngineKeyFunctions.Use)
            _dragging = false;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (!_dragging)
            return;

        _pan = _panStart + (args.RelativePixelPosition - _dragStart);
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        if (args.Delta.Y == 0)
            return;

        var oldZoom = _zoom;
        var factor = 1f + ZoomStep * args.Delta.Y;
        _zoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);

        if (Math.Abs(_zoom - oldZoom) < 0.0001f)
            return;

        var cursor = args.RelativePixelPosition;
        var center = PixelSize / 2f;
        _pan = cursor - (cursor - center - _pan) * (_zoom / oldZoom) - center;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        _font ??= new VectorFont(_cache.GetResource<FontResource>(MiniFonts.Bold), 12);
        _zoneFont ??= new VectorFont(_cache.GetResource<FontResource>(MiniFonts.Bold), 17);
        _xform ??= _ent.System<TransformSystem>();

        var box = PixelSizeBox;
        if (box.Width <= 1 || box.Height <= 1)
            return;

        handle.DrawRect(box, Background);
        handle.DrawRect(box, Border, false);

        if (!_hasViewBounds && _grids.Length > 0)
            UpdateViewBounds();

        if (!TryBuildTransform(box, out var map))
            return;

        foreach (var grid in _grids)
        {
            if (!_ent.TryGetEntity(grid.Grid, out var gridUid) || gridUid is not { } uid)
                continue;

            if (!_shapeCache.TryGetValue(uid, out var shape))
                continue;

            DrawCachedShape(handle, map, shape);

            if (!shape.IsShuttle)
                continue;

            var center = map.WorldToScreen(shape.CenterWorld.X, shape.CenterWorld.Y);
            var blipColor = shape.Kind == TypanWarMinimapGridKind.NtShuttle ? NtBlip : TypanBlip;
            DrawMassScannerBlip(handle, center, blipColor, BlipSize);

            if (!string.IsNullOrEmpty(shape.Label))
                DrawEntityLabel(handle, center, shape.Label, blipColor);
        }

        foreach (var zone in _zones)
        {
            if (!zone.Active)
                continue;

            DrawZoneMarker(handle, map, zone);
        }

        var localSide = GetLocalSide();
        var localPos = GetLocalWorldPosition();

        foreach (var ally in _allies)
        {
            if (localSide == null || ally.Side != localSide)
                continue;

            if (localPos != null && IsSamePosition(ally.WorldX, ally.WorldY, localPos.Value.X, localPos.Value.Y, 2f))
                continue;

            var color = ally.Side == TypanWarSide.Nanotrasen ? NtAllyBlip : TypanAllyBlip;
            DrawCircleBlip(handle, map.WorldToScreen(ally.WorldX, ally.WorldY), color, 5f);
        }

        if (localPos is { } self)
            DrawCircleBlip(handle, map.WorldToScreen(self.X, self.Y), SelfBlip, 6f);
    }

    private void QueueShapeRebuild()
    {
        _buildQueue.Clear();

        foreach (var grid in _grids)
        {
            if (!_ent.TryGetEntity(grid.Grid, out var gridUid) || gridUid is not { } uid)
                continue;

            if (_ent.TryGetComponent(uid, out MapGridComponent? mapGrid) &&
                _shapeCache.TryGetValue(uid, out var cached) &&
                cached.LastBuild >= mapGrid.LastTileModifiedTick)
            {
                continue;
            }

            _buildQueue.Add(grid);
        }

        var activeIds = _grids
            .Select(g => g.Grid)
            .Where(net => _ent.TryGetEntity(net, out _))
            .Select(net => _ent.GetEntity(net))
            .ToHashSet();

        foreach (var uid in _shapeCache.Keys.ToArray())
        {
            if (!activeIds.Contains(uid))
                _shapeCache.Remove(uid);
        }
    }

    private void ProcessBuildQueue(int maxGrids)
    {
        if (_buildQueue.Count == 0)
            return;

        _map ??= _ent.System<SharedMapSystem>();
        _turf ??= _ent.System<TurfSystem>();
        _xform ??= _ent.System<TransformSystem>();

        var built = 0;

        while (_buildQueue.Count > 0 && built < maxGrids)
        {
            var gridInfo = _buildQueue[0];
            _buildQueue.RemoveAt(0);

            if (!_ent.TryGetEntity(gridInfo.Grid, out var gridUid) || gridUid is not { } uid)
                continue;

            if (!_ent.TryGetComponent(uid, out MapGridComponent? mapGrid))
                continue;

            if (_shapeCache.TryGetValue(uid, out var cached) && cached.LastBuild >= mapGrid.LastTileModifiedTick)
                continue;

            var (fill, edge) = gridInfo.Kind switch
            {
                TypanWarMinimapGridKind.NtStation => (NtStationFill, NtStationEdge),
                TypanWarMinimapGridKind.TypanStation => (TypanStationFill, TypanStationEdge),
                TypanWarMinimapGridKind.NtShuttle => (NtShuttleFill, NtShuttleEdge),
                TypanWarMinimapGridKind.TypanShuttle => (TypanShuttleFill, TypanShuttleEdge),
                _ => (TradeFill, TradeEdge),
            };

            if (TryBuildGridShape(uid, mapGrid, gridInfo, fill, edge, out var shape))
                _shapeCache[uid] = shape;
            else if (_shapeCache.TryGetValue(uid, out var fallback))
                _shapeCache[uid] = fallback;

            built++;
        }
    }

    private bool TryBuildGridShape(
        EntityUid gridUid,
        MapGridComponent mapGrid,
        TypanWarMinimapGrid gridInfo,
        Color fill,
        Color edge,
        out CachedGridShape shape)
    {
        shape = default;

        _tileSet.Clear();
        _tileList.Clear();

        var tileSize = mapGrid.TileSize;
        var worldMatrix = _xform!.GetWorldMatrix(gridUid);
        var vertices = new List<Vector2>();
        var minWorld = new Vector2(float.MaxValue, float.MaxValue);
        var maxWorld = new Vector2(float.MinValue, float.MinValue);

        void IncludeWorld(Vector2 world)
        {
            minWorld = Vector2.Min(minWorld, world);
            maxWorld = Vector2.Max(maxWorld, world);
        }

        var rator = _map!.GetAllTilesEnumerator(gridUid, mapGrid);
        while (rator.MoveNext(out var tileRef))
        {
            var tile = tileRef.Value;

            if (tile.Tile.IsEmpty || _turf!.IsSpace(tile))
                continue;

            var def = _turf.GetContentTileDefinition(tile);
            if (def.MapAtmosphere)
                continue;

            var index = tile.GridIndices;
            _tileSet.Add(index);
            _tileList.Add(index);

            var bl = _map.TileToVector((gridUid, mapGrid), index);
            var br = bl + new Vector2(tileSize, 0f);
            var tr = bl + new Vector2(tileSize, tileSize);
            var tl = bl + new Vector2(0f, tileSize);

            AddWorldTri(vertices, worldMatrix, bl, br, tl);
            AddWorldTri(vertices, worldMatrix, br, tr, tl);

            IncludeWorld(Vector2.Transform(bl, worldMatrix));
            IncludeWorld(Vector2.Transform(tr, worldMatrix));
        }

        if (_tileSet.Count == 0)
            return false;

        var edgeIndex = vertices.Count;
        _edges.Clear();

        foreach (var index in _tileList)
        {
            var bl = _map.TileToVector((gridUid, mapGrid), index);
            var br = bl + new Vector2(tileSize, 0f);
            var tr = bl + new Vector2(tileSize, tileSize);
            var tl = bl + new Vector2(0f, tileSize);

            foreach (var (dir, dirVec) in _neighborDirections)
            {
                if (_tileSet.Contains(index + dirVec))
                    continue;

                var (start, end) = dir switch
                {
                    DirectionFlag.South => (bl, br),
                    DirectionFlag.East => (br, tr),
                    DirectionFlag.North => (tr, tl),
                    DirectionFlag.West => (tl, bl),
                    _ => throw new NotImplementedException(),
                };

                _edges.Add((start, end));
            }
        }

        MergeCollinearEdges();

        foreach (var (start, end) in _edges)
        {
            vertices.Add(Vector2.Transform(start, worldMatrix));
            vertices.Add(Vector2.Transform(end, worldMatrix));
        }

        var centerWorld = minWorld.X <= maxWorld.X
            ? (minWorld + maxWorld) * 0.5f
            : new Vector2((gridInfo.MinX + gridInfo.MaxX) * 0.5f, (gridInfo.MinY + gridInfo.MaxY) * 0.5f);

        shape = new CachedGridShape(
            gridUid,
            mapGrid.LastTileModifiedTick,
            vertices,
            edgeIndex,
            fill,
            edge,
            IsShuttleKind(gridInfo.Kind),
            gridInfo.Kind,
            gridInfo.Name,
            centerWorld);
        return true;
    }

    private void MergeCollinearEdges()
    {
        var merged = true;

        while (merged)
        {
            merged = false;

            for (var i = 0; i < _edges.Count; i++)
            {
                var (start, end) = _edges[i];

                for (var j = i + 1; j < _edges.Count; j++)
                {
                    var (neighborStart, neighborEnd) = _edges[j];

                    if (!end.Equals(neighborStart))
                        continue;

                    if (!CollinearSimplifier.IsCollinear(start, end, neighborEnd, 10f * float.Epsilon))
                        continue;

                    _edges[i] = (start, neighborEnd);
                    _edges.RemoveAt(j);
                    merged = true;
                    break;
                }

                if (merged)
                    break;
            }
        }
    }

    private static void AddWorldTri(List<Vector2> tris, Matrix3x2 worldMatrix, Vector2 a, Vector2 b, Vector2 c)
    {
        tris.Add(Vector2.Transform(a, worldMatrix));
        tris.Add(Vector2.Transform(b, worldMatrix));
        tris.Add(Vector2.Transform(c, worldMatrix));
    }

    private static bool GridKeysEqual(NetEntity[] cached, TypanWarMinimapGrid[] grids)
    {
        if (cached.Length != grids.Length)
            return false;

        for (var i = 0; i < cached.Length; i++)
        {
            if (cached[i] != grids[i].Grid)
                return false;
        }

        return true;
    }

    private void UpdateViewBounds()
    {
        if (_hasViewBounds)
            return;

        var minX = float.MaxValue;
        var maxX = float.MinValue;
        var minY = float.MaxValue;
        var maxY = float.MinValue;
        var hasData = false;

        foreach (var grid in _grids)
        {
            hasData = true;
            minX = Math.Min(minX, grid.MinX);
            maxX = Math.Max(maxX, grid.MaxX);
            minY = Math.Min(minY, grid.MinY);
            maxY = Math.Max(maxY, grid.MaxY);
        }

        foreach (var zone in _zones)
        {
            hasData = true;
            minX = Math.Min(minX, zone.WorldX);
            maxX = Math.Max(maxX, zone.WorldX);
            minY = Math.Min(minY, zone.WorldY);
            maxY = Math.Max(maxY, zone.WorldY);
        }

        if (!hasData)
            return;

        const float pad = 8f;
        _viewMinX = minX - pad;
        _viewMaxX = maxX + pad;
        _viewMinY = minY - pad;
        _viewMaxY = maxY + pad;
        _hasViewBounds = true;
    }

    private void DrawCachedShape(DrawingHandleScreen handle, MapTransform map, CachedGridShape shape)
    {
        var total = shape.Vertices.Count;
        if (total == 0)
            return;

        Extensions.EnsureLength(ref _scaledVerts, total);

        _drawJob.MapTransform = map;
        _drawJob.Vertices = shape.Vertices;
        _drawJob.ScaledVertices = _scaledVerts;
        _parallel.ProcessNow(_drawJob, total);

        var triCount = shape.EdgeIndex;
        var edgeCount = total - triCount;

        for (var i = 0; i < Math.Ceiling(triCount / DrawBatchSize); i++)
        {
            var start = (int) (i * DrawBatchSize);
            var end = (int) Math.Min(triCount, start + DrawBatchSize);
            var count = end - start;
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, new Span<Vector2>(_scaledVerts, start, count), shape.Fill);
        }

        if (edgeCount > 0)
            handle.DrawPrimitives(DrawPrimitiveTopology.LineList, new Span<Vector2>(_scaledVerts, triCount, edgeCount), shape.Edge);
    }

    private static bool IsShuttleKind(TypanWarMinimapGridKind kind) =>
        kind is TypanWarMinimapGridKind.NtShuttle or TypanWarMinimapGridKind.TypanShuttle;

    private static void DrawCircleBlip(DrawingHandleScreen handle, Vector2 center, Color color, float radius)
    {
        handle.DrawCircle(center, radius + 1.5f, Color.FromHex("#101018").WithAlpha(0.85f));
        handle.DrawCircle(center, radius, color.WithAlpha(0.95f));
    }

    private static void DrawMassScannerBlip(DrawingHandleScreen handle, Vector2 center, Color color, float size)
    {
        var points = new Vector2[]
        {
            center + new Vector2(0f, -size),
            center + new Vector2(size, size * 0.55f),
            center + new Vector2(-size, size * 0.55f),
        };

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, points, color.WithAlpha(0.95f));
    }

    private void DrawEntityLabel(DrawingHandleScreen handle, Vector2 anchor, string label, Color color)
    {
        if (_font == null || string.IsNullOrWhiteSpace(label))
            return;

        var textPos = anchor + new Vector2(0f, BlipSize + 4f);
        var dims = handle.GetDimensions(_font, label, 1f);
        var box = new UIBox2(
            textPos - new Vector2(dims.X * 0.5f + 3f, 1f),
            textPos + new Vector2(dims.X * 0.5f + 3f, dims.Y + 1f));

        handle.DrawRect(box, Background.WithAlpha(0.85f));
        handle.DrawString(_font, textPos - new Vector2(dims.X * 0.5f, 0f), label, color);
    }

    private TypanWarSide? GetLocalSide()
    {
        if (_players.LocalEntity is not { } local || !_ent.TryGetComponent<TypanWarFactionComponent>(local, out var faction))
            return null;

        return faction.Side;
    }

    private Vector2? GetLocalWorldPosition()
    {
        if (_players.LocalEntity is not { } local)
            return null;

        return _xform!.GetWorldPosition(local);
    }

    private static bool IsSamePosition(float ax, float ay, float bx, float by, float epsilon = 0.35f)
    {
        return Math.Abs(ax - bx) <= epsilon && Math.Abs(ay - by) <= epsilon;
    }

    private void DrawZoneMarker(DrawingHandleScreen handle, MapTransform map, TypanWarCaptureZoneStatus zone)
    {
        var color = zone.Owner switch
        {
            TypanWarCaptureOwner.Nanotrasen => Color.FromHex("#4A7FD4"),
            TypanWarCaptureOwner.Typan => Color.FromHex("#C84848"),
            _ => Color.FromHex("#D8D8D8"),
        };

        var center = map.WorldToScreen(zone.WorldX, zone.WorldY);
        handle.DrawCircle(center, 16f, Color.FromHex("#101018").WithAlpha(0.92f));
        handle.DrawCircle(center, 13f, color.WithAlpha(0.95f));

        var label = string.IsNullOrEmpty(zone.ZoneLabel) ? "?" : zone.ZoneLabel;
        var font = _zoneFont ?? _font!;
        var dims = handle.GetDimensions(font, label, 1f);
        var textPos = center - dims * 0.5f;
        handle.DrawString(font, textPos + new Vector2(1f, 1f), label, Color.FromHex("#101018"));
        handle.DrawString(font, textPos, label, Color.White);
    }

    private bool TryBuildTransform(UIBox2 box, out MapTransform map)
    {
        map = default;

        if (!_hasViewBounds)
            return false;

        const float padScreen = 6f;
        var inner = new UIBox2(
            box.Left + padScreen,
            box.Top + padScreen,
            box.Right - padScreen,
            box.Bottom - padScreen);

        var rangeX = Math.Max(_viewMaxX - _viewMinX, 1f);
        var rangeY = Math.Max(_viewMaxY - _viewMinY, 1f);
        var baseScale = Math.Min(inner.Width / rangeX, inner.Height / rangeY);
        var scale = baseScale * _zoom;
        var drawW = rangeX * scale;
        var drawH = rangeY * scale;
        var offsetX = inner.Left + (inner.Width - drawW) * 0.5f + _pan.X;
        var offsetY = inner.Top + (inner.Height - drawH) * 0.5f + _pan.Y;

        map = new MapTransform(_viewMinX, _viewMinY, _viewMaxX, _viewMaxY, scale, offsetX, offsetY);
        return true;
    }

    private readonly record struct CachedGridShape(
        EntityUid GridUid,
        GameTick LastBuild,
        List<Vector2> Vertices,
        int EdgeIndex,
        Color Fill,
        Color Edge,
        bool IsShuttle,
        TypanWarMinimapGridKind Kind,
        string Label,
        Vector2 CenterWorld);

    private readonly struct MapTransform
    {
        public readonly float MinX;
        public readonly float MinY;
        public readonly float Scale;
        public readonly float OffsetX;
        public readonly float OffsetY;
        public readonly float DrawHeight;

        public MapTransform(
            float minX,
            float minY,
            float maxX,
            float maxY,
            float scale,
            float offsetX,
            float offsetY)
        {
            MinX = minX;
            MinY = minY;
            Scale = scale;
            OffsetX = offsetX;
            OffsetY = offsetY;
            DrawHeight = (maxY - minY) * scale;
        }

        public Vector2 WorldToScreen(float worldX, float worldY)
        {
            var nx = (worldX - MinX) * Scale;
            var ny = (worldY - MinY) * Scale;
            return new Vector2(OffsetX + nx, OffsetY + DrawHeight - ny);
        }
    }

    private record struct GridDrawJob : IParallelRobustJob
    {
        public int BatchSize => 64;

        public MapTransform MapTransform;
        public List<Vector2> Vertices;
        public Vector2[] ScaledVertices;

        public void Execute(int index)
        {
            var vert = Vertices[index];
            ScaledVertices[index] = MapTransform.WorldToScreen(vert.X, vert.Y);
        }
    }
}
