// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
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
/// Placement quality of Lavaland ruins: guaranteed landmarks must end up on solid
/// ground instead of inside a lava lake, and ruin grids must not overlap each other.
/// </summary>
[TestFixture]
[TestOf(typeof(LavalandSystem))]
public sealed class LavalandPlacementTest
{
    private const string LavaPrototype = "FloorLavaEntity";
    private const float OverlapEpsilon = 0.01f;

    [TestCase(1234)]
    [TestCase(987654)]
    public async Task GuaranteedRuinsOnSolidGroundAndNoOverlappingGrids(int seed)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var config = server.ResolveDependency<IConfigurationManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var lavaland = entMan.System<LavalandSystem>();

        var guaranteedMarkers = protoMan.EnumeratePrototypes<LavalandMarkerRuinPrototype>()
            .Where(ruin => ruin.Guaranteed)
            .ToList();
        Assert.That(guaranteedMarkers, Is.Not.Empty, "there must be guaranteed marker ruins to test");

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

            await server.WaitAssertion(() => AssertPlacement(entMan, protoMan, guaranteedMarkers, planet, seed));
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
        List<LavalandMarkerRuinPrototype> guaranteedMarkers,
        EntityUid planet,
        int seed)
    {
        var xformQuery = entMan.GetEntityQuery<TransformComponent>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var xformSystem = entMan.System<SharedTransformSystem>();

        var grid = entMan.GetComponent<MapGridComponent>(planet);
        var pool = protoMan.Index<LavalandRuinPoolPrototype>("LavalandRuins");

        var spawnerRuins = new Dictionary<string, LavalandMarkerRuinPrototype>();
        foreach (var ruin in guaranteedMarkers)
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
        foreach (var ruin in guaranteedMarkers)
        {
            spawners.TryGetValue(ruin.SpawnedMarker.Id, out var found);
            var expected = pool.MarkerRuins.TryGetValue(ruin.ID, out var count) ? (int) count : 0;
            Assert.That(found?.Count ?? 0, Is.EqualTo(expected),
                $"seed {seed}: expected {expected} of guaranteed marker ruin {ruin.ID} on the planet, found {found?.Count ?? 0}");
            if (found == null)
                continue;

            var bounds = Box2.CentredAroundZero(ruin.Boundary);
            foreach (var spawner in found)
            {
                var position = xformSystem.GetWorldPosition(spawner);
                var box = bounds.Translated(position);

                // Hard check: no lava or chasm may be anchored inside the landmark footprint.
                for (var x = (int) MathF.Floor(box.Left); x < (int) MathF.Ceiling(box.Right); x++)
                {
                    for (var y = (int) MathF.Floor(box.Bottom); y < (int) MathF.Ceiling(box.Top); y++)
                    {
                        var anchored = mapSystem.GetAnchoredEntitiesEnumerator(planet, grid, new Vector2i(x, y));
                        while (anchored.MoveNext(out var anchoredUid))
                        {
                            var anchoredMeta = entMan.GetComponent<MetaDataComponent>(anchoredUid.Value);
                            if (anchoredMeta.EntityPrototype == null || !IsLavaOrChasm(anchoredMeta.EntityPrototype.ID))
                                continue;

                            Assert.Fail($"seed {seed}: {anchoredMeta.EntityPrototype.ID} inside guaranteed marker ruin {ruin.ID}");
                        }
                    }
                }
            }
        }

        // Ruin grids must not overlap each other (outpost grids and the planet itself are not ruins).
        var spawnedGrids = entMan.GetComponent<LavalandMapComponent>(planet).SpawnedGrids;
        var ruinGrids = new List<(string Name, Box2 Aabb)>();
        var gridQuery = entMan.EntityQueryEnumerator<MapGridComponent>();
        while (gridQuery.MoveNext(out var uid, out var ruinGrid))
        {
            if (uid == planet || spawnedGrids.Contains(uid))
                continue;
            if (!xformQuery.TryComp(uid, out var xform) || xform.MapUid != planet)
                continue;

            var aabb = xformSystem.GetWorldMatrix(xform).TransformBox(ruinGrid.LocalAABB);
            var meta = entMan.GetComponent<MetaDataComponent>(uid);
            ruinGrids.Add((meta.EntityPrototype?.ID ?? uid.ToString(), aabb));
        }

        for (var i = 0; i < ruinGrids.Count; i++)
        {
            for (var j = i + 1; j < ruinGrids.Count; j++)
            {
                Assert.That(ruinGrids[i].Aabb.Enlarged(-OverlapEpsilon).Intersects(ruinGrids[j].Aabb.Enlarged(-OverlapEpsilon)),
                    Is.False,
                    $"seed {seed}: ruin grids {ruinGrids[i].Name} and {ruinGrids[j].Name} overlap");
            }
        }
    }

    private static bool IsLavaOrChasm(string prototypeId)
    {
        return prototypeId is LavaPrototype or "FloorChasmEntity";
    }
}
