using System.Linq;
using System.Numerics;
using Content.Server.Cargo.Components;
using Content.Server.Station.Systems;
using Content.Shared._Mini.TypanWar;
using Content.Shared.Parallax;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Random;

namespace Content.Server._Mini.TypanWar;

/// <summary>
/// Merges NT and Typan station grids onto one map with a fixed tile offset and repositions trade posts.
/// </summary>
public sealed class TypanStationWarLayoutSystem : EntitySystem
{
    private const float TradePostMinDistance = 90f;
    private const int TradePostPlacementAttempts = 48;

    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TypanWarStartedEvent>(OnWarStarted);
    }

    private void OnWarStarted(TypanWarStartedEvent ev)
    {
        if (!TryComp<TypanStationWarRuleComponent>(ev.Rule, out var rule) || rule.LayoutApplied)
            return;

        if (!TryComp<StationDataComponent>(ev.NtStation, out var ntData) ||
            !TryComp<StationDataComponent>(ev.TypanStation, out var typanData))
        {
            Log.Warning("Typan station war layout: missing station data.");
            return;
        }

        var ntGrid = _station.GetLargestGrid((ev.NtStation, ntData));
        var typanGrid = _station.GetLargestGrid((ev.TypanStation, typanData));

        if (ntGrid == null || typanGrid == null)
        {
            Log.Warning("Typan station war layout: could not resolve largest station grids.");
            return;
        }

        if (!TryComp<TransformComponent>(ntGrid, out var ntXform) || ntXform.MapID == MapId.Nullspace)
        {
            Log.Warning("Typan station war layout: NT grid has no map.");
            return;
        }

        var ntMap = ntXform.MapID;
        var separation = new Vector2(rule.StationSeparationTiles, 0f);
        var ntCenter = _transform.GetWorldPosition(ntGrid.Value);
        var typanCenter = _transform.GetWorldPosition(typanGrid.Value);
        var offset = ntCenter + separation - typanCenter;

        foreach (var grid in typanData.Grids.ToList())
        {
            if (!TryComp<TransformComponent>(grid, out var gridXform))
                continue;

            var worldPos = _transform.GetWorldPosition(gridXform);
            _transform.SetMapCoordinates(grid, new MapCoordinates(worldPos + offset, ntMap));
        }

        RepositionTradeGrids(ev.NtStation, ntData, ntGrid.Value, rule.TradePostMaxDistanceTiles);
        RepositionTradeGrids(ev.TypanStation, typanData, typanGrid.Value, rule.TradePostMaxDistanceTiles);

        ApplyWarParallax(ntMap, rule.WarParallax);

        rule.LayoutApplied = true;
        Log.Info($"Typan station war layout: merged Typan grids onto map {ntMap} with {rule.StationSeparationTiles} tile offset.");

        RaiseLocalEvent(new TypanWarLayoutReadyEvent(ev.Rule, ev.NtStation, ev.TypanStation));
    }

    private void ApplyWarParallax(MapId mapId, string parallaxId)
    {
        if (string.IsNullOrWhiteSpace(parallaxId))
            return;

        var mapUid = _map.GetMap(mapId);
        EnsureComp<ParallaxComponent>(mapUid, out var parallax);
        parallax.Parallax = parallaxId;
        Dirty(mapUid, parallax);
    }

    private void RepositionTradeGrids(EntityUid station, StationDataComponent stationData, EntityUid anchorGrid, float maxDistance)
    {
        foreach (var grid in stationData.Grids)
        {
            if (grid == anchorGrid || !HasComp<TradeStationComponent>(grid))
                continue;

            if (!TryPlaceTradeGridNearStation(grid, anchorGrid, maxDistance))
                Log.Warning($"Typan station war layout: failed to reposition trade grid {grid} within {maxDistance}m of station {station}.");
        }
    }

    private bool TryPlaceTradeGridNearStation(EntityUid tradeGrid, EntityUid anchorGrid, float maxDistance)
    {
        if (!TryComp<MapGridComponent>(tradeGrid, out var tradeMapGrid) ||
            !TryComp<TransformComponent>(anchorGrid, out var anchorXform))
        {
            return false;
        }

        var anchorCenter = _transform.GetWorldPosition(anchorGrid);
        var tradeExtent = MathF.Max(tradeMapGrid.LocalAABB.Width, tradeMapGrid.LocalAABB.Height) * 0.5f;
        var minDistance = TradePostMinDistance + tradeExtent;
        var maxDist = maxDistance - tradeExtent;

        if (maxDist <= minDistance)
            maxDist = minDistance + 20f;

        var mapId = anchorXform.MapID;
        var localCenter = tradeMapGrid.LocalAABB.Center;
        var grids = new List<Entity<MapGridComponent>>();

        for (var attempt = 0; attempt < TradePostPlacementAttempts; attempt++)
        {
            var angle = _random.NextAngle();
            var distance = _random.NextFloat(minDistance, maxDist);
            var targetCenter = anchorCenter + angle.RotateVec(new Vector2(distance, 0f));
            var worldOrigin = targetCenter - localCenter;

            _transform.SetWorldPositionRotation(tradeGrid, worldOrigin, Angle.Zero);

            if (!IsWithinDistance(tradeGrid, tradeMapGrid, anchorCenter, maxDistance))
                continue;

            if (HasBlockingOverlap(tradeGrid, tradeMapGrid, mapId, grids))
                continue;

            return true;
        }

        foreach (var bearing in new[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f })
        {
            for (var distance = minDistance; distance <= maxDist; distance += 35f)
            {
                var targetCenter = anchorCenter + Angle.FromDegrees(bearing).RotateVec(new Vector2(distance, 0f));
                var worldOrigin = targetCenter - localCenter;

                _transform.SetWorldPositionRotation(tradeGrid, worldOrigin, Angle.Zero);

                if (!IsWithinDistance(tradeGrid, tradeMapGrid, anchorCenter, maxDistance))
                    continue;

                if (HasBlockingOverlap(tradeGrid, tradeMapGrid, mapId, grids))
                    continue;

                return true;
            }
        }

        return false;
    }

    private bool IsWithinDistance(EntityUid tradeGrid, MapGridComponent tradeMapGrid, Vector2 anchorCenter, float maxDistance)
    {
        var tradeAabb = _transform.GetWorldMatrix(tradeGrid).TransformBox(tradeMapGrid.LocalAABB);
        return Vector2.Distance(tradeAabb.Center, anchorCenter) <= maxDistance;
    }

    private bool HasBlockingOverlap(
        EntityUid tradeGrid,
        MapGridComponent tradeMapGrid,
        MapId mapId,
        List<Entity<MapGridComponent>> grids)
    {
        var tradeAabb = _transform.GetWorldMatrix(tradeGrid).TransformBox(tradeMapGrid.LocalAABB);

        grids.Clear();
        _mapManager.FindGridsIntersecting(mapId, tradeAabb, ref grids);

        foreach (var (uid, gridComp) in grids)
        {
            if (uid == tradeGrid)
                continue;

            var otherAabb = _transform.GetWorldMatrix(uid).TransformBox(gridComp.LocalAABB);
            var intersection = tradeAabb.Intersect(otherAabb);

            if (intersection.IsEmpty())
                continue;

            if (intersection.Width * intersection.Height > 4f)
                return true;
        }

        return false;
    }
}
