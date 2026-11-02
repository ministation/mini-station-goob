// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server._Lavaland.Procedural.Systems;
using Content.Server.Parallax;
using Content.Shared._Lavaland.Procedural.Components;
using Content.Shared._Lavaland.Procedural.Prototypes;
using Content.Shared.CCVar;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Procedural;

/// <summary>
/// Placement quality of Lavaland ruins: landmarks must end up on dry open ground
/// instead of inside a lava lake or a cave wall, and no two ruins may overlap.
/// </summary>
[TestFixture]
[TestOf(typeof(LavalandSystem))]
public sealed class LavalandPlacementTest
{
    private const float OverlapEpsilon = 0.01f;
    // Terrain reserved around every ruin by LavalandSystem, cleared of lava and rock walls.
    private const float ReservedPadding = 2f;
    // Ring outside the reserved padding that has to stay mostly open (a cave would have it walled in).
    private const float ApproachRing = ReservedPadding + 2f;
    private const float MinFreeApproach = 0.25f;

    [TestCase(1234)]
    [TestCase(987654)]
    public async Task RuinsOnDryOpenGroundAndNoOverlappingRuins(int seed)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var config = server.ResolveDependency<IConfigurationManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var lavaland = entMan.System<LavalandSystem>();

        var markers = protoMan.EnumeratePrototypes<LavalandMarkerRuinPrototype>().ToList();
        Assert.That(markers.Any(ruin => ruin.Guaranteed), Is.True, "there must be guaranteed marker ruins to test");

        EntityUid planet = default;
        try
        {
            await server.WaitPost(() =>
            {
                // The shared pool keeps lavaland off (it round-starts a planet); this test needs it.
                config.SetCVar(CCVars.LavalandEnabled, true);
                Assert.That(lavaland.LavalandEnabled, Is.True, "lavaland.enabled cvar must be on");

                lavaland.EnsurePreloaderMap();
                Assert.That(lavaland.GetPreloaderEntity(), Is.Not.Null, "preloader entity must exist");
                Assert.That(lavaland.SetupLavalandPlanet("Lavaland", out var map, seed: seed, withRuins: false),
                    Is.True, $"planet setup should succeed for seed {seed}");
                planet = map!.Value.Owner;

                // Drain the ruin batch exactly like GameTicker.MapPreload does: 200 ms passes.
                var done = false;
                while (!done)
                    done = lavaland.ProcessRuinsBatch(TimeSpan.FromMilliseconds(200));
            });

            await server.WaitRunTicks(5);

            await server.WaitAssertion(() => AssertPlacement(entMan, protoMan, markers, planet, seed));
        }
        catch (Exception ex)
        {
            TestContext.Progress.WriteLine($"Lavaland placement failed for seed {seed}: {ex}");
            throw;
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var preloader = lavaland.GetPreloaderEntity();
                if (preloader != null)
                    entMan.DeleteEntity(preloader.Value.Owner);
                if (planet.IsValid())
                    entMan.DeleteEntity(planet);
                config.SetCVar(CCVars.LavalandEnabled, false);
            });
            await server.WaitRunTicks(10);
        }

        await pair.CleanReturnAsync();
    }

    private static void AssertPlacement(
        IEntityManager entMan,
        IPrototypeManager protoMan,
        List<LavalandMarkerRuinPrototype> markers,
        EntityUid planet,
        int seed)
    {
        var xformQuery = entMan.GetEntityQuery<TransformComponent>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var xformSystem = entMan.System<SharedTransformSystem>();

        var grid = entMan.GetComponent<MapGridComponent>(planet);
        var pool = protoMan.Index<LavalandRuinPoolPrototype>("LavalandRuins");

        var spawnerRuins = new Dictionary<string, LavalandMarkerRuinPrototype>();
        foreach (var ruin in markers)
            spawnerRuins[ruin.SpawnedMarker.Id] = ruin;

        var spawners = new Dictionary<string, List<EntityUid>>();
        var spawnerQuery = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (spawnerQuery.MoveNext(out var uid, out var meta))
        {
            if (meta.EntityPrototype == null || entMan.Deleted(uid))
                continue;
            if (!spawnerRuins.ContainsKey(meta.EntityPrototype.ID))
                continue;
            if (!xformQuery.TryComp(uid, out var xform) || xform.MapUid != planet)
                continue;

            if (!spawners.TryGetValue(meta.EntityPrototype.ID, out var list))
                spawners[meta.EntityPrototype.ID] = list = new List<EntityUid>();
            list.Add(uid);
        }

        // Every guaranteed landmark from the pool must be on the planet.
        foreach (var ruin in markers.Where(ruin => ruin.Guaranteed))
        {
            spawners.TryGetValue(ruin.SpawnedMarker.Id, out var found);
            var expected = pool.MarkerRuins.TryGetValue(ruin.ID, out var count) ? (int) count : 0;
            Assert.That(found?.Count ?? 0, Is.EqualTo(expected),
                $"seed {seed}: expected {expected} of guaranteed marker ruin {ruin.ID} on the planet, found {found?.Count ?? 0}");
        }

        // Every placed landmark must sit on dry ground, clear of rock walls, with an open approach.
        var markerBoxes = new List<(string Name, Box2 Aabb)>();
        foreach (var (spawnerPrototype, spawnerList) in spawners)
        {
            var ruin = spawnerRuins[spawnerPrototype];
            var bounds = Box2.CentredAroundZero(ruin.Boundary);
            foreach (var spawner in spawnerList)
            {
                var markerPosition = xformSystem.GetWorldPosition(spawner);
                var box = bounds.Translated(markerPosition);
                markerBoxes.Add((ruin.ID, box));

                // No lava, chasm, rock wall or solid rock may be anchored in the reserved padding
                // around the ruin. The footprint itself is skipped: some TG ruins have their own
                // chasm or lava inside by design (the watcher grave is a chasm tomb).
                var reserved = box.Enlarged(ReservedPadding);
                AssertNoBlockingTerrain(entMan, mapSystem, xformSystem, planet, grid, box, reserved, seed, ruin.ID);
                AssertOpenApproach(entMan, mapSystem, planet, grid, reserved, seed, ruin.ID);
            }
        }

        // Ruin grids must not overlap each other (outpost grids and the planet itself are not ruins).
        var spawnedGrids = entMan.GetComponent<LavalandMapComponent>(planet).SpawnedGrids;
        var allBoxes = new List<(string Name, Box2 Aabb)>(markerBoxes);
        var gridQuery = entMan.EntityQueryEnumerator<MapGridComponent>();
        while (gridQuery.MoveNext(out var uid, out var ruinGrid))
        {
            if (uid == planet || spawnedGrids.Contains(uid))
                continue;
            if (!xformQuery.TryComp(uid, out var xform) || xform.MapUid != planet)
                continue;

            var aabb = xformSystem.GetWorldMatrix(xform).TransformBox(ruinGrid.LocalAABB);
            var meta = entMan.GetComponent<MetaDataComponent>(uid);
            allBoxes.Add((meta.EntityPrototype?.ID ?? uid.ToString(), aabb));
        }

        for (var i = 0; i < allBoxes.Count; i++)
        {
            for (var j = i + 1; j < allBoxes.Count; j++)
            {
                Assert.That(allBoxes[i].Aabb.Enlarged(-OverlapEpsilon).Intersects(allBoxes[j].Aabb.Enlarged(-OverlapEpsilon)),
                    Is.False,
                    $"seed {seed}: ruins {allBoxes[i].Name} and {allBoxes[j].Name} overlap");
            }
        }
    }

    private static void AssertNoBlockingTerrain(
        IEntityManager entMan,
        SharedMapSystem mapSystem,
        SharedTransformSystem xformSystem,
        EntityUid planet,
        MapGridComponent grid,
        Box2 footprint,
        Box2 reserved,
        int seed,
        string ruin)
    {
        for (var x = (int) MathF.Floor(reserved.Left); x < (int) MathF.Ceiling(reserved.Right); x++)
        {
            for (var y = (int) MathF.Floor(reserved.Bottom); y < (int) MathF.Ceiling(reserved.Top); y++)
            {
                var anchored = mapSystem.GetAnchoredEntitiesEnumerator(planet, grid, new Vector2i(x, y));
                while (anchored.MoveNext(out var anchoredUid))
                {
                    // The ruin's own builder may spawn a chasm inside its footprint by design
                    // (the watcher grave is a chasm tomb), so only the padding ring is checked.
                    if (footprint.Contains(xformSystem.GetWorldPosition(anchoredUid.Value)))
                        continue;

                    var anchoredMeta = entMan.GetComponent<MetaDataComponent>(anchoredUid.Value);
                    // Rock walls are not checked here: a neighbouring ruin may legitimately spawn
                    // its own wall right at the padding edge. Lava and chasms never belong there.
                    if (anchoredMeta.EntityPrototype == null || !IsLavaOrChasm(anchoredMeta.EntityPrototype.ID))
                        continue;

                    Assert.Fail($"seed {seed}: {anchoredMeta.EntityPrototype.ID} inside reserved area of ruin {ruin} at tile {x},{y}");
                }
            }
        }
    }

    private static void AssertOpenApproach(
        IEntityManager entMan,
        SharedMapSystem mapSystem,
        EntityUid planet,
        MapGridComponent grid,
        Box2 reserved,
        int seed,
        string ruin)
    {
        var ring = reserved.Enlarged(ApproachRing - ReservedPadding);
        var minX = (int) MathF.Floor(ring.Left);
        var maxX = (int) MathF.Ceiling(ring.Right);
        var minY = (int) MathF.Floor(ring.Bottom);
        var maxY = (int) MathF.Ceiling(ring.Top);

        var free = 0;
        var total = 0;
        void Check(Vector2i indices)
        {
            total++;
            var anchored = mapSystem.GetAnchoredEntitiesEnumerator(planet, grid, indices);
            var blocked = false;
            while (anchored.MoveNext(out var anchoredUid))
            {
                var anchoredMeta = entMan.GetComponent<MetaDataComponent>(anchoredUid.Value);
                if (anchoredMeta.EntityPrototype != null && IsBlockingTerrain(anchoredMeta.EntityPrototype.ID))
                {
                    blocked = true;
                    break;
                }
            }

            if (!blocked)
                free++;
        }

        for (var x = minX; x < maxX; x += 2)
        {
            Check(new Vector2i(x, minY));
            Check(new Vector2i(x, maxY - 1));
        }

        for (var y = minY; y < maxY; y += 2)
        {
            Check(new Vector2i(minX, y));
            Check(new Vector2i(maxX - 1, y));
        }

        Assert.That(total, Is.GreaterThan(0), $"seed {seed}: empty approach ring for ruin {ruin}");
        Assert.That((float) free / total, Is.GreaterThanOrEqualTo(MinFreeApproach),
            $"seed {seed}: ruin {ruin} looks walled in, only {free}/{total} approach tiles are open");
    }

    private static bool IsLavaOrChasm(string prototypeId)
    {
        return prototypeId is "FloorLavaEntity" or "FloorChasmEntity";
    }

    private static bool IsBlockingTerrain(string prototypeId)
    {
        return prototypeId is "FloorLavaEntity"
            or "FloorChasmEntity"
            or "WallRockBasalt"
            or "WallRockBasaltGibtonite"
            or "FloraRockSolid";
    }
}
