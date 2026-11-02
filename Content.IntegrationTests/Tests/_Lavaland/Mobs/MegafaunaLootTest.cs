// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Goobstation.Shared.GPS.Components;
using Content.Shared.Actions;
using Content.Shared.Clothing.Components;
using Content.Shared.Actions.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Storage;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Mobs;

/// <summary>
/// Regression tests for the megafauna loot port: ash drake butchery, the
/// bubblegum sarcophagus death drop, boss GPS trackers and enrage-only actions.
/// </summary>
[TestFixture]
public sealed class MegafaunaLootTest
{
    private static readonly (string Proto, string GpsName)[] GpsProtos =
    {
        ("LavalandBossAshDrake", "Fiery Signal"),
        ("LavalandBossColossus", "Angelic Signal"),
        ("LavalandBossBubblegum", "Bloody Signal"),
        ("LavalandBossBloodDrunk", "Berserk Signal"),
        ("MobHierophant", "Zealous Signal"),
        ("LavalandTendrilGoliath", "Eerie Signal"),
    };

    [Test]
    public async Task AshDrakeButchersIntoHide()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        EntityUid drake = default;
        EntityUid hide = default;

        await server.WaitPost(() =>
        {
            drake = entMan.SpawnEntity("LavalandBossAshDrake", testMap.GridCoords);
            hide = entMan.SpawnEntity("LavalandAshDrakeHide1", testMap.GridCoords);
        });
        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.TryGetComponent<ButcherableComponent>(drake, out var butcher), Is.True,
                "ash drake must be butcherable");

            var spawns = EntitySpawnCollection.GetSpawns(butcher!.SpawnedEntities);
            Assert.That(spawns.Count(id => id == "LavalandAshDrakeHide1"), Is.EqualTo(10),
                "one ash drake must yield 10 ash drake hide");

            foreach (var id in new[]
                     {
                         "LavalandAshDrakeHide1", "LavalandEquipmentDrakeArmour", "LavalandEquipmentDrakeHelm",
                     })
            {
                Assert.That(protoMan.HasIndex<EntityPrototype>(id), Is.True, $"{id} prototype is missing");
            }

            // The hide prototype the butcher table spawns must initialize and not delete itself.
            Assert.That(entMan.Deleted(hide), Is.False, "spawned ash drake hide must stay in the world");
        });

        await server.WaitPost(() =>
        {
            entMan.DeleteEntity(drake);
            entMan.DeleteEntity(hide);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BubblegumDropsSarcophagusOnDeath()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid bubblegum = default;

        await server.WaitPost(() => bubblegum = entMan.SpawnEntity("LavalandBossBubblegum", testMap.GridCoords));
        await server.WaitRunTicks(5);

        await server.WaitPost(() => server.System<MobStateSystem>().ChangeMobState(bubblegum, MobState.Dead));
        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(FindPrototype(entMan, "LavalandCrateSarcophagus"), Is.True,
                "bubblegum death must drop its sarcophagus");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BubblegumSarcophagusDropsSuitWithItsOwnHelmetOnly()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid bubblegum = default;

        await server.WaitPost(() => bubblegum = entMan.SpawnEntity("LavalandBossBubblegum", testMap.GridCoords));
        await server.WaitRunTicks(5);

        await server.WaitPost(() => server.System<MobStateSystem>().ChangeMobState(bubblegum, MobState.Dead));
        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(CountPrototypes(entMan, "ClothingOuterHardsuitHECK"), Is.EqualTo(1),
                "bubblegum sarcophagus must drop one H.E.C.K. suit");
            Assert.That(CountPrototypes(entMan, "ClothingHeadHelmetHECK"), Is.EqualTo(1),
                "the suit drops its helmet by itself, the sarcophagus must not add a second loose one");

            var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out var uid, out var meta))
            {
                if (meta.EntityPrototype?.ID != "ClothingHeadHelmetHECK")
                    continue;

                Assert.That(entMan.HasComponent<AttachedClothingComponent>(uid), Is.True,
                    "the helmet must be attached to the suit, not a separately pickable item");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BossesAndTendrilsHaveGps()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        foreach (var (proto, gpsName) in GpsProtos)
        {
            EntityUid mob = default;

            await server.WaitPost(() => mob = entMan.SpawnEntity(proto, testMap.GridCoords));
            await server.WaitRunTicks(2);

            await server.WaitAssertion(() =>
            {
                Assert.That(entMan.TryGetComponent<GPSComponent>(mob, out var gps), Is.True,
                    $"{proto} has no GPS component");
                Assert.Multiple(() =>
                {
                    Assert.That(gps!.GpsName, Is.EqualTo(gpsName), $"{proto} GPS name mismatch");
                    Assert.That(gps.Enabled, Is.True, $"{proto} GPS is disabled");
                });
            });

            await server.WaitPost(() => entMan.DeleteEntity(mob));
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EnrageActionsAreGranted()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        var expected = new (string Proto, string[] Actions)[]
        {
            ("LavalandBossColossus", new[] { "ActionColossusFinale" }),
            ("MobHierophant", new[] { "ActionHierophantCrossesEnraged", "ActionHierophantBlinkSpam" }),
        };

        foreach (var (proto, actions) in expected)
        {
            EntityUid mob = default;

            await server.WaitPost(() => mob = entMan.SpawnEntity(proto, testMap.GridCoords));
            await server.WaitRunTicks(5);

            await server.WaitAssertion(() =>
            {
                Assert.That(entMan.TryGetComponent<ActionsComponent>(mob, out var granted), Is.True,
                    $"{proto} has no actions");

                var ids = new List<string>();
                foreach (var action in granted!.Actions)
                {
                    if (entMan.Deleted(action))
                        continue;
                    ids.Add(entMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID ?? action.ToString());
                }

                foreach (var action in actions)
                {
                    Assert.That(ids, Does.Contain(action),
                        $"{proto} did not receive {action}, granted: {string.Join(", ", ids)}");
                }
            });

            await server.WaitPost(() => entMan.DeleteEntity(mob));
        }

        await pair.CleanReturnAsync();
    }

    private static int CountPrototypes(IEntityManager entMan, string id)
    {
        var count = 0;
        var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var meta))
        {
            if (meta.EntityPrototype?.ID == id)
                count++;
        }

        return count;
    }

    private static bool FindPrototype(IEntityManager entMan, string id)
    {
        var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var meta))
        {
            if (meta.EntityPrototype?.ID == id)
                return true;
        }

        return false;
    }
}
