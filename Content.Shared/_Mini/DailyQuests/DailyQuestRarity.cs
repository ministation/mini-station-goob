// SPDX-FileCopyrightText: 2026 Casha
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT
using Robust.Shared.Maths;

namespace Content.Shared._Mini.DailyQuests;

/// <summary>
/// Brawl Stars-style quest rarities. Higher tier = harder targets, better rewards, lower drop weight.
/// </summary>
public enum DailyQuestRarity : byte
{
    Rare = 0,
    SuperRare = 1,
    Epic = 2,
    Mythic = 3,
    Legendary = 4,
}

public static class DailyQuestRarityExtensions
{
    public static float GetDropWeight(this DailyQuestRarity rarity) => rarity switch
    {
        DailyQuestRarity.Rare => 1.2f,
        DailyQuestRarity.SuperRare => 0.85f,
        DailyQuestRarity.Epic => 0.55f,
        DailyQuestRarity.Mythic => 0.3f,
        DailyQuestRarity.Legendary => 0.12f,
        _ => 1f,
    };

    public static int GetDefaultReward(this DailyQuestRarity rarity) => rarity switch
    {
        DailyQuestRarity.Rare => 1,
        DailyQuestRarity.SuperRare => 2,
        DailyQuestRarity.Epic => 3,
        DailyQuestRarity.Mythic => 4,
        DailyQuestRarity.Legendary => 5,
        _ => 1,
    };

    public static Color GetColor(this DailyQuestRarity rarity) => rarity switch
    {
        DailyQuestRarity.Rare => Color.FromHex("#85f851"),
        DailyQuestRarity.SuperRare => Color.FromHex("#69b4f2"),
        DailyQuestRarity.Epic => Color.FromHex("#802cd0"),
        DailyQuestRarity.Mythic => Color.FromHex("#b6323c"),
        DailyQuestRarity.Legendary => Color.FromHex("#f9fc60"),
        _ => Color.White,
    };

    public static string GetLocId(this DailyQuestRarity rarity) => rarity switch
    {
        DailyQuestRarity.Rare => "daily-quest-rarity-rare",
        DailyQuestRarity.SuperRare => "daily-quest-rarity-super-rare",
        DailyQuestRarity.Epic => "daily-quest-rarity-epic",
        DailyQuestRarity.Mythic => "daily-quest-rarity-mythic",
        DailyQuestRarity.Legendary => "daily-quest-rarity-legendary",
        _ => "daily-quest-rarity-rare",
    };
}
