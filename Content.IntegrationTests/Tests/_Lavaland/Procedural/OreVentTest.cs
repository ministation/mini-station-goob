// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Goobstation.Shared.GPS.Components;
using Content.Server._Lavaland.OreVent;
using Content.Shared._Lavaland.OreVent;
using Content.Shared._Lavaland.UnclaimedOre;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Lavaland.Procedural;

/// <summary>
/// Guards the ore vent loop: marker spawner builds the vent, the defense event
/// deploys a NODE drone and a wave, and a won defense produces diggable boulders
/// and a reward once the vent runs dry.
/// </summary>
[TestFixture]
[TestOf(typeof(OreVentComponent))]
[TestOf(typeof(OreBoulderComponent))]
public sealed class OreVentTest
{
    [Test]
    public async Task DefenseProducesBouldersAndDepletionReward()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid vent = default;
        OreVentComponent ventComp = null!;

        await server.WaitPost(() =>
        {
            vent = entMan.SpawnAtPosition("LavalandOreVent", testMap.GridCoords);
            ventComp = entMan.GetComponent<OreVentComponent>(vent);

            // Keep the test fast but otherwise use the normal components.
            ventComp.WaveCount = 1;
            ventComp.MobsPerWave = 1;
            ventComp.WaveInterval = TimeSpan.FromMilliseconds(100);
            ventComp.BoulderInterval = TimeSpan.FromMilliseconds(100);
            ventComp.ArtifactBoulderChance = 0;
            ventComp.BoulderBudget = 2;
            ventComp.BouldersRemaining = 2;
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(ventComp.BouldersRemaining, Is.EqualTo(2), "vent should start with its boulder budget");
            Assert.That(entMan.System<OreVentSystem>().TryStartDefense((vent, ventComp)), Is.True,
                "an idle vent should accept a tap");
        });

        // One second of windup, then the wave spawns.
        await server.WaitRunTicks(70);

        EntityUid drone = default;
        await server.WaitAssertion(() =>
        {
            Assert.That(ventComp.Defending, Is.True, "vent should be under defense");

            var drones = CollectPrototypes(entMan, "LavalandNodeDrone");
            Assert.That(drones.Count, Is.EqualTo(1), "exactly one NODE drone should guard the vent");
            drone = drones[0];

            var thresholds = entMan.GetComponent<MobThresholdsComponent>(drone);
            var maxHp = FixedPoint2.Zero;
            foreach (var hp in thresholds.Thresholds.Keys)
            {
                if (hp > maxHp)
                    maxHp = hp;
            }

            Assert.That(maxHp, Is.GreaterThan(FixedPoint2.Zero), "NODE drone must have positive health");
            Assert.That(CountAliveWaveMobs(entMan, ventComp), Is.GreaterThan(0), "a wave should have spawned");
        });

        // Kill the wave. The drone survives, so the vent is tapped.
        await server.WaitPost(() =>
        {
            var damage = new DamageSpecifier { DamageDict = { { "Blunt", 10000f } } };
            foreach (var mob in ventComp.SpawnedMobs)
            {
                if (!entMan.Deleted(mob))
                    entMan.System<DamageableSystem>().TryChangeDamage(mob, damage, true);
            }
        });

        await server.WaitRunTicks(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(ventComp.Defending, Is.False, "defense should end once the wave is dead");
            Assert.That(ventComp.Tapped, Is.True, "a won defense should tap the vent");
        });

        await server.WaitRunTicks(40);

        await server.WaitAssertion(() =>
        {
            Assert.That(ventComp.BouldersRemaining, Is.EqualTo(0), "vent should run out of its budget");
            Assert.That(CountBoulders(entMan), Is.GreaterThan(0), "tapped vent should produce boulders");
            Assert.That(CollectPrototypes(entMan, "LavalandCrateNecropolisFilled").Count, Is.GreaterThan(0),
                "depleted vent should leave its reward behind");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MarkerSpawnerBuildsVentAndBoulderDropsOre()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitPost(() =>
        {
            entMan.SpawnAtPosition("LavalandOreVentSpawner", testMap.GridCoords);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            // Only count vents on this test map: the server may still hold maps from earlier tests.
            var vents = CollectPrototypes(entMan, "LavalandOreVent")
                .Where(v => entMan.GetComponent<TransformComponent>(v).MapID == testMap.MapId)
                .ToList();
            Assert.That(vents.Count, Is.EqualTo(1), "the marker spawner should build exactly one vent");

            var ventComp = entMan.GetComponent<OreVentComponent>(vents[0]);
            Assert.That(ventComp.BouldersRemaining, Is.InRange(6, 12),
                "the spawner should pick a boulder budget for the vent");
        });

        EntityUid boulder = default;
        EntityUid pickaxe = default;
        await server.WaitPost(() =>
        {
            var coords = testMap.GridCoords.Offset(new Vector2(3, 0));
            boulder = entMan.SpawnAtPosition("LavalandOreBoulder", coords);
            pickaxe = entMan.SpawnAtPosition("Pickaxe", coords);
        });

        await server.WaitRunTicks(2);

        // Two hits crumble the boulder, exactly like a miner swinging a pickaxe.
        await server.WaitPost(() =>
        {
            var coords = entMan.GetComponent<TransformComponent>(boulder).Coordinates;
            for (var i = 0; i < 2; i++)
                entMan.EventBus.RaiseLocalEvent(boulder, new AttackedEvent(pickaxe, pickaxe, coords));
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(boulder), Is.True, "boulder should crumble after enough pickaxe hits");
            Assert.That(CountUnclaimedOre(entMan), Is.GreaterThan(0), "broken boulder should drop ore");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ArtifactBoulderDropsRealArtifact()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid boulder = default;
        EntityUid pickaxe = default;
        await server.WaitPost(() =>
        {
            var coords = testMap.GridCoords.Offset(new Vector2(3, 0));
            boulder = entMan.SpawnAtPosition("LavalandOreBoulderArtifact", coords);
            pickaxe = entMan.SpawnAtPosition("Pickaxe", coords);
        });

        await server.WaitRunTicks(2);

        // Artifact boulders take four hits.
        await server.WaitPost(() =>
        {
            var coords = entMan.GetComponent<TransformComponent>(boulder).Coordinates;
            for (var i = 0; i < 4; i++)
                entMan.EventBus.RaiseLocalEvent(boulder, new AttackedEvent(pickaxe, pickaxe, coords));
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(boulder), Is.True, "artifact boulder should crumble after four pickaxe hits");
            // Only count artifacts on this test map: the server may still hold maps from earlier tests.
            var artifacts = CollectPrototypes(entMan, "ComplexXenoArtifactItem")
                .Where(a => entMan.GetComponent<TransformComponent>(a).MapID == testMap.MapId)
                .ToList();
            Assert.That(artifacts.Count, Is.EqualTo(1),
                "artifact boulder must drop one real xeno artifact");
            Assert.That(CountUnclaimedOre(entMan), Is.GreaterThanOrEqualTo(3),
                "artifact boulder must also drop diamonds and bluespace");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task VentStartsUndiscoveredAndScanRevealsIt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid vent = default;
        OreVentComponent ventComp = null!;
        await server.WaitPost(() =>
        {
            vent = entMan.SpawnAtPosition("LavalandOreVent", testMap.GridCoords);
            ventComp = entMan.GetComponent<OreVentComponent>(vent);
        });

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(ventComp.Discovered, Is.False, "vents start undiscovered");
                Assert.That(ventComp.Size, Is.EqualTo(OreVentSize.Medium), "vents default to medium size");
                Assert.That(ventComp.Minerals, Has.Count.EqualTo(4), "a vent rolls four minerals");
            });
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.System<OreVentSystem>().TryDiscover((vent, ventComp)), Is.True,
                "an undiscovered vent should accept the scan");
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(ventComp.Discovered, Is.True, "the scan should reveal the vent");
            Assert.That(entMan.TryGetComponent<GPSComponent>(vent, out var gps), Is.True,
                "a discovered vent gets a GPS signal");
            Assert.That(gps!.Enabled, Is.True, "the vent GPS signal is enabled");
            Assert.That(gps.GpsName, Is.Not.Empty, "the GPS signal has a name");
        });

        // Size drives the NODE drone: large vents get the 500 HP drone.
        await server.WaitAssertion(() =>
        {
            ventComp.Size = OreVentSize.Large;
            ventComp.ArtifactBoulderChance = 0;
            Assert.That(entMan.System<OreVentSystem>().TryStartDefense((vent, ventComp)), Is.True,
                "a discovered vent should accept a tap");
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var drones = CollectPrototypes(entMan, "LavalandNodeDrone");
            Assert.That(drones, Has.Count.EqualTo(1), "exactly one NODE drone should guard the vent");

            var thresholds = entMan.GetComponent<MobThresholdsComponent>(drones[0]);
            var maxHp = FixedPoint2.Zero;
            foreach (var hp in thresholds.Thresholds.Keys)
            {
                if (hp > maxHp)
                    maxHp = hp;
            }

            Assert.That(maxHp, Is.EqualTo(FixedPoint2.New(500)), "a large vent gets a 500 HP drone");
            Assert.That(ventComp.WavesRemaining, Is.EqualTo(4), "a large vent gets four waves");
        });

        await pair.CleanReturnAsync();
    }

    private static int CountAliveWaveMobs(IEntityManager entMan, OreVentComponent ventComp)
    {
        var alive = 0;
        foreach (var mob in ventComp.SpawnedMobs)
        {
            if (!entMan.Deleted(mob)
                && entMan.TryGetComponent<MobStateComponent>(mob, out var mobState)
                && mobState.CurrentState == MobState.Alive)
                alive++;
        }

        return alive;
    }

    private static int CountBoulders(IEntityManager entMan)
    {
        var count = 0;
        var query = entMan.EntityQueryEnumerator<OreBoulderComponent>();
        while (query.MoveNext(out _, out _))
            count++;
        return count;
    }

    private static int CountUnclaimedOre(IEntityManager entMan)
    {
        var count = 0;
        var query = entMan.EntityQueryEnumerator<UnclaimedOreComponent>();
        while (query.MoveNext(out _, out _))
            count++;
        return count;
    }

    private static List<EntityUid> CollectPrototypes(IEntityManager entMan, string protoId)
    {
        var found = new List<EntityUid>();
        var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var uid, out var meta))
        {
            if (!entMan.Deleted(uid) && meta.EntityPrototype?.ID == protoId)
                found.Add(uid);
        }

        return found;
    }
}
