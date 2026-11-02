// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Nutrition.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

[TestFixture]
public sealed class FishEdibleTest : InteractionTest
{
    [Test]
    public async Task FishIsEdible()
    {
        var goldfish = SEntMan.GetEntity(await Spawn("FishGoldfish"));
        var monkfish = SEntMan.GetEntity(await Spawn("FishMonkfish"));
        var holo = SEntMan.GetEntity(await Spawn("FishHolo"));

        await Server.WaitPost(() =>
        {
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();

            var edible = SEntMan.GetComponent<EdibleComponent>(goldfish);
            Assert.That(edible.TransferAmount?.Float(), Is.GreaterThan(0f));

            Assert.That(SEntMan.TryGetComponent<SolutionContainerManagerComponent>(goldfish, out var manager), Is.True);
            Assert.That(solutions.TryGetSolution(new Entity<SolutionContainerManagerComponent?>(goldfish, manager), "food", out _, out var food), Is.True);
            Assert.That(food!.GetTotalPrototypeQuantity("Protein").Float(), Is.GreaterThan(0f));
            Assert.That(food.GetTotalPrototypeQuantity("Blood").Float(), Is.GreaterThan(0f));

            var flavor = SEntMan.GetComponent<FlavorProfileComponent>(goldfish);
            Assert.That(flavor.Flavors, Does.Contain("raw-fish"));

            // tg adds salt to every saltwater fish.
            Assert.That(SEntMan.TryGetComponent<SolutionContainerManagerComponent>(monkfish, out var monkfishManager), Is.True);
            Assert.That(solutions.TryGetSolution(new Entity<SolutionContainerManagerComponent?>(monkfish, monkfishManager), "food", out _, out var monkfishFood), Is.True);
            Assert.That(monkfishFood!.GetTotalPrototypeQuantity("TableSalt").Float(), Is.GreaterThan(0f));

            // tg holographic fish override make_edible and stay inedible.
            Assert.That(SEntMan.HasComponent<EdibleComponent>(holo), Is.False);
        });
    }
}
