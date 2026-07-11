using System.Linq;
using System.Numerics;
using Content.Server.Cargo.Components;
using Content.Server.Pinpointer;
using Content.Server.Station.Systems;
using Content.Shared._Mini.TypanWar;
using Content.Shared.Localizations;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Mini.TypanWar;

public sealed class SpawnedCaptureZone
{
    public required EntityUid ZoneUid;
    public required EntityUid FlagUid;
    public required string Label;
    public required string DisplayName;
    public required TypanWarCaptureOwner HomeFaction;
}

/// <summary>
/// Finds open 3×3 areas and spawns war capture zones with descriptive locations.
/// </summary>
public sealed class TypanWarCaptureZoneSpawnSystem : EntitySystem
{
    private const int MaxRandomAttempts = 500;
    private static readonly EntProtoId ZoneProto = "TypanWarCaptureZone";
    private static readonly EntProtoId FlagProto = "TypanWarCaptureFlag";

    private static readonly EntProtoId IndestructibleWallProto = "WallPlastitaniumIndestructible";

    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private readonly List<MapCoordinates> _usedWorldCenters = new();

    private sealed class SpawnTarget
    {
        public required EntityUid Grid;
        public EntityUid? Station;
        public required TypanWarCaptureOwner HomeFaction;
        public bool IsTradePost;
        public bool IsTypanTrade;
    }

    public bool TrySpawnWarZones(EntityUid ntStation, EntityUid typanStation, out List<SpawnedCaptureZone> spawnedZones)
    {
        spawnedZones = new List<SpawnedCaptureZone>();
        _usedWorldCenters.Clear();

        ClearExistingZones();

        if (!TryComp<StationDataComponent>(ntStation, out var ntData) ||
            !TryComp<StationDataComponent>(typanStation, out var typanData))
        {
            Log.Warning("Typan station war: cannot spawn capture zones — missing station data.");
            return false;
        }

        var ntGrid = _station.GetLargestGrid((ntStation, ntData));
        var typanGrid = _station.GetLargestGrid((typanStation, typanData));

        if (ntGrid == null || typanGrid == null)
        {
            Log.Warning("Typan station war: cannot spawn capture zones — missing station grids.");
            return false;
        }

        var typanTrade = FindTradeGridForStation(typanStation, typanGrid.Value);
        var ntTrade = FindTradeGridForStation(ntStation, ntGrid.Value);

        var tradeCandidates = new List<(EntityUid Grid, EntityUid Station, bool IsTypanTrade)>();
        if (ntTrade != null)
            tradeCandidates.Add((ntTrade.Value, ntStation, false));
        if (typanTrade != null)
            tradeCandidates.Add((typanTrade.Value, typanStation, true));

        var targets = new List<SpawnTarget>
        {
            new()
            {
                Grid = ntGrid.Value,
                Station = ntStation,
                HomeFaction = TypanWarCaptureOwner.Nanotrasen,
            },
            new()
            {
                Grid = typanGrid.Value,
                Station = typanStation,
                HomeFaction = TypanWarCaptureOwner.Typan,
            },
        };

        if (tradeCandidates.Count > 0)
        {
            var pick = _random.Pick(tradeCandidates);
            targets.Add(new SpawnTarget
            {
                Grid = pick.Grid,
                Station = pick.Station,
                HomeFaction = TypanWarCaptureOwner.Neutral,
                IsTradePost = true,
                IsTypanTrade = pick.IsTypanTrade,
            });
        }
        else
        {
            Log.Warning("Typan station war: no trade post grid found — spawning third zone on Typan station.");
            targets.Add(new SpawnTarget
            {
                Grid = typanGrid.Value,
                Station = typanStation,
                HomeFaction = TypanWarCaptureOwner.Neutral,
            });
        }

        var labels = new[] { "A", "B", "C" };
        for (var i = 0; i < labels.Length; i++)
        {
            var target = targets[i];
            if (!TrySpawnZone(target, labels[i], out var spawned))
            {
                Log.Warning($"Typan station war: failed to spawn capture zone {labels[i]} on grid {target.Grid}.");
                continue;
            }

            spawnedZones.Add(spawned);
        }

        return spawnedZones.Count > 0;
    }

    private void ClearExistingZones()
    {
        var query = EntityQueryEnumerator<TypanWarCaptureZoneComponent>();
        while (query.MoveNext(out var uid, out _))
            Del(uid);
    }

    private EntityUid? FindTradeGridForStation(EntityUid station, EntityUid mainGrid)
    {
        if (TryComp<StationDataComponent>(station, out var stationData))
        {
            foreach (var gridUid in stationData.Grids)
            {
                if (gridUid == mainGrid || !HasComp<TradeStationComponent>(gridUid))
                    continue;

                return gridUid;
            }
        }

        var query = EntityQueryEnumerator<TradeStationComponent>();
        while (query.MoveNext(out var gridUid, out _))
        {
            if (gridUid == mainGrid)
                continue;

            if (_station.GetOwningStation(gridUid) == station)
                return gridUid;
        }

        return null;
    }

    private bool TrySpawnZone(SpawnTarget target, string label, out SpawnedCaptureZone spawned)
    {
        spawned = null!;

        if (!TryComp<MapGridComponent>(target.Grid, out var grid))
            return false;

        if (!TryPickCenterTile(target, grid, out var centerTile))
            return false;

        var worldCenter = _transform.ToMapCoordinates(
            _map.GridTileToLocal(target.Grid, grid, centerTile));
        _usedWorldCenters.Add(worldCenter);

        var coords = _map.GridTileToLocal(target.Grid, grid, centerTile);
        var zoneUid = Spawn(ZoneProto, coords);

        if (!HasComp<TypanWarCaptureZoneComponent>(zoneUid))
            return false;

        var flag = Spawn(FlagProto, coords);

        spawned = new SpawnedCaptureZone
        {
            ZoneUid = zoneUid,
            FlagUid = flag,
            Label = label,
            DisplayName = BuildLocationName(target, centerTile),
            HomeFaction = target.HomeFaction,
        };

        return true;
    }

    private bool TryPickCenterTile(SpawnTarget target, MapGridComponent grid, out Vector2i center)
    {
        center = default;

        if (!TryComp<TransformComponent>(target.Grid, out var xform))
            return false;

        if (target.IsTradePost)
        {
            var bounds = grid.LocalAABB;
            var centerCandidate = new Vector2i(
                (int) ((bounds.Left + bounds.Right) * 0.5f),
                (int) ((bounds.Bottom + bounds.Top) * 0.5f));

            if (IsValidZoneCenter(target.Grid, grid, centerCandidate, new Vector2i(1, 1)))
            {
                center = centerCandidate;
                return true;
            }
        }

        return TryPickCenterTileRandom(target.Grid, grid, out center);
    }

    private bool TryPickCenterTileRandom(EntityUid gridUid, MapGridComponent grid, out Vector2i center)
    {
        center = default;

        if (!TryComp<TransformComponent>(gridUid, out var xform))
            return false;

        var bounds = grid.LocalAABB;
        var halfExtents = new Vector2i(1, 1);

        for (var attempt = 0; attempt < MaxRandomAttempts; attempt++)
        {
            var candidate = new Vector2i(
                _random.Next((int) bounds.Left + halfExtents.X, (int) bounds.Right - halfExtents.X),
                _random.Next((int) bounds.Bottom + halfExtents.Y, (int) bounds.Top - halfExtents.Y));

            if (IsTooClose(candidate, gridUid, grid))
                continue;

            if (!IsValidZoneCenter(gridUid, grid, candidate, halfExtents))
                continue;

            center = candidate;
            return true;
        }

        // Fallback: full scan
        foreach (var tileRef in _map.GetAllTiles(gridUid, grid))
        {
            var candidate = tileRef.GridIndices;
            if (IsTooClose(candidate, gridUid, grid))
                continue;

            if (!IsValidZoneCenter(gridUid, grid, candidate, halfExtents))
                continue;

            center = candidate;
            return true;
        }

        return false;
    }

    private bool IsTooClose(MapCoordinates candidate)
    {
        const float minDistance = 8f;
        var minDistanceSq = minDistance * minDistance;

        foreach (var used in _usedWorldCenters)
        {
            if ((used.Position - candidate.Position).LengthSquared() < minDistanceSq)
                return true;
        }

        return false;
    }

    private bool IsTooClose(Vector2i candidate, EntityUid gridUid, MapGridComponent grid)
    {
        var world = _transform.ToMapCoordinates(_map.GridTileToLocal(gridUid, grid, candidate));
        return IsTooClose(world);
    }

    private bool IsValidZoneCenter(EntityUid gridUid, MapGridComponent grid, Vector2i center, Vector2i halfExtents)
    {
        for (var dx = -halfExtents.X; dx <= halfExtents.X; dx++)
        {
            for (var dy = -halfExtents.Y; dy <= halfExtents.Y; dy++)
            {
                var indices = center + new Vector2i(dx, dy);

                if (!_map.TryGetTileRef(gridUid, grid, indices, out var tileRef))
                    return false;

                if (tileRef.Tile.IsEmpty || _turf.IsSpace(tileRef))
                    return false;

                var def = _turf.GetContentTileDefinition(tileRef);
                if (def.MapAtmosphere || def.IsSubFloor)
                    return false;

                if (_turf.IsTileBlocked(tileRef, CollisionGroup.MobMask))
                    return false;

                foreach (var ent in _map.GetAnchoredEntities(gridUid, grid, indices))
                {
                    if (HasComp<TypanWarCaptureZoneComponent>(ent) || HasComp<TypanWarCaptureFlagComponent>(ent))
                        return false;
                }
            }
        }

        if (IsNearIndestructibleWall(gridUid, grid, center, halfExtents))
            return false;

        return true;
    }

    /// <summary>
    /// Indestructible plastitanium barriers mark off-limits areas — zones must stay reachable.
    /// </summary>
    private bool IsNearIndestructibleWall(EntityUid gridUid, MapGridComponent grid, Vector2i center, Vector2i halfExtents)
    {
        const int margin = 1;

        for (var dx = -halfExtents.X - margin; dx <= halfExtents.X + margin; dx++)
        {
            for (var dy = -halfExtents.Y - margin; dy <= halfExtents.Y + margin; dy++)
            {
                var indices = center + new Vector2i(dx, dy);

                foreach (var ent in _map.GetAnchoredEntities(gridUid, grid, indices))
                {
                    if (MetaData(ent).EntityPrototype?.ID == IndestructibleWallProto.Id)
                        return true;
                }
            }
        }

        return false;
    }

    private string BuildLocationName(SpawnTarget target, Vector2i centerTile)
    {
        var mapCoords = _transform.ToMapCoordinates(
            _map.GridTileToLocal(target.Grid, Comp<MapGridComponent>(target.Grid), centerTile));

        var area = FormattedMessage.RemoveMarkupPermissive(
            _navMap.GetNearestBeaconString(mapCoords, onlyName: true));

        if (target.IsTradePost)
        {
            var tradeKey = target.IsTypanTrade
                ? "typan-war-capture-location-trade-typan"
                : "typan-war-capture-location-trade-nt";

            return Loc.GetString(tradeKey);
        }

        var stationName = target.Station != null
            ? Name(target.Station.Value)
            : Name(target.Grid);

        return Loc.GetString("typan-war-capture-location-station",
            ("station", stationName),
            ("area", area));
    }
}
