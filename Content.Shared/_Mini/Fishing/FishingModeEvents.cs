// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

// SPDX-FileCopyrightText: 2026 Mini Station
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

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
    /// Coins awarded for this place at round end, zero outside the rewarded places.
    /// </summary>
    public readonly int Reward;

    public FishingModeLeader(string name, int fish, int score, int reward)
    {
        Name = name;
        Fish = fish;
        Score = score;
        Reward = reward;
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
