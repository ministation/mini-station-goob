// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Shared.Serialization;

using Content.Shared._Mini.DailyQuests;

namespace Content.Shared._Mini.Fishing;

/// <summary>
/// One row of the fishing mode leaderboard: the three leaders in the HUD and the
/// full list in the round end manifest.
/// </summary>
[Serializable, NetSerializable]
public readonly struct FishingModeLeader
{
    public readonly string Name;
    public readonly int Fish;
    public readonly int Score;

    /// <summary>
    /// Name of the rarest fish this player caught, empty when they caught nothing.
    /// </summary>
    public readonly string BestFish;

    public readonly DailyQuestRarity BestFishRarity;

    public FishingModeLeader(string name, int fish, int score, string bestFish, DailyQuestRarity bestFishRarity)
    {
        Name = name;
        Fish = fish;
        Score = score;
        BestFish = bestFish;
        BestFishRarity = bestFishRarity;
    }
}

[Serializable, NetSerializable]
public sealed class FishingModeStatusEvent : EntityEventArgs
{
    public bool Active;
    public FishingModeLeader[] Leaders;

    public FishingModeStatusEvent(bool active, FishingModeLeader[] leaders)
    {
        Active = active;
        Leaders = leaders;
    }
}

[Serializable, NetSerializable]
public sealed class FishingModeStatusRequestEvent : EntityEventArgs;

/// <summary>
/// Final table, sent before the round end window opens so the manifest can show it.
/// </summary>
[Serializable, NetSerializable]
public sealed class FishingModeRoundEndEvent : EntityEventArgs
{
    public FishingModeLeader[] Leaders;

    public FishingModeRoundEndEvent(FishingModeLeader[] leaders)
    {
        Leaders = leaders;
    }
}

/// <summary>
/// Fish rarity, the same tiers and colors as achievements and weekly quests.
/// </summary>
public static class FishingModeRarity
{
    public const int DefaultWeight = 1000;

    /// <summary>
    /// Lower fish prototype rarity weight means a rarer fish. Weight 0 is a fish that
    /// never shows up in random cases, so it counts as the top tier.
    /// </summary>
    public static DailyQuestRarity GetTier(int weight)
    {
        if (weight <= 0)
            return DailyQuestRarity.Legendary;
        if (weight <= 50)
            return DailyQuestRarity.Mythic;
        if (weight <= 200)
            return DailyQuestRarity.Epic;
        if (weight <= 400)
            return DailyQuestRarity.SuperRare;
        return DailyQuestRarity.Rare;
    }

    public static int GetPoints(DailyQuestRarity tier) => tier switch
    {
        DailyQuestRarity.Rare => 1,
        DailyQuestRarity.SuperRare => 2,
        DailyQuestRarity.Epic => 4,
        DailyQuestRarity.Mythic => 6,
        DailyQuestRarity.Legendary => 10,
        _ => 1,
    };
}
