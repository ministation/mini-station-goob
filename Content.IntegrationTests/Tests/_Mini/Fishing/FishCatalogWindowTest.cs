// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

#nullable enable

using System.Linq;
using Content.Client._Mini.Fishing.Sources;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Mini.Fishing.Prototypes;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

/// <summary>
/// The fish encyclopedia opens and lists every catalog fish.
/// </summary>
[TestFixture]
public sealed class FishCatalogWindowTest : InteractionTest
{
    private static T? Find<T>(Control root) where T : Control
    {
        if (root is T match)
            return match;

        foreach (var child in root.Children)
        {
            if (Find<T>(child) is { } found)
                return found;
        }

        return null;
    }

    [Test]
    public async Task EncyclopediaOpensAndListsEveryFish()
    {
        var expected = ProtoMan.EnumeratePrototypes<FishPrototype>().Count(fish => fish.ShowInCatalog);

        await PlaceInHands("FishCatalog");
        await UseInHand();
        await RunTicks(5);

        Assert.That(TryFindWindow(out FishCatalogWindow? window), Is.True, "The fish encyclopedia did not open.");

        var list = Find<ItemList>(window!);
        Assert.That(list, Is.Not.Null, "The catalog window has no fish list.");
        Assert.That(list!.Count, Is.EqualTo(expected), "The catalog must list every catalog fish.");
    }
}
