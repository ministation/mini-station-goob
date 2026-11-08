// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0
using System;
using Robust.Shared.Serialization;

namespace Content.Shared._Mini.DailyQuests;

[Serializable, NetSerializable]
public sealed class DailyQuestEntry
{
    public string QuestId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int CurrentProgress { get; set; }
    public int TargetProgress { get; set; }
    public int RewardCoins { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsClaimed { get; set; }
    public string? RoleHint { get; set; }
    public string? IconId { get; set; }
    public string? IconSprite { get; set; }
    public string? IconState { get; set; }
    public bool IsTimeBased { get; set; }
    /// <summary>Does the player's current job satisfy the quest's RequiredJob? False means the server has frozen progress.</summary>
    public bool RoleMatches { get; set; } = true;
    public bool CanReplace { get; set; }
    /// <summary>Coin cost of the replacement while the weekly free one is used up. 0 means free.</summary>
    public int ReplaceCostCoins { get; set; }
    public DailyQuestRarity Rarity { get; set; }
    /// <summary>UTC time when the next daily quest assignment becomes available.</summary>
    public DateTime? NextQuestResetUtc { get; set; }

    public DailyQuestEntry()
    {
    }

    public DailyQuestEntry(
        string questId,
        string title,
        string description,
        int currentProgress,
        int targetProgress,
        int rewardCoins,
        bool isCompleted,
        bool isClaimed,
        string? roleHint,
        string? iconId,
        string? iconSprite,
        string? iconState,
        bool isTimeBased,
        bool canReplace,
        DailyQuestRarity rarity = DailyQuestRarity.Rare,
        DateTime? nextQuestResetUtc = null,
        int replaceCostCoins = 0,
        bool roleMatches = true)
    {
        QuestId = questId;
        Title = title;
        Description = description;
        CurrentProgress = currentProgress;
        TargetProgress = targetProgress;
        RewardCoins = rewardCoins;
        IsCompleted = isCompleted;
        IsClaimed = isClaimed;
        RoleHint = roleHint;
        IconId = iconId;
        IconSprite = iconSprite;
        IconState = iconState;
        IsTimeBased = isTimeBased;
        RoleMatches = roleMatches;
        CanReplace = canReplace;
        ReplaceCostCoins = replaceCostCoins;
        Rarity = rarity;
        NextQuestResetUtc = nextQuestResetUtc;
    }
}

[Serializable, NetSerializable]
public sealed class DailyQuestClaimRequestEvent(string questId) : EntityEventArgs
{
    public string QuestId { get; private set; } = questId;
}
