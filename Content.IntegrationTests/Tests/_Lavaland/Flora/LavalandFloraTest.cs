// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Linq;
using System.Numerics;
using Content.Server._Lavaland.Flora;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.StepTrigger.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Lavaland.Flora;

/// <summary>
/// TG ash flora parity: the produce items carry the reagents from ash_flora.dm, and the
/// caltrop moved from the star cacti structure to the TG fruiting cacti structure.
/// </summary>
[TestFixture]
[TestOf(typeof(LavalandFloraSystem))]
public sealed class LavalandFloraTest
{
    [Test]
    public async Task ProduceAndCaltropMatchTg()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var solutions = server.System<SharedSolutionContainerSystem>();

        await server.WaitAssertion(() =>
        {
            AssertProduce(entMan, solutions, testMap.GridCoords.Offset(new Vector2(0, 0)), "FoodAshMushroomCap",
                "MindbreakerToxin", "Entpoly", "MushroomHallucinogen");
            AssertProduce(entMan, solutions, testMap.GridCoords.Offset(new Vector2(1, 0)), "FoodAshMushroomStem",
                "Tinlux", "SpaceDrugs");
            AssertProduce(entMan, solutions, testMap.GridCoords.Offset(new Vector2(2, 0)), "FoodAshVahrezjiaStalks",
                "Berserker");
            AssertProduce(entMan, solutions, testMap.GridCoords.Offset(new Vector2(3, 0)), "FoodAshPodsneyzkaBunch",
                "Cryostylane", "LiquidGibs");

            var cacti = entMan.SpawnEntity("FruitCactus", testMap.GridCoords.Offset(new Vector2(4, 0)));
            Assert.That(entMan.HasComponent<LavalandFloraCaltropComponent>(cacti), Is.True,
                "TG fruiting cacti hurt whoever steps on them");
            Assert.That(entMan.HasComponent<StepTriggerComponent>(cacti), Is.True,
                "the fruiting cacti caltrop needs a step trigger to fire");

            var starCacti = entMan.SpawnEntity("FloraAshStarCacti", testMap.GridCoords.Offset(new Vector2(5, 0)));
            Assert.That(entMan.HasComponent<LavalandFloraCaltropComponent>(starCacti), Is.False,
                "TG star cacti are only a hydroponics plant, the caltrop belongs to the fruiting cacti");
            Assert.That(entMan.HasComponent<StepTriggerComponent>(starCacti), Is.False,
                "star cacti should not keep the fruiting cacti step trigger");
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertProduce(IEntityManager entMan, SharedSolutionContainerSystem solutions,
        EntityCoordinates coordinates, string prototype, params string[] reagents)
    {
        var produce = entMan.SpawnEntity(prototype, coordinates);
        Assert.That(solutions.TryGetSolution(produce, "food", out _, out var solution), Is.True,
            prototype + " has no food solution");

        var present = solution.Contents.Select(reagent => reagent.Reagent.Prototype).ToList();
        foreach (var reagent in reagents)
            Assert.That(present, Does.Contain(reagent), prototype + " is missing " + reagent);
    }
}
