// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Server._Lavaland.Flora;
using Content.Server.Botany;
using Content.Server.Botany.Components;
using Content.Server.Gatherable;
using Content.Server.Gatherable.Components;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax.Biomes.Layers;
using Content.Shared.StepTrigger.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Flora;

/// <summary>
/// Guards the TG ash flora port: every plant prototype exists, is gatherable and regrows
/// after being picked, every produce item points at a real seed prototype, the new plants
/// spawn on the planet, and the seed vault hands out the lavaland seed cache.
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
        { "FloraAshSeraka", "FoodAshSerakaCap" },
        { "FloraAshFonarstolbe", "FoodAshFonarstolbe" },
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
        { "LavalandFonarstolbeSeeds", "lavalandFonarstolbe" },
    };

    /// <summary>
    /// Produce items that should hand the seed extractor the matching seed prototype.
    /// </summary>
    private static readonly Dictionary<string, string> ProduceSeeds = new()
    {
        { "FoodAshFireBlossom", "lavalandFireblossom" },
        { "FoodAshGlowgrowthSheet", "lavalandGlowgrowth" },
        { "FoodAshInozhakustMash", "lavalandInozhakust" },
        { "FoodAshPlasmaFlower", "lavalandPlasmaFlower" },
        { "FoodAshPodsneyzkaBunch", "lavalandPodsneyzka" },
        { "FoodAshVahrezjiaStalks", "lavalandVahrezjia" },
        { "FoodAshStarCactus", "lavalandStarCactus" },
        { "FoodAshMushroomShavings", "lavalandPolypore" },
        { "FoodAshMushroomLeaf", "lavalandPorcini" },
        { "FoodAshMushroomCap", "lavalandInocybe" },
        { "FoodAshMushroomStem", "lavalandEmber" },
        { "FoodAshSerakaCap", "lavalandSeraka" },
        { "FoodAshFonarstolbe", "lavalandFonarstolbe" },
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

                var floraProto = protoMan.Index<EntityPrototype>(floraId);
                Assert.That(floraProto.HasComp<GatherableComponent>(compFactory), Is.True,
                    $"{floraId} should be gatherable");
                Assert.That(floraProto.HasComp<LavalandFloraRegrowComponent>(compFactory), Is.True,
                    $"{floraId} should regrow after being picked");

                Assert.That(protoMan.HasIndex<EntityPrototype>(produceId), Is.True,
                    $"{produceId} produce should exist");
            }

            var cactus = protoMan.Index<EntityPrototype>("FloraAshStarCacti");
            Assert.That(cactus.HasComp<StepTriggerComponent>(compFactory), Is.True,
                "cacti should trigger on a step");
            Assert.That(cactus.HasComp<LavalandFloraCaltropComponent>(compFactory), Is.True,
                "cacti should be a caltrop");

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
    public async Task ProducePointsAtSeeds()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var compFactory = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            foreach (var (produceId, seedId) in ProduceSeeds)
            {
                var produceProto = protoMan.Index<EntityPrototype>(produceId);
                Assert.That(produceProto.Components.TryGetComponent(
                        compFactory.GetComponentName(typeof(ProduceComponent)), out var component), Is.True,
                    $"{produceId} should have a Produce component");
                Assert.That(((ProduceComponent) component!).SeedId, Is.EqualTo(seedId),
                    $"{produceId} should extract {seedId}");
                Assert.That(protoMan.HasIndex<SeedPrototype>(seedId), Is.True,
                    $"{seedId} should exist");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NewFloraIsInBiomeSpawnPools()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var expected = new[] { "FloraAshSeraka", "FloraAshFonarstolbe" };
            var found = new HashSet<string>();

            foreach (var biome in protoMan.EnumeratePrototypes<BiomeTemplatePrototype>())
            {
                foreach (var layer in biome.Layers)
                {
                    if (layer is not BiomeEntityLayer entityLayer)
                        continue;

                    foreach (var entity in entityLayer.Entities)
                    {
                        if (expected.Contains(entity.Id))
                            found.Add(entity.Id);
                    }
                }
            }

            Assert.That(found, Is.EquivalentTo(expected),
                "both new plants should be in a biome spawn pool");
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
    public async Task GatheredFloraRegrows()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        const string floraId = "FloraAshLargeMushrooms";

        EntityUid flora = default;
        await server.WaitPost(() =>
        {
            flora = entMan.SpawnEntity(floraId, testMap.GridCoords);
            var regrow = entMan.GetComponent<LavalandFloraRegrowComponent>(flora);
            regrow.MinDelay = 0.3f;
            regrow.MaxDelay = 0.3f;
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
                "the picked plant should be consumed");
            Assert.That(CountGatherable(entMan, floraId), Is.EqualTo(0),
                "the picked stub should not be gatherable");
            Assert.That(CountPrototypes(entMan, floraId), Is.EqualTo(1),
                "the picked stub should stay in place");
        });

        await server.WaitRunTicks(30);

        await server.WaitAssertion(() =>
        {
            Assert.That(CountPrototypes(entMan, floraId), Is.EqualTo(1),
                "the plant should be back in place");
            Assert.That(CountGatherable(entMan, floraId), Is.EqualTo(1),
                "the regrown plant should be gatherable");
        });

        EntityUid regrown = default;
        await server.WaitPost(() =>
        {
            regrown = FindGatherable(entMan, floraId);
        });

        await server.WaitPost(() =>
        {
            entMan.System<GatherableSystem>().Gather(regrown);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(regrown), Is.True,
                "the regrown plant should be gatherable again");
        });

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

    private static int CountGatherable(IEntityManager entMan, string protoId)
    {
        var count = 0;
        var query = entMan.EntityQueryEnumerator<MetaDataComponent, GatherableComponent>();
        while (query.MoveNext(out var uid, out var meta, out _))
        {
            if (!entMan.Deleted(uid) && meta.EntityPrototype?.ID == protoId)
                count++;
        }
        return count;
    }

    private static EntityUid FindGatherable(IEntityManager entMan, string protoId)
    {
        var query = entMan.EntityQueryEnumerator<MetaDataComponent, GatherableComponent>();
        while (query.MoveNext(out var uid, out var meta, out _))
        {
            if (!entMan.Deleted(uid) && meta.EntityPrototype?.ID == protoId)
                return uid;
        }

        Assert.Fail($"no gatherable {protoId} found");
        return default;
    }
}
