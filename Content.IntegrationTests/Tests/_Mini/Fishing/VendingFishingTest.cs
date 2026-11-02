// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable

using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Mini.Fishing.Events;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

/// <summary>
/// Vending machines hand out weighted loot: the closer the product price is to the bait
/// value, the more likely it is, so expensive gear (captain's carapace) is astronomically
/// unlikely with regular bait, like in tg.
/// </summary>
[TestFixture]
public sealed class VendingFishingTest : InteractionTest
{
    private const string Carapace = "ClothingOuterArmorCaptainCarapace";

    [Test]
    public async Task ExpensiveProductsAreAstronomicallyUnlikely()
    {
        var machine = SEntMan.GetEntity(await Spawn("CaptainDrobe"));

        Dictionary<string, float> weights = default!;
        await Server.WaitPost(() =>
        {
            // A bare rod has no bait, so the bait value is the 1 credit floor, like tg.
            var ev = new FishingDynamicRewardEvent(SPlayer, SPlayer, machine);
            SEntMan.EventBus.RaiseLocalEvent(machine, ref ev);

            Assert.That(ev.Handled, Is.True);
            weights = ev.WeightedCandidates.ToDictionary(c => c.Key.Id, c => c.Value);
        });

        await RunTicks(5);

        Assert.That(weights, Is.Not.Empty);
        Assert.That(weights, Does.ContainKey(Carapace));

        // Carapace costs 1500, bait floor is 1 credit: weight = 1/1500 * 1000.
        Assert.That(weights[Carapace], Is.LessThan(1f), "captain's carapace must stay astronomically unlikely");

        // Unpriced products count as cheap: weight 1/100 * 1000 = 10.
        var cheap = weights.Where(w => w.Key != Carapace).ToList();
        Assert.That(cheap.Any(w => w.Value >= 10f), Is.True, "cheap products must stay common");

        var cheapestWeight = cheap.Max(w => w.Value);
        Assert.That(weights[Carapace] * 10, Is.LessThan(cheapestWeight),
            "the carapace must be at least an order of magnitude rarer than cheap products");
    }
}
