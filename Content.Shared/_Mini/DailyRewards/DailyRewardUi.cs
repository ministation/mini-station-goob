// SPDX-FileCopyrightText: 2026 Casha
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT
using System;
using System.Collections.Generic;
using Content.Shared._Mini.DailyQuests;
using Robust.Shared.Serialization;

namespace Content.Shared._Mini.DailyRewards;

[Serializable, NetSerializable]
public sealed class DailyRewardUpdateMessage(
    int currentStreak,
    int nextRewardDay,
    bool canClaim,
    bool isTrackingActiveTime,
    bool hasLastClaim,
    TimeSpan timeUntilExpiration,
    TimeSpan timeUntilNextClaim,
    TimeSpan currentActiveTime,
    TimeSpan requiredActiveTime,
    List<DailyRewardEntry> rewards,
    TimeSpan onlineElapsed,
    List<TimeSpan> onlineGrantedThresholds,
    List<DailyQuestEntry> dailyQuests,
    bool timeRewardsEnabled) : BoundUserInterfaceState
{
    public int CurrentStreak { get; private set; } = currentStreak;
    public int NextRewardDay { get; private set; } = nextRewardDay;
    public bool CanClaim { get; private set; } = canClaim;
    public bool IsTrackingActiveTime { get; private set; } = isTrackingActiveTime;
    public bool HasLastClaim { get; private set; } = hasLastClaim;
    public TimeSpan TimeUntilExpiration { get; private set; } = timeUntilExpiration;
    public TimeSpan TimeUntilNextClaim { get; private set; } = timeUntilNextClaim;
    public TimeSpan CurrentActiveTime { get; private set; } = currentActiveTime;
    public TimeSpan RequiredActiveTime { get; private set; } = requiredActiveTime;
    public List<DailyRewardEntry> Rewards { get; private set; } = rewards;
    public TimeSpan OnlineElapsed { get; private set; } = onlineElapsed;
    public List<TimeSpan> OnlineGrantedThresholds { get; private set; } = onlineGrantedThresholds;
    public List<DailyQuestEntry> DailyQuests { get; private set; } = dailyQuests;
    /// <summary>False while time-based rewards (daily streak, playtime) are temporarily disabled.</summary>
    public bool TimeRewardsEnabled { get; private set; } = timeRewardsEnabled;
}

[Serializable, NetSerializable]
public sealed class DailyRewardClaimMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class DailyRewardOpenRequestEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class DailyRewardClaimRequestEvent : EntityEventArgs
{
}

/// <summary>
/// Result of a daily reward claim attempt. Sent to the client so the menu can always show
/// feedback, even when the player has no attached entity (lobby) and a popup is not possible.
/// </summary>
[Serializable, NetSerializable]
public sealed class DailyRewardClaimResultEvent(
    bool granted,
    int amount,
    string? roleName,
    bool monthlyCapReached,
    string? reasonLocKey,
    TimeSpan retryIn) : EntityEventArgs
{
    public bool Granted { get; } = granted;
    public int Amount { get; } = amount;
    public string? RoleName { get; } = roleName;
    /// <summary>True if the reward was granted, but the monthly coin cap swallowed part of it.</summary>
    public bool MonthlyCapReached { get; } = monthlyCapReached;
    /// <summary>Localization id explaining why the claim was rejected.</summary>
    public string? ReasonLocKey { get; } = reasonLocKey;
    /// <summary>Cooldown or remaining playtime before the next attempt.</summary>
    public TimeSpan RetryIn { get; } = retryIn;
}

[Serializable, NetSerializable]
public sealed class DailyQuestReplaceRequestEvent : EntityEventArgs
{
    public string QuestId;
    public int SlotIndex;

    public DailyQuestReplaceRequestEvent()
    {
        QuestId = string.Empty;
        SlotIndex = -1;
    }

    public DailyQuestReplaceRequestEvent(string questId, int slotIndex)
    {
        QuestId = questId;
        SlotIndex = slotIndex;
    }
}

[Serializable, NetSerializable]
public sealed class DailyQuestReplaceDeniedEvent : EntityEventArgs
{
    public string QuestId;
    public string Reason;

    public DailyQuestReplaceDeniedEvent()
    {
        QuestId = string.Empty;
        Reason = string.Empty;
    }

    public DailyQuestReplaceDeniedEvent(string questId, string reason)
    {
        QuestId = questId;
        Reason = reason;
    }
}

[Serializable, NetSerializable]
public sealed class DailyRewardStateEvent(DailyRewardUpdateMessage state) : EntityEventArgs
{
    public DailyRewardUpdateMessage State { get; private set; } = state;
}

[Serializable, NetSerializable]
public sealed class DailyRewardEntry(
    int day,
    string? rewardName,
    bool hasReward,
    string iconPath,
    bool isClaimed,
    bool isCurrent)
{
    public int Day { get; private set; } = day;
    public string? RewardName { get; private set; } = rewardName;
    public bool HasReward { get; private set; } = hasReward;
    public string IconPath { get; private set; } = iconPath;
    public bool IsClaimed { get; private set; } = isClaimed;
    public bool IsCurrent { get; private set; } = isCurrent;
}
