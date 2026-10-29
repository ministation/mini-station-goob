// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Shared.Serialization;

namespace Content.Goobstation.Shared.Fishing;

/// <summary>
/// Phases of the tg fishing challenge.
/// </summary>
public enum FishingPhase : byte
{
    Idle,
    Waiting,
    Biting,
    Minigame,
}

[Serializable, NetSerializable]
public enum FishingMinigameUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class FishingMinigameBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly FishingPhase Phase;
    public readonly float Completion;
    public readonly float BaitPosition;
    public readonly float FishPosition;
    public readonly float BaitHeight;
    public readonly float FishHeight;
    public readonly string Background;
    public readonly string FishIcon;
    public readonly string FrameState;
    public readonly FishingMinigameRules Rules;

    public FishingMinigameBoundUserInterfaceState(
        FishingPhase phase,
        float completion,
        float baitPosition,
        float fishPosition,
        float baitHeight,
        float fishHeight,
        string background,
        string fishIcon,
        string frameState,
        FishingMinigameRules rules)
    {
        Phase = phase;
        Completion = completion;
        BaitPosition = baitPosition;
        FishPosition = fishPosition;
        BaitHeight = baitHeight;
        FishHeight = fishHeight;
        Background = background;
        FishIcon = fishIcon;
        FrameState = frameState;
        Rules = rules;
    }
}

/// <summary>
/// Sent by the client while the reel button is held (1 = reel up, -1 = feed down, 0 = released).
/// </summary>
[Serializable, NetSerializable]
public sealed class FishingMinigameReelMessage : BoundUserInterfaceMessage
{
    public readonly int Direction;

    public FishingMinigameReelMessage(int direction)
    {
        Direction = direction;
    }
}

/// <summary>
/// Sent when the player closes the minigame window, meaning they give up.
/// </summary>
[Serializable, NetSerializable]
public sealed class FishingMinigameStopMessage : BoundUserInterfaceMessage;
