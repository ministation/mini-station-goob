// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

namespace Content.Shared._Mini.Fishing.Components;

/// <summary>
/// An item that makes fishing easier while it is held in a hand, like the fish encyclopedia or a fishing bag.
/// </summary>
[RegisterComponent]
public sealed partial class HeldFishingDifficultyComponent : Component
{
    /// <summary>
    /// Additive minigame difficulty modifier, negative values make fishing easier.
    /// </summary>
    [DataField]
    public float Difficulty;
}
