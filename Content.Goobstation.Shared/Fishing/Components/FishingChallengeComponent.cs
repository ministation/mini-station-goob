// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Goobstation.Shared.Fishing.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.Fishing.Components;

/// <summary>
/// Full state of a fishing challenge (waiting, biting or the minigame) on the fisherman.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishingChallengeComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Rod;

    [DataField, AutoNetworkedField]
    public EntityUid? Lure;

    [DataField]
    public ProtoId<FishingSourcePrototype> Source = default!;

    [DataField]
    public EntityUid? SourceEntity;

    [DataField]
    public EntityCoordinates CastCoordinates;

    [DataField, AutoNetworkedField]
    public FishingPhase Phase = FishingPhase.Waiting;

    /// <summary>
    /// When the current phase ends.
    /// </summary>
    [DataField]
    public TimeSpan NextPhaseTime;

    /// <summary>
    /// Rolled reward, either a fish entity prototype or any other item.
    /// </summary>
    [DataField]
    public EntProtoId? Reward;

    // Minigame state.

    [DataField, AutoNetworkedField]
    public float Completion = 30f;

    [DataField, AutoNetworkedField]
    public float BaitPosition;

    [DataField, AutoNetworkedField]
    public float FishPosition;

    [DataField, AutoNetworkedField]
    public float BaitHeight = 360f;

    [DataField, AutoNetworkedField]
    public float FishHeight = 50f;

    [DataField, AutoNetworkedField]
    public string Background = "background_default";

    [DataField, AutoNetworkedField]
    public string FishIcon = "fish";

    [DataField, AutoNetworkedField]
    public string FrameState = "frame_wood";

    [DataField, AutoNetworkedField]
    public FishingMinigameRules Rules = FishingMinigameRules.None;

    /// <summary>
    /// -1 feed down, 0 idle, 1 reel up.
    /// </summary>
    [DataField]
    public int ReelDirection;

    // Difficulty and physics tuning.

    [DataField]
    public float CompletionGain = 5f;

    [DataField]
    public float CompletionLoss = 6f;

    [DataField]
    public float ReelingSpeed = 1200f;

    [DataField]
    public float Gravity = -800f;

    [DataField]
    public float Bounciness = 0.6f;

    [DataField]
    public float Deceleration = 1.8f;

    [DataField]
    public float OverlapVelocityMult = 0.6f;

    [DataField]
    public float BaitVelocity;

    [DataField]
    public float FishVelocity;

    [DataField]
    public float FishTarget;

    [DataField]
    public bool HasFishTarget;

    [DataField]
    public float FishVelocityLimit;

    [DataField]
    public int TicksFired;

    [DataField]
    public bool IsPlunging = true;

    [DataField]
    public ProtoId<FishMovementPrototype>? Movement;

    [DataField]
    public float Difficulty = 15f;

    [DataField]
    public bool IsFinishing;

    /// <summary>
    /// Minigame uses a fixed 0.05 s simulation step, same as tg.
    /// </summary>
    [DataField]
    public float TickAccumulator;

    [DataField]
    public EntityUid? ChallengeUserInterface;
}