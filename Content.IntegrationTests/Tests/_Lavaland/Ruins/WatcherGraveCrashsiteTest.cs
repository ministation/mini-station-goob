// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Server._Lavaland.Ruins;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Lavaland.Ruins;

/// <summary>
/// Guards the two TG ruins ported as marker builders: the watcher grave (stone
/// mosaic, chasm, corpses, watcher egg, grave cache) and the crash site (pod
/// wreck, survival gear, hot springs, goliaths, supply cache).
/// </summary>
[TestFixture]
[TestOf(typeof(WatcherGraveSpawnerComponent))]
[TestOf(typeof(CrashsiteSpawnerComponent))]
public sealed class WatcherGraveCrashsiteTest
{
    [Test]
    public async Task WatcherGraveSpawnsMosaicCorpsesAndLoot()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid marker = default;
        await server.WaitPost(() => marker = entMan.SpawnAtPosition("LavalandWatcherGraveSpawner", testMap.GridCoords));

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var counts = CountPrototypes(entMan);

            Assert.Multiple(() =>
            {
                Assert.That(counts.GetValueOrDefault("WallNecropolisIndestructible"), Is.GreaterThanOrEqualTo(20),
                    "the grave must be ringed by its stone walls");
                Assert.That(counts.GetValueOrDefault("FloorChasmEntity"), Is.GreaterThanOrEqualTo(5),
                    "the grave must sit over a chasm");
                Assert.That(counts.GetValueOrDefault("LavalandGraveTileSlab"), Is.GreaterThanOrEqualTo(3),
                    "the mosaic needs its slabs");
                Assert.That(counts.GetValueOrDefault("LavalandGraveTileCracked"), Is.GreaterThanOrEqualTo(5),
                    "the mosaic needs its cracked tiles");
                Assert.That(counts.GetValueOrDefault("LavalandGraveTileCenter"), Is.EqualTo(1),
                    "the mosaic has a single center piece");
                Assert.That(counts.GetValueOrDefault("LavalandGraveTileSurrounding"), Is.EqualTo(1),
                    "the mosaic has a single outer ring");
                Assert.That(counts.GetValueOrDefault("LavalandWatcherEgg"), Is.EqualTo(1),
                    "the grave holds a watcher egg");
                Assert.That(counts.GetValueOrDefault("LavalandCorpseWatcher"), Is.GreaterThanOrEqualTo(1),
                    "the grave holds a dead watcher");
                Assert.That(counts.GetValueOrDefault("LavalandCorpseGoliath"), Is.GreaterThanOrEqualTo(1),
                    "the grave holds goliath remains");
                Assert.That(counts.GetValueOrDefault("LavalandGraveCache"), Is.EqualTo(1),
                    "the grave must have its offering cache");
                Assert.That(CountLoot(counts,
                        "MaterialWatcherWing5",
                        "MaterialWatcherMagmaWing5",
                        "MaterialWatcherIceWing5",
                        "MaterialLegionSkull1",
                        "MaterialBones1"), Is.GreaterThan(0),
                    "the grave cache must roll the fixed material table");
                Assert.That(counts.GetValueOrDefault("FloraAshFireBlossom"), Is.GreaterThanOrEqualTo(1),
                    "ash flora must grow around the grave");
                Assert.That(entMan.EntityExists(marker), Is.True,
                    "the marker must stay alive for the prototype sweep");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CrashsiteSpawnsPodWreckAndLoot()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid marker = default;
        await server.WaitPost(() => marker = entMan.SpawnAtPosition("LavalandCrashsiteSpawner", testMap.GridCoords));

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var counts = CountPrototypes(entMan);

            Assert.Multiple(() =>
            {
                Assert.That(counts.GetValueOrDefault("WallPlastitaniumIndestructible"), Is.GreaterThanOrEqualTo(10),
                    "the pod hull must be part of the wreck");
                Assert.That(counts.GetValueOrDefault("WallPlastitanium"), Is.GreaterThanOrEqualTo(5),
                    "the interior walls must be part of the wreck");
                Assert.That(counts.GetValueOrDefault("AirlockShuttle"), Is.GreaterThanOrEqualTo(2),
                    "the pod should keep its titanium airlocks");
                Assert.That(counts.GetValueOrDefault("AirlockGlassShuttle"), Is.GreaterThanOrEqualTo(1),
                    "the pod should keep a glass airlock");
                Assert.That(counts.GetValueOrDefault("Thruster"), Is.GreaterThanOrEqualTo(4),
                    "the wreck needs its burst engines");
                Assert.That(counts.GetValueOrDefault("ShuttleWindow"), Is.GreaterThanOrEqualTo(1),
                    "the wreck needs a broken cockpit window");
                Assert.That(counts.GetValueOrDefault("Girder"), Is.GreaterThanOrEqualTo(1),
                    "part of the hull should be torn down to a girder");
                Assert.That(counts.GetValueOrDefault("Bonfire"), Is.GreaterThanOrEqualTo(1),
                    "the survivors built a bonfire");
                Assert.That(counts.GetValueOrDefault("FloraTreeStump"), Is.GreaterThanOrEqualTo(1),
                    "the crash site has dead trees");
                Assert.That(counts.GetValueOrDefault("FloorWaterEntity"), Is.GreaterThanOrEqualTo(5),
                    "the crash site sits by hot springs");
                Assert.That(counts.GetValueOrDefault("Bed"), Is.GreaterThanOrEqualTo(1),
                    "the pod has pod beds");
                Assert.That(counts.GetValueOrDefault("Rack"), Is.GreaterThanOrEqualTo(2),
                    "the pod has supply racks");
                Assert.That(counts.GetValueOrDefault("SmartFridge"), Is.GreaterThanOrEqualTo(1),
                    "the pod has its dusty storage fridge");
                Assert.That(counts.GetValueOrDefault("WeaponProtoKineticAccelerator"), Is.EqualTo(1),
                    "a kinetic accelerator lies in the wreck");
                Assert.That(counts.GetValueOrDefault("MobLavalandGoliath"), Is.GreaterThanOrEqualTo(1),
                    "goliaths scavenge the crash site");
                Assert.That(counts.GetValueOrDefault("MobLavalandGoliathAncient"), Is.EqualTo(1),
                    "an ancient goliath guards the wreck");
                Assert.That(counts.GetValueOrDefault("LavalandCorpseGoliath"), Is.GreaterThanOrEqualTo(1),
                    "a dead goliath lies in the wreck");
                Assert.That(counts.GetValueOrDefault("LavalandCrashPodCache"), Is.EqualTo(1),
                    "the pod must have its supply cache");
                Assert.That(CountLoot(counts,
                        "PKAUpgradeDamage",
                        "PKAUpgradeRange",
                        "PKAUpgradeFireRate",
                        "PKAUpgradePressure",
                        "PKAUpgradeSpace"), Is.GreaterThan(0),
                    "the supply cache must roll the fixed PKA modkit table");
                Assert.That(entMan.EntityExists(marker), Is.True,
                    "the marker must stay alive for the prototype sweep");
            });
        });

        await pair.CleanReturnAsync();
    }

    private static int CountLoot(Dictionary<string, int> counts, params string[] ids)
    {
        return ids.Sum(id => counts.GetValueOrDefault(id));
    }

    private static Dictionary<string, int> CountPrototypes(IEntityManager entMan)
    {
        var counts = new Dictionary<string, int>();
        foreach (var uid in entMan.GetEntities())
        {
            if (!entMan.TryGetComponent<MetaDataComponent>(uid, out var meta))
                continue;

            if (meta.EntityPrototype is not { } proto)
                continue;

            counts[proto.ID] = counts.GetValueOrDefault(proto.ID) + 1;
        }

        return counts;
    }
}
