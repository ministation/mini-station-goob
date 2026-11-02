// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Server.Parallax;
using Content.Server.Procedural;
using Content.Shared._Lavaland.Procedural.Components;
using Content.Shared._Lavaland.Procedural.Prototypes;
using Content.Shared.Decals;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Lavaland.Procedural.Systems;

public sealed partial class LavalandSystem
{
    // Batched ruin placement state (see BeginRuinsBatch/ProcessRuinsBatch).
    private Entity<LavalandMapComponent>? _ruinLavaland;
    private Entity<LavalandPreloaderComponent>? _ruinPreloader;
    private Random? _ruinRandom;
    private List<Vector2i> _ruinCoords = new();
    private List<Box2> _ruinUsedSpace = new();
    private List<LavalandGridRuinPrototype>? _ruinGridList;
    private int _ruinGridIndex;
    private List<LavalandDungeonRuinPrototype>? _ruinDungeonList;
    private int _ruinDungeonIndex;
    private List<LavalandMarkerRuinPrototype>? _ruinMarkerList;
    private int _ruinMarkerIndex;
    private int _ruinPhase; // 0 idle, 1 guaranteed markers, 2 grid, 3 dungeon, 4 marker

    // Tile -> would the biome put lava or a chasm there. Cached for the current ruin batch.
    private readonly Dictionary<Vector2i, bool> _terrainBlocked = new();
    private readonly List<EntityUid> _ruinBlockers = new();

    private const float MaxBlockedTerrainFraction = 0.25f;
    private const int TerrainSamplesPerAxis = 6;
    private const float RuinPadding = 2f;

    /// <summary>
    /// Prepares the ruin placement plan for batched processing (same order and filters as
    /// <see cref="SetupRuins"/>). Call once after a withRuins:false planet setup.
    /// </summary>
    public void BeginRuinsBatch(LavalandRuinPoolPrototype? pool, Entity<LavalandMapComponent> lavaland, Entity<LavalandPreloaderComponent> preloader)
    {
        if (pool == null)
            return;

        _ruinLavaland = lavaland;
        _ruinPreloader = preloader;
        _ruinRandom = new Random(lavaland.Comp.Seed);
        _ruinUsedSpace = GetOutpostBoundary(lavaland);
        var coords = GetCoordinates(pool.RuinDistance, pool.MaxDistance, pool.MinDistance);
        _ruinRandom.Shuffle(coords);
        _ruinCoords = coords;

        _ruinGridList = GetGridRuinProtos(pool.GridRuins);
        _ruinGridList.Sort(CompareGridRuins);
        _ruinDungeonList = GetDungeonRuinProtos(pool.DungeonRuins);
        _ruinDungeonList.Sort((x, y) => x.Priority.CompareTo(y.Priority));
        _ruinMarkerList = GetMarkerRuinProtos(pool.MarkerRuins);
        _ruinMarkerList.Sort(CompareMarkerRuins);

        _terrainBlocked.Clear();
        _ruinGridIndex = 0;
        _ruinDungeonIndex = 0;
        _ruinMarkerIndex = 0;
        _ruinPhase = 1;

        Log.Debug($"Batched ruin setup on {ToPrettyString(lavaland)}: {_ruinGridList.Count} grid, {_ruinDungeonList.Count} dungeon, {_ruinMarkerList.Count} marker ruins.");
    }

    /// <summary>
    /// Places ruins until the time budget is spent or all are done.
    /// Returns true when the whole ruin plan has been placed (or nothing was queued).
    /// </summary>
    public bool ProcessRuinsBatch(TimeSpan budget)
    {
        if (_ruinPhase == 0)
            return true;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            switch (_ruinPhase)
            {
                // Guaranteed marker ruins go first so the landmarks get clean spots,
                // everything else is placed around them afterwards.
                case 1:
                    if (_ruinMarkerIndex >= _ruinMarkerList!.Count || !_ruinMarkerList[_ruinMarkerIndex].Guaranteed)
                    {
                        _ruinPhase = 2;
                        _ruinCoords = _ruinCoords!.Where(coord => !_ruinUsedSpace!.Any(usedBox => usedBox.Contains(coord))).ToList();
                        continue;
                    }

                    LoadMarkerRuin(_ruinMarkerList[_ruinMarkerIndex++], _ruinLavaland!.Value, ref _ruinUsedSpace, ref _ruinCoords);
                    break;
                case 2:
                    if (_ruinGridIndex >= _ruinGridList!.Count)
                    {
                        _ruinPhase = 3;
                        _ruinCoords = _ruinCoords!.Where(coord => !_ruinUsedSpace!.Any(usedBox => usedBox.Contains(coord))).ToList();
                        continue;
                    }

                    LoadGridRuin(_ruinGridList[_ruinGridIndex++], _ruinLavaland!.Value, _ruinPreloader!.Value, ref _ruinUsedSpace, ref _ruinCoords);
                    break;
                case 3:
                    if (_ruinDungeonIndex >= _ruinDungeonList!.Count)
                    {
                        _ruinPhase = 4;
                        _ruinCoords = _ruinCoords!.Where(coord => !_ruinUsedSpace!.Any(usedBox => usedBox.Contains(coord))).ToList();
                        continue;
                    }

                    LoadDungeonRuin(_ruinDungeonList[_ruinDungeonIndex++], _ruinLavaland!.Value, _ruinPreloader!.Value, _ruinRandom!, ref _ruinUsedSpace, ref _ruinCoords);
                    break;
                case 4:
                    if (_ruinMarkerIndex >= _ruinMarkerList!.Count)
                    {
                        MapInitLateRuins(_ruinLavaland!.Value);
                        CleanupRuinsBatch();
                        return true;
                    }

                    LoadMarkerRuin(_ruinMarkerList[_ruinMarkerIndex++], _ruinLavaland!.Value, ref _ruinUsedSpace, ref _ruinCoords);
                    break;
            }

            if (sw.Elapsed > budget)
                return false;
        }
    }

    /// <summary>
    /// Sorts grid ruins: guaranteed ones first (TG-style always_place), then by priority.
    /// </summary>
    private static int CompareGridRuins(LavalandGridRuinPrototype x, LavalandGridRuinPrototype y)
    {
        var guaranteed = y.Guaranteed.CompareTo(x.Guaranteed);
        return guaranteed != 0 ? guaranteed : x.Priority.CompareTo(y.Priority);
    }

    /// <summary>
    /// Sorts marker ruins: guaranteed ones first (TG-style always_place), then by priority.
    /// </summary>
    private static int CompareMarkerRuins(LavalandMarkerRuinPrototype x, LavalandMarkerRuinPrototype y)
    {
        var guaranteed = y.Guaranteed.CompareTo(x.Guaranteed);
        return guaranteed != 0 ? guaranteed : x.Priority.CompareTo(y.Priority);
    }

    /// <summary>
    /// The staged-preload path places ruins after the planet map was already initialized,
    /// so MapInit never fires for grid-ruin contents (spawners stay dead, mobs get no AI).
    /// The sync path initializes them together with the planet instead. Raise MapInit
    /// manually for everything that arrived late - same end state as the sync path.
    /// </summary>
    private void MapInitLateRuins(Entity<LavalandMapComponent> lavaland)
    {
        if (!_map.IsInitialized(lavaland))
            return;

        var mapUid = lavaland.Owner;
        var query = EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        var toInit = new List<EntityUid>();
        while (query.MoveNext(out var uid, out var meta, out var xform))
        {
            if (meta.EntityLifeStage >= EntityLifeStage.MapInitialized)
                continue;
            if (TerminatingOrDeleted(uid) || xform.MapUid != mapUid)
                continue;
            toInit.Add(uid);
        }

        var metaQuery = EntityManager.GetEntityQuery<MetaDataComponent>();
        foreach (var uid in toInit)
        {
            if (!metaQuery.TryComp(uid, out var meta))
                continue;
            if (meta.EntityLifeStage != EntityLifeStage.Initialized)
                continue;
            EntityManager.RunMapInit(uid, meta);
        }

        Log.Debug($"Map-initialized late ruin contents on {ToPrettyString(lavaland)}: {toInit.Count} entities.");
    }

    private void CleanupRuinsBatch()
    {
        _ruinLavaland = null;
        _ruinPreloader = null;
        _ruinRandom = null;
        _ruinCoords.Clear();
        _ruinUsedSpace.Clear();
        _ruinGridList = null;
        _ruinDungeonList = null;
        _ruinMarkerList = null;
        _ruinGridIndex = 0;
        _ruinDungeonIndex = 0;
        _ruinMarkerIndex = 0;
        _terrainBlocked.Clear();
        _ruinPhase = 0;
    }

    private void SetupRuins(LavalandRuinPoolPrototype? pool, Entity<LavalandMapComponent> lavaland, Entity<LavalandPreloaderComponent> preloader)
    {
        if (pool == null)
            return;

        var random = new Random(lavaland.Comp.Seed);
        _terrainBlocked.Clear();

        var usedSpace = GetOutpostBoundary(lavaland);
        var coords = GetCoordinates(pool.RuinDistance, pool.MaxDistance, pool.MinDistance);

        random.Shuffle(coords);

        // Guaranteed marker ruins first: landmarks get the cleanest spots, everything else avoids them.
        SetupMarkerRuins(pool.MarkerRuins, lavaland, ref coords, ref usedSpace, guaranteed: true);

        coords = coords.Where(coord => !usedSpace.Any(usedBox => usedBox.Contains(coord))).ToList();

        SetupGridRuins(pool.GridRuins, lavaland, preloader, ref coords, ref usedSpace);

        // Create a new list that excludes all already used spaces that intersect with big ruins.
        coords = coords.Where(coord => !usedSpace.Any(usedBox => usedBox.Contains(coord))).ToList();

        SetupDungeonRuins(pool.DungeonRuins, lavaland, preloader, random, ref coords, ref usedSpace);

        // Do that again
        coords = coords.Where(coord => !usedSpace.Any(usedBox => usedBox.Contains(coord))).ToList();

        SetupMarkerRuins(pool.MarkerRuins, lavaland, ref coords, ref usedSpace, guaranteed: false);
    }

    private void SetupGridRuins(
        Dictionary<ProtoId<LavalandGridRuinPrototype>, ushort> ruins,
        Entity<LavalandMapComponent> lavaland,
        Entity<LavalandPreloaderComponent> preloader,
        ref List<Vector2i> coords,
        ref List<Box2> usedSpace)
    {
        // Get and sort all ruins, because we can't sort dictionaries
        var list = GetGridRuinProtos(ruins);
        list.Sort(CompareGridRuins);

        Log.Debug($"Spawning {list.Count} Grid Ruins on {ToPrettyString(lavaland)} planet.");
        foreach (var ruin in list)
        {
            LoadGridRuin(ruin, lavaland, preloader, ref usedSpace, ref coords);
        }
    }

    private void SetupDungeonRuins(
        Dictionary<ProtoId<LavalandDungeonRuinPrototype>, ushort> ruins,
        Entity<LavalandMapComponent> lavaland,
        Entity<LavalandPreloaderComponent> preloader,
        Random random,
        ref List<Vector2i> coords,
        ref List<Box2> usedSpace)
    {
        // Get and sort all ruins, because we can't sort dictionaries
        var list = GetDungeonRuinProtos(ruins);
        list.Sort((x, y) => x.Priority.CompareTo(y.Priority));

        Log.Debug($"Spawning {list.Count} Dungeon Ruins on {ToPrettyString(lavaland)} planet.");
        foreach (var ruin in list)
        {
            LoadDungeonRuin(ruin, lavaland, preloader, random, ref usedSpace, ref coords);
        }
    }

    private void SetupMarkerRuins(
        Dictionary<ProtoId<LavalandMarkerRuinPrototype>, ushort> ruins,
        Entity<LavalandMapComponent> lavaland,
        ref List<Vector2i> coords,
        ref List<Box2> usedSpace,
        bool guaranteed)
    {
        // Get and sort all ruins, because we can't sort dictionaries
        var list = GetMarkerRuinProtos(ruins);
        list.Sort(CompareMarkerRuins);

        var count = 0;
        foreach (var ruin in list)
        {
            if (ruin.Guaranteed != guaranteed)
                continue;

            LoadMarkerRuin(ruin, lavaland, ref usedSpace, ref coords);
            count++;
        }

        Log.Debug($"Spawning {count} Marker Ruins (guaranteed: {guaranteed}) on {ToPrettyString(lavaland)} planet.");
    }

    private List<Box2> GetOutpostBoundary(Entity<LavalandMapComponent> lavaland)
    {
        var boundary = new List<Box2>();

        foreach (var uid in lavaland.Comp.SpawnedGrids)
        {
            if (!_xformQuery.TryComp(uid, out var xform)
                || !_fixtureQuery.TryComp(uid, out var fixtureComp)
                || xform.MapUid != lavaland)
                continue;

            var aabbs = new Box2();

            var transform = _physics.GetRelativePhysicsTransform((uid, xform), xform.MapUid.Value);
            foreach (var fixture in fixtureComp.Fixtures.Values)
            {
                if (!fixture.Hard)
                    continue;

                var aabb = fixture.Shape.ComputeAABB(transform, 0);
                aabbs = aabbs.Union(aabb);
            }

            aabbs = aabbs.Enlarged(8f);
            boundary.Add(aabbs);
        }

        return boundary;
    }

    private void LoadGridRuin(
        LavalandGridRuinPrototype ruin,
        Entity<LavalandMapComponent> lavaland,
        Entity<LavalandPreloaderComponent> preloader,
        ref List<Box2> usedSpace,
        ref List<Vector2i> coords)
    {
        if (coords.Count == 0)
            return;

        var mapXform = Transform(preloader);

        if (!_mapLoader.TryLoadGrid(mapXform.MapID, ruin.Path, out var spawnedBoundedGrid))
        {
            Log.Error($"Failed to load ruin {ruin.ID} onto dummy map, on stage of loading! AAAAA!!");
            return;
        }

        var spawned = spawnedBoundedGrid.Value;
        var ruinBox = spawned.Comp.LocalAABB;

        // Guaranteed ruins scan every remaining lattice point instead of overlapping something.
        var attempts = ruin.Guaranteed ? coords.Count : ruin.SpawnAttempts;
        // Fall back to a full scan (still no overlapping) so a bad random streak never drops the ruin.
        if (!TryPlaceRuin(ruinBox, attempts, ruin.MinDistance, ruin.MaxDistance, lavaland, coords, usedSpace, out var coord)
            && !TryPlaceRuin(ruinBox, coords.Count, ruin.MinDistance, ruin.MaxDistance, lavaland, coords, usedSpace, out coord))
        {
            Log.Warning($"Failed to load ruin {ruin.ID} on {ToPrettyString(lavaland)} planet's surface: all attempts have failed and no eligible spot was found!");
            Del(spawnedBoundedGrid);
            return;
        }

        usedSpace.Add(ruinBox.Translated(coord.Value));
        // Purge every lattice point covered by the placed ruin, not just its center: later ruins
        // only sample the first few entries of the list, so leftover points inside occupied boxes
        // would make them burn all spawn attempts and fail to place (seen on MiningPost/Generator).
        coords.RemoveAll(c => ruinBox.Translated(coord.Value).Contains(new Vector2(c.X, c.Y)));

        // Clear the biome before the ruin lands there, so ruin-owned rock walls are not deleted.
        ReserveRuinTerrain(lavaland, ruinBox.Translated(coord.Value));

        // Teleport it into place on preloader map
        _transform.SetCoordinates(spawned, new EntityCoordinates(preloader, coord.Value));

        if (ruin.PatchToPlanet)
            PatchToPlanet(spawned, (lavaland.Owner, _gridQuery.Comp(lavaland.Owner)), coord.Value);
        else
        {
            var spawnedXForm = _xformQuery.GetComponent(spawned);
            _metaData.SetEntityName(spawned, Loc.GetString(ruin.Name));
            _transform.SetParent(spawned, spawnedXForm, lavaland);
            _transform.SetCoordinates(spawned,
                new EntityCoordinates(lavaland, spawnedXForm.Coordinates.Position.Rounded()));

            var componentsToGrant = EnsureComp<LavalandGridGrantComponent>(spawned);
            foreach (var (key, comp) in ruin.ComponentsToGrant)
            {
                componentsToGrant.ComponentsToGrant[key] = comp;
            }
        }

        Log.Debug($"Successfully spawned ruin {ruin.ID} on {ToPrettyString(lavaland)} planet surface at coordinates {coord.ToString()}");
    }

    private void LoadDungeonRuin(
        LavalandDungeonRuinPrototype ruin,
        Entity<LavalandMapComponent> lavaland,
        Entity<LavalandPreloaderComponent> preloader,
        Random random,
        ref List<Box2> usedSpace,
        ref List<Vector2i> coords)
    {
        if (coords.Count == 0)
            return;

        var pos = random.Pick(coords);
        _dungeon.GenerateDungeon(_proto.Index(ruin.Config),
            lavaland.Owner,
            Comp<MapGridComponent>(lavaland),
            pos,
            lavaland.Comp.Seed);
    }

    private void LoadMarkerRuin(
        LavalandMarkerRuinPrototype ruin,
        Entity<LavalandMapComponent> lavaland,
        ref List<Box2> usedSpace,
        ref List<Vector2i> coords)
    {
        if (coords.Count == 0)
            return;

        var ruinBox = Box2.CentredAroundZero(ruin.Boundary);

        // Guaranteed ruins scan every remaining lattice point instead of overlapping something.
        var attempts = ruin.Guaranteed ? coords.Count : ruin.SpawnAttempts;
        // Fall back to a full scan (still no overlapping) so a bad random streak never drops the ruin.
        if (!TryPlaceRuin(ruinBox, attempts, ruin.MinDistance, ruin.MaxDistance, lavaland, coords, usedSpace, out var coord)
            && !TryPlaceRuin(ruinBox, coords.Count, ruin.MinDistance, ruin.MaxDistance, lavaland, coords, usedSpace, out coord))
        {
            Log.Warning($"Failed to load ruin {ruin.ID} on {ToPrettyString(lavaland)} planet's surface: all attempts have failed and no eligible spot was found!");
            return;
        }

        // Clear the biome before the marker spawns its structure, so its own rock walls are not deleted.
        ReserveRuinTerrain(lavaland, ruinBox.Translated(coord.Value));

        Spawn(ruin.SpawnedMarker, new EntityCoordinates(lavaland, coord.Value));

        usedSpace.Add(ruinBox.Translated(coord.Value));
        // Same as in LoadGridRuin: purge all covered lattice points, not just the center.
        coords.RemoveAll(c => ruinBox.Translated(coord.Value).Contains(new Vector2(c.X, c.Y)));
    }

    /// <summary>
    /// Tries to find a spot on a surface of a planet where a ruin can fit on solid ground
    /// without intersecting (or visually touching) other ruins.
    /// </summary>
    /// <param name="ruinBox">Boundaries of a ruin, confined inside a box</param>
    /// <param name="spawnAttempts">The maximum amount of spawn attempts we make before giving up on generating that ruin.</param>
    /// <param name="minDistance">Optional override of the pool's min distance from the map center, for this specific ruin.</param>
    /// <param name="maxDistance">Optional override of the pool's max distance from the map center, for this specific ruin.</param>
    /// <param name="lavaland">The planet the ruin is placed on, used for terrain checks.</param>
    /// <param name="coords">A list of all eligible planet coordinates. Has to be shuffled to be as efficient as possible.</param>
    /// <param name="usedSpace">A list of bounding boxes of already placed ruins, translated relative to their position.</param>
    /// <param name="pickedCoord">Picked coordinates that we can safely put a ruin at.</param>
    /// <returns></returns>
    private bool TryPlaceRuin(
        Box2 ruinBox,
        int spawnAttempts,
        int? minDistance,
        int? maxDistance,
        Entity<LavalandMapComponent> lavaland,
        List<Vector2i> coords,
        List<Box2> usedSpace,
        [NotNullWhen(true)] out Vector2i? pickedCoord)
    {
        pickedCoord = null;
        for (var i = 0; i < coords.Count && i < spawnAttempts; i++)
        {
            var pos = coords[i];

            // Ruin can override the pool's distance band from the map center.
            var distance = MathF.Sqrt(pos.X * pos.X + pos.Y * pos.Y);
            if (distance < minDistance || distance > maxDistance)
                continue;

            // Pad the candidate so neighboring buildings never visually touch.
            var translated = ruinBox.Enlarged(RuinPadding).Translated(pos);
            var intersects = usedSpace.Any(used => translated.Intersects(used));
            if (intersects)
                continue;

            if (!IsTerrainSuitable(lavaland, ruinBox, pos))
                continue;

            pickedCoord = pos;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Rejects spots where the biome would put lava or a chasm under the ruin,
    /// so landmarks do not end up in the middle of a lava lake or over a hole.
    /// </summary>
    private bool IsTerrainSuitable(Entity<LavalandMapComponent> lavaland, Box2 ruinBox, Vector2i pos)
    {
        var grid = Comp<MapGridComponent>(lavaland.Owner);
        var biome = Comp<BiomeComponent>(lavaland.Owner);

        var box = ruinBox.Translated(pos);
        if (IsTerrainBlocked(lavaland, grid, biome, box.Center.Floored()))
            return false;

        var minX = (int) MathF.Floor(box.Left);
        var minY = (int) MathF.Floor(box.Bottom);
        var maxX = (int) MathF.Ceiling(box.Right);
        var maxY = (int) MathF.Ceiling(box.Top);

        // Sampled on purpose: the full biome prediction is too slow to run for every tile
        // of every candidate during the round-start batch, and a lava lake is many tiles wide.
        var stepX = Math.Max(2, (maxX - minX) / TerrainSamplesPerAxis);
        var stepY = Math.Max(2, (maxY - minY) / TerrainSamplesPerAxis);
        var blocked = 0;
        var total = 0;
        for (var x = minX; x < maxX; x += stepX)
        {
            for (var y = minY; y < maxY; y += stepY)
            {
                total++;
                if (IsTerrainBlocked(lavaland, grid, biome, new Vector2i(x, y)))
                    blocked++;
            }
        }

        return total > 0 && blocked < total * MaxBlockedTerrainFraction;
    }

    /// <summary>
    /// True if the tile is, or would be, covered by lava or a chasm.
    /// </summary>
    private bool IsTerrainBlocked(
        Entity<LavalandMapComponent> lavaland,
        MapGridComponent grid,
        BiomeComponent biome,
        Vector2i indices)
    {
        if (_terrainBlocked.TryGetValue(indices, out var blocked))
            return blocked;

        blocked = false;

        // Terrain may already exist if the map was loaded the non-batched way.
        var anchored = _map.GetAnchoredEntitiesEnumerator(lavaland.Owner, grid, indices);
        while (anchored.MoveNext(out var uid))
        {
            var prototype = MetaData(uid.Value).EntityPrototype;
            if (prototype == null || !IsBlockingTerrain(prototype.ID))
                continue;

            blocked = true;
            break;
        }

        // The staged preload path has no biome chunks yet, so ask the biome what it would spawn here.
        if (!blocked
            && _biome.TryGetEntity(indices, biome, (lavaland.Owner, grid), out var terrain)
            && terrain != null
            && IsBlockingTerrain(terrain))
        {
            blocked = true;
        }

        _terrainBlocked[indices] = blocked;
        return blocked;
    }

    private static bool IsBlockingTerrain(string prototypeId)
    {
        return prototypeId is "FloorLavaEntity" or "FloorChasmEntity";
    }

    /// <summary>
    /// Clears biome terrain from the ruin footprint: deletes blockers that were already loaded,
    /// and reserves the tiles so a later chunk load never spawns lava, rock, ores or decals there.
    /// </summary>
    private void ReserveRuinTerrain(Entity<LavalandMapComponent> lavaland, Box2 worldBox)
    {
        var grid = Comp<MapGridComponent>(lavaland.Owner);
        var minX = (int) MathF.Floor(worldBox.Left);
        var minY = (int) MathF.Floor(worldBox.Bottom);
        var maxX = (int) MathF.Ceiling(worldBox.Right);
        var maxY = (int) MathF.Ceiling(worldBox.Top);

        // The chunk may already be loaded, in which case the biome has spawned its entities here.
        for (var x = minX; x < maxX; x++)
        {
            for (var y = minY; y < maxY; y++)
            {
                var anchored = _map.GetAnchoredEntitiesEnumerator(lavaland.Owner, grid, new Vector2i(x, y));
                while (anchored.MoveNext(out var uid))
                {
                    var prototype = MetaData(uid.Value).EntityPrototype;
                    if (prototype != null && IsBiomeBlocker(prototype.ID))
                        _ruinBlockers.Add(uid.Value);
                }
            }
        }

        foreach (var uid in _ruinBlockers)
        {
            if (!Deleted(uid))
                Del(uid);
        }

        _ruinBlockers.Clear();

        _tiles.Clear();
        _biome.ReserveTiles(lavaland.Owner, worldBox, _tiles);
        _tiles.Clear();
    }

    /// <summary>
    /// Surface clutter the biome spawns, which must never stay inside a ruin.
    /// </summary>
    private static bool IsBiomeBlocker(string prototypeId)
    {
        return prototypeId is "FloorLavaEntity"
            or "FloorChasmEntity"
            or "WallRockBasalt"
            or "FloraRockSolid"
            or "FruitCactus"
            or "BasaltOne"
            or "BasaltTwo"
            or "BasaltThree"
            or "BasaltFour"
            or "BasaltFive";
    }

    /// <summary>
    /// Builds a regular grid of positions, keeping only the points inside the [minDistance, maxDistance].
    /// </summary>
    private List<Vector2i> GetCoordinates(int distance, int maxDistance, int minDistance = 0)
    {
        var coords = new List<Vector2i>();

        for (var y = -maxDistance; y <= maxDistance; y += distance)
        {
            for (var x = -maxDistance; x <= maxDistance; x += distance)
            {
                var length = MathF.Sqrt(x * x + y * y);
                if (length >= minDistance && length <= maxDistance)
                    coords.Add(new Vector2i(x, y));
            }
        }

        return coords;
    }

    private List<LavalandGridRuinPrototype> GetGridRuinProtos(Dictionary<ProtoId<LavalandGridRuinPrototype>, ushort> protos)
    {
        var list = new List<LavalandGridRuinPrototype>();

        foreach (var (protoId, count) in protos)
        {
            var proto = _proto.Index(protoId);
            for (var i = 0; i < count; i++)
            {
                list.Add(proto);
            }
        }

        return list;
    }

    private List<LavalandDungeonRuinPrototype> GetDungeonRuinProtos(Dictionary<ProtoId<LavalandDungeonRuinPrototype>, ushort> protos)
    {
        var list = new List<LavalandDungeonRuinPrototype>();
        foreach (var (protoId, count) in protos)
        {
            var proto = _proto.Index(protoId);
            for (var i = 0; i < count; i++)
            {
                list.Add(proto);
            }
        }

        return list;
    }

    private List<LavalandMarkerRuinPrototype> GetMarkerRuinProtos(Dictionary<ProtoId<LavalandMarkerRuinPrototype>, ushort> protos)
    {
        var list = new List<LavalandMarkerRuinPrototype>();
        foreach (var (protoId, count) in protos)
        {
            var proto = _proto.Index(protoId);
            for (var i = 0; i < count; i++)
            {
                list.Add(proto);
            }
        }

        return list;
    }

    private readonly List<(Vector2i, Tile)> _tiles = new();

    /// <summary>
    /// Tricky and sneaky method that patches some grid to a lavaland planet
    /// by teleporting all entities and copying all tiles & decals.
    /// Of course, it deletes the grid right after.
    /// </summary>
    private void PatchToPlanet(
        Entity<MapGridComponent> grid,
        Entity<MapGridComponent> lavaland,
        Vector2i offset,
        HashSet<Vector2i>? reservedTiles = null)
    {
        var box = grid.Comp.LocalAABB.Translated(offset);
        var center = box.Center;
        var roomTransform = Matrix3Helpers.CreateTranslation(center.X, center.Y);

        // Copy all tiles
        _tiles.Clear();

        var tiles = _map.GetAllTiles(grid.Owner, grid.Comp).ToList();
        foreach (var tileRef in tiles)
        {
            _tiles.Add((tileRef.GridIndices + offset, tileRef.Tile));
        }

        _map.SetTiles(lavaland.Owner, lavaland.Comp, _tiles);

        // Teleport all entities
        var ents = new HashSet<Entity<TransformComponent>>();
        _lookup.GetChildEntities(grid, ents);
        foreach (var (teleportEnt, xform) in ents)
        {
            var anchored = xform.Anchored;
            var newPos = new EntityCoordinates(lavaland.Owner, xform.LocalPosition + offset);
            _transform.SetParent(teleportEnt, lavaland);
            _transform.SetCoordinates(teleportEnt, newPos);

            if (anchored)
                _transform.AnchorEntity(teleportEnt);
        }

        // Spawn decals
        if (TryComp<DecalGridComponent>(grid.Owner, out var loadedDecals))
        {
            EnsureComp<DecalGridComponent>(lavaland);
            foreach (var (_, decal) in _decals.GetDecalsIntersecting(grid.Owner, box, loadedDecals))
            {
                // Offset by 0.5 because decals are offset from bot-left corner
                // So we convert it to center of tile then convert it back again after transform.
                // Do these shenanigans because 32x32 decals assume as they are centered on bottom-left of tiles.
                var position = Vector2.Transform(decal.Coordinates + grid.Comp.TileSizeHalfVector - center,
                    roomTransform);
                position -= grid.Comp.TileSizeHalfVector;

                if (reservedTiles?.Contains(position.Floored()) == true)
                    continue;

                // Umm uhh I love decals so uhhhh idk what to do about this
                var angle = decal.Angle.Reduced();

                // Adjust because 32x32 so we can't rotate cleanly
                // Yeah idk about the uhh vectors here but it looked visually okay but they may still be off by 1.
                // Also EyeManager.PixelsPerMeter should really be in shared.
                if (angle.Equals(Math.PI))
                {
                    position += new Vector2(-1f / 32f, 1f / 32f);
                }
                else if (angle.Equals(-Math.PI / 2f))
                {
                    position += new Vector2(-1f / 32f, 0f);
                }
                else if (angle.Equals(Math.PI / 2f))
                {
                    position += new Vector2(0f, 1f / 32f);
                }
                else if (angle.Equals(Math.PI * 1.5f))
                {
                    // I hate this but decals are bottom-left rather than center position and doing the
                    // matrix ops is a PITA hence this workaround for now; I also don't want to add a stupid
                    // field for 1 specific op on decals
                    if (decal.Id != "DiagonalCheckerAOverlay" &&
                        decal.Id != "DiagonalCheckerBOverlay")
                    {
                        position += new Vector2(-1f / 32f, 0f);
                    }
                }

                var tilePos = position.Floored();

                // Fallback because uhhhhhhhh yeah, a corner tile might look valid on the original
                // but place 1 nanometre off grid and fail the add.
                if (!_map.TryGetTileRef(grid, grid, tilePos, out var tileRef) || tileRef.Tile.IsEmpty)
                {
                    _map.SetTile(grid,
                        grid,
                        tilePos,
                        _tile.GetVariantTile((ContentTileDefinition) _tiledef[DungeonSystem.FallbackTileId],
                            _random.GetRandom()));
                }

                _decals.TryAddDecal(
                    decal.Id,
                    new EntityCoordinates(grid, position),
                    out _,
                    decal.Color,
                    angle,
                    decal.ZIndex,
                    decal.Cleanable);
            }
        }

        Del(grid);
    }
}
