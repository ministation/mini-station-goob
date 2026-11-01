// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Goobstation.Maths.FixedPoint;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Mini.Fishing;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Nutrition.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

[TestFixture]
public sealed class FishCookingTest : InteractionTest
{
    private static readonly ProtoId<ReagentPrototype> Protein = "Protein";
    private static readonly ProtoId<ReagentPrototype> Blood = "Blood";

    [Test]
    public async Task CookingRemovesBloodAndDoublesProtein()
    {
        var fish = SEntMan.GetEntity(await Spawn("FishGoldfish"));

        await Server.WaitPost(() =>
        {
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(fish, "food", out _, out var solution), Is.True);
            var rawProtein = solution.GetReagentQuantity(new ReagentId(Protein.Id, null));
            var rawBlood = solution.GetReagentQuantity(new ReagentId(Blood.Id, null));
            Assert.That(rawProtein, Is.GreaterThan(FixedPoint2.Zero));
            Assert.That(rawBlood, Is.GreaterThan(FixedPoint2.Zero));

            var cooking = SEntMan.System<FishCookingSystem>();
            var fishComp = SEntMan.GetComponent<FishComponent>(fish);
            cooking.CookFish(fish, fishComp);

            Assert.That(fishComp.Cooked, Is.True);
            Assert.That(fishComp.Alive, Is.False);

            Assert.That(solutions.TryGetSolution(fish, "food", out _, out var cookedSolution), Is.True);
            Assert.That(cookedSolution.GetReagentQuantity(new ReagentId(Blood.Id, null)), Is.EqualTo(FixedPoint2.Zero));
            Assert.That(cookedSolution.GetReagentQuantity(new ReagentId(Protein.Id, null)), Is.EqualTo(rawProtein * FixedPoint2.New(2f)));

            var flavor = SEntMan.GetComponent<FlavorProfileComponent>(fish);
            Assert.That(flavor.Flavors, Does.Contain("cooked-fish"));

            // Cooking an already cooked fish does not double the protein again.
            cooking.CookFish(fish, fishComp);
            Assert.That(solutions.TryGetSolution(fish, "food", out _, out var again), Is.True);
            Assert.That(again.GetReagentQuantity(new ReagentId(Protein.Id, null)), Is.EqualTo(rawProtein * FixedPoint2.New(2f)));
        });
    }
}
