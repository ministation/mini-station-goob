// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared.Actions;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

using Content.Shared._Mini.Fishing.Prototypes;

namespace Content.Shared._Mini.Fishing.Events;

public sealed partial class ThrowFishingLureActionEvent : WorldTargetActionEvent;

public sealed partial class PullFishingLureActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed class ActiveFishingSpotComponentState : ComponentState
{
    public readonly float FishDifficulty;
    public bool IsActive;
    public TimeSpan? FishingStartTime;
    public NetEntity? AttachedFishingLure;

    public ActiveFishingSpotComponentState(float fishDifficulty, bool isActive, TimeSpan? fishingStartTime, NetEntity? attachedFishingLure)
    {
        FishDifficulty = fishDifficulty;
        IsActive = isActive;
        FishingStartTime = fishingStartTime;
        AttachedFishingLure = attachedFishingLure;
    }
}

// Mini/Orion: both the upstream entity payload and the Mini prototype payload are
// carried in one event so all raise sites keep compiling.
[ByRefEvent]
public readonly record struct FishCaughtEvent(EntityUid? Fish, EntProtoId? FishId)
{
    public FishCaughtEvent(EntityUid fish) : this(fish, null) { }

    public FishCaughtEvent(EntProtoId fishId) : this(null, fishId) { }
}

/// <summary>
/// Raised on the fishing rod right before the minigame starts so equipment can tweak it.
/// </summary>
[ByRefEvent]
public struct FishFightModifiersEvent
{
    public float Difficulty;

    public float CompletionGain = 5f;

    public float CompletionLoss = 6f;

    public float BaitSpeed = 1f;

    public float BaitHeight = 1f;

    public float Gravity = 1f;

    public float Bounciness = 1f;

    public float Deceleration = 1f;

    public FishingMinigameRules Rules = FishingMinigameRules.None;

    public FishFightModifiersEvent(float difficulty)
    {
        Difficulty = difficulty;
    }
}

/// <summary>
/// Raised on the fishing rod when a catch is rolled, before the Loot is picked.
/// </summary>
[ByRefEvent]
public struct FishLootModifiersEvent
{
    public EntityUid User;
    public EntityUid Rod;
    public ProtoId<FishingSourcePrototype> Source;

    /// <summary>
    /// Multiplier applied to the weight of fish entries in the loot table.
    /// </summary>
    public float FishWeightMultiplier = 1f;

    /// <summary>
    /// How many extra times an empty roll is rerolled. Better bait provides rerolls.
    /// </summary>
    public int DudRerolls;

    /// <summary>
    /// Chance to catch an additional item on top of the normal catch.
    /// </summary>
    public float ExtraCatchChance;

    public FishLootModifiersEvent(EntityUid user, EntityUid rod, ProtoId<FishingSourcePrototype> source)
    {
        User = user;
        Rod = rod;
        Source = source;
    }
}
/// <summary>
/// Raised on the fishing rod after a successful catch, so equipment can be consumed.
/// </summary>
[ByRefEvent]
public struct FishCaughtWithRodEvent
{
    public EntityUid User;
    public EntProtoId? Reward;

    public FishCaughtWithRodEvent(EntityUid user, EntProtoId? reward)
    {
        User = user;
        Reward = reward;
    }
}
/// <summary>
/// Raised on the fisher after a won minigame, regardless of what was pulled out of the water.
/// Minigame outcomes are not always fish, and other catch events are already taken by achievements.
/// </summary>
public sealed class FishingCatchCompletedEvent : EntityEventArgs
{
    public readonly EntityUid User;
    public readonly EntProtoId? Reward;

    public FishingCatchCompletedEvent(EntityUid user, EntProtoId? reward)
    {
        User = user;
        Reward = reward;
    }
}
/// <summary>
/// Raised on the entity a dynamic fishing source is attached to (vending machine, tray, aquarium...)
/// so the host can provide its own loot. Set Handled to true even when the host has nothing.
/// </summary>
[ByRefEvent]
public struct FishingDynamicRewardEvent
{
    public EntityUid User;
    public EntityUid Rod;
    public EntityUid SourceEntity;

    public List<EntProtoId> Candidates = new();
    public EntityUid? ExistingCatch;
    public bool Handled;

    public FishingDynamicRewardEvent(EntityUid user, EntityUid rod, EntityUid sourceEntity)
    {
        User = user;
        Rod = rod;
        SourceEntity = sourceEntity;
    }
}
