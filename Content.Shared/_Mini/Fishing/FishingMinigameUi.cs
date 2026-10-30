// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._Mini.Fishing;

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

/// <summary>
/// Client to server reel input for the minigame: 1 reels up, -1 feeds line down.
/// A plain network event because the minigame has no window, it is drawn by the client overlay.
/// </summary>
[Serializable, NetSerializable]
public sealed class FishingReelInputEvent : EntityEventArgs
{
    public int Direction;

    public FishingReelInputEvent(int direction)
    {
        Direction = direction;
    }
}
