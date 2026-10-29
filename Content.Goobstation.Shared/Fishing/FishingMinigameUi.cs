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

/// <summary>
/// Sent by the client for every reel input (1 = reel up, -1 = feed down, 0 = stop).
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
