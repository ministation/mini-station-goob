// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing;
using NUnit.Framework;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

/// <summary>
/// The bait zone is split into green, yellow and red bands, and the catch is faster the closer
/// the fish is to the middle.
/// </summary>
[TestFixture]
public sealed class FishingBaitZoneTest
{
    [Test]
    public void ZonesAreSplitInThreeBands()
    {
        Assert.That(FishingBaitZones.GetZone(0f), Is.EqualTo(FishingBaitZone.Green));
        Assert.That(FishingBaitZones.GetZone(FishingBaitZones.GreenDistance - 0.01f), Is.EqualTo(FishingBaitZone.Green));
        Assert.That(FishingBaitZones.GetZone(FishingBaitZones.GreenDistance + 0.01f), Is.EqualTo(FishingBaitZone.Yellow));
        Assert.That(FishingBaitZones.GetZone(FishingBaitZones.YellowDistance - 0.01f), Is.EqualTo(FishingBaitZone.Yellow));
        Assert.That(FishingBaitZones.GetZone(FishingBaitZones.YellowDistance + 0.01f), Is.EqualTo(FishingBaitZone.Red));
        Assert.That(FishingBaitZones.GetZone(1f), Is.EqualTo(FishingBaitZone.Red));
    }

    [Test]
    public void CompletionIsFasterNearTheCenter()
    {
        var middle = FishingBaitZones.GetCompletionMultiplier(0f);
        var greenEdge = FishingBaitZones.GetCompletionMultiplier(FishingBaitZones.GreenDistance);
        var redEdge = FishingBaitZones.GetCompletionMultiplier(1f);

        Assert.That(middle, Is.EqualTo(FishingBaitZones.GreenCompletionMultiplier).Within(0.001f),
            "The middle of the zone must give the full green bonus.");
        Assert.That(redEdge, Is.EqualTo(FishingBaitZones.RedCompletionMultiplier).Within(0.001f),
            "The edge of the zone must give the red malus.");
        Assert.That(middle, Is.GreaterThan(greenEdge));
        Assert.That(greenEdge, Is.GreaterThan(redEdge));
    }
}
