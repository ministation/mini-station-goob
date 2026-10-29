// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Shared.GameStates;
using Robust.Shared.Maths;

namespace Content.Goobstation.Shared.Fishing.Components;

/// <summary>
/// A reel of fishing line that can be slotted into a fishing rod.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishingLineComponent : Component
{
    /// <summary>
    /// Tint of the line and of the reel overlay on the rod.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color LineColor = Color.Gray;

    /// <summary>
    /// Extra casting range in tiles this line adds to the rod.
    /// </summary>
    [DataField]
    public float CastRangeBonus = 2f;

    /// <summary>
    /// Additive minigame difficulty modifier, negative values make fishing easier.
    /// </summary>
    [DataField]
    public float DifficultyModifier;

    /// <summary>
    /// Multiplier applied to the progress lost when the bait slips off.
    /// </summary>
    [DataField]
    public float CompletionLossMultiplier = 1f;

    /// <summary>
    /// Extra minigame rules granted by this line, for example <see cref="FishingMinigameRules.Autoreel"/>.
    /// </summary>
    [DataField]
    public FishingMinigameRules Rules = FishingMinigameRules.None;
}
