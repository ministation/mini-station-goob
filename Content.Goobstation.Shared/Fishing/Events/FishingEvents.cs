// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Goobstation.Shared.Fishing.Events;

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
