// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Server.Botany;
using Content.Server.Botany.Components;
using Content.Server.Gatherable;
using Content.Server.Gatherable.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Flora;

/// <summary>
/// Guards the TG ash flora port: every plant prototype exists and is gatherable,
/// every seed pack resolves to a real seed prototype, and the seed vault hands out
/// the lavaland seed cache instead of only ordinary seeds.
/// </summary>
[TestFixture]
[TestOf(typeof(GatherableComponent))]
public sealed class LavalandFloraTest
{
    private static readonly Dictionary<string, string> FloraProduce = new()
    {
        { "FloraAshFireBlossom", "FoodAshFireBlossom" },
        { "FloraAshGlowgrowth", "FoodAshGlowgrowthSheet" },
        { "FloraAshInozhakust", "FoodAshInozhakustMash" },
        { "FloraAshPlasmaFlower", "FoodAshPlasmaFlower" },
        { "FloraAshPodsneyzka", "FoodAshPodsneyzkaBunch" },
        { "FloraAshVahrezjia", "FoodAshVahrezjiaStalks" },
        { "FloraAshDrakeBeet", "FoodAshDrakeBeet" },
        { "FloraAshStarCacti", "FoodAshStarCactus" },
        { "FloraAshLargeMushrooms", "FoodAshMushroomShavings" },
        { "FloraAshLeafyMushrooms", "FoodAshMushroomLeaf" },
        { "FloraAshTallMushrooms", "FoodAshMushroomCap" },
        { "FloraAshNumerousMushrooms", "FoodAshMushroomStem" },
    };

    private static readonly Dictionary<string, string> SeedPackets = new()
    {
        { "LavalandCactusSeeds", "lavalandCactus" },
        { "LavalandStarCactusSeeds", "lavalandStarCactus" },
        { "LavalandPolyporeMycelium", "lavalandPolypore" },
        { "LavalandPorciniMycelium", "lavalandPorcini" },
        { "LavalandInocybeMycelium", "lavalandInocybe" },
        { "LavalandEmberMycelium", "lavalandEmber" },
        { "LavalandSerakaMycelium", "lavalandSeraka" },
        { "LavalandFireBlossomSeeds", "lavalandFireblossom" },
        { "LavalandGlowgrowthMycelium", "lavalandGlowgrowth" },
        { "LavalandVahrezjiaSeeds", "lavalandVahrezjia" },
        { "LavalandPodsneyzkaSeeds", "lavalandPodsneyzka" },
        { "LavalandInozhakustSeeds", "lavalandInozhakust" },
        { "LavalandPlasmaFlowerSeeds", "lavalandPlasmaFlower" },
    };

    [Test]
    public async Task FloraAndSeedPrototypesResolve()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var compFactory = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            foreach (var (floraId, produceId) in FloraProduce)
            {
                Assert.That(protoMan.HasIndex<EntityPrototype>(floraId), Is.True,
                    $"{floraId} should exist");
                Assert.That(protoMan.Index<EntityPrototype>(floraId).HasComp<GatherableComponent>(compFactory), Is.True,
                    $"{floraId} should be gatherable");

                Assert.That(protoMan.HasIndex<EntityPrototype>(produceId), Is.True,
                    $"{produceId} produce should exist");
            }

            foreach (var (packetId, seedId) in SeedPackets)
            {
                Assert.That(protoMan.HasIndex<EntityPrototype>(packetId), Is.True,
                    $"{packetId} should exist");
                Assert.That(protoMan.Index<EntityPrototype>(packetId).HasComp<SeedComponent>(compFactory), Is.True,
                    $"{packetId} should carry a seed");
                Assert.That(protoMan.HasIndex<SeedPrototype>(seedId), Is.True,
                    $"{packetId} points at missing seed prototype {seedId}");

                var seedProto = protoMan.Index<SeedPrototype>(seedId);
                Assert.That(seedProto.ProductPrototypes, Is.Not.Empty,
                    $"seed {seedId} should produce something");
                foreach (var product in seedProto.ProductPrototypes)
                    Assert.That(protoMan.HasIndex<EntityPrototype>(product), Is.True,
                        $"seed {seedId} points at missing product {product}");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GatheringFloraSpawnsProduce()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        foreach (var (floraId, produceId) in FloraProduce)
        {
            EntityUid flora = default;
            await server.WaitPost(() =>
            {
                flora = entMan.SpawnEntity(floraId, testMap.GridCoords);
            });

            await server.WaitRunTicks(2);

            await server.WaitPost(() =>
            {
                entMan.System<GatherableSystem>().Gather(flora);
            });

            await server.WaitRunTicks(2);

            await server.WaitAssertion(() =>
            {
                Assert.That(entMan.Deleted(flora), Is.True,
                    $"{floraId} should be consumed by gathering");
                Assert.That(CountPrototypes(entMan, produceId), Is.GreaterThan(0),
                    $"{floraId} should drop {produceId}");
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SeedVaultHandsOutLavalandSeedCache()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitPost(() =>
        {
            entMan.SpawnEntity("LavalandSeedVaultSpawner", testMap.GridCoords);
        });

        await server.WaitRunTicks(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(CountPrototypes(entMan, "LavalandSeedCache"), Is.EqualTo(1),
                "the seed vault should hold one lavaland seed cache");

            var lavalandSeeds = 0;
            foreach (var packetId in SeedPackets.Keys)
                lavalandSeeds += CountPrototypes(entMan, packetId);

            Assert.That(lavalandSeeds, Is.GreaterThan(0),
                "the seed vault should contain at least one lavaland seed pack");
        });

        await pair.CleanReturnAsync();
    }

    private static int CountPrototypes(IEntityManager entMan, string protoId)
    {
        var count = 0;
        var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var uid, out var meta))
        {
            if (!entMan.Deleted(uid) && meta.EntityPrototype?.ID == protoId)
                count++;
        }
        return count;
    }
}
