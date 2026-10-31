using System;
using System.Linq;

using Content.Shared._Mini.DailyQuests;
using Content.Shared._Mini.Fishing;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

/// <summary>
/// Fishing mode scoring: the fish prototype rarity weight must map to the achievement
/// rarity tiers and a rarer fish must be worth more points.
/// </summary>
[TestFixture]
public sealed class FishingModeRarityTest
{
    [Test]
    public void WeightMapsToRarityTier()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FishingModeRarity.GetTier(1000), Is.EqualTo(DailyQuestRarity.Rare), "default weight is common");
            Assert.That(FishingModeRarity.GetTier(401), Is.EqualTo(DailyQuestRarity.Rare));
            Assert.That(FishingModeRarity.GetTier(400), Is.EqualTo(DailyQuestRarity.SuperRare));
            Assert.That(FishingModeRarity.GetTier(200), Is.EqualTo(DailyQuestRarity.Epic));
            Assert.That(FishingModeRarity.GetTier(50), Is.EqualTo(DailyQuestRarity.Mythic));
            Assert.That(FishingModeRarity.GetTier(0), Is.EqualTo(DailyQuestRarity.Legendary), "weight 0 is never rolled, it counts as the top tier");
        });
    }

    [Test]
    public void RarerTiersAreWorthMorePoints()
    {
        var tiers = Enum.GetValues<DailyQuestRarity>();
        var points = tiers.Select(FishingModeRarity.GetPoints).ToArray();

        for (var i = 1; i < points.Length; i++)
        {
            Assert.That(points[i], Is.GreaterThan(points[i - 1]),
                $"{tiers[i]} must score more than {tiers[i - 1]}");
        }
    }
}
