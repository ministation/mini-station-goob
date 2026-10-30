// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Shared.GameStates;

namespace Content.Shared._Mini.Fishing.Components;

/// <summary>
/// A hook that can be slotted into a fishing rod.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishingHookComponent : Component
{
    /// <summary>
    /// Sprite state of the hook overlay on the rod.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string RodOverlayState = "hook_overlay";

    /// <summary>
    /// Multiplier for the chance to catch a fish instead of junk.
    /// </summary>
    [DataField]
    public float FishWeightMultiplier = 1f;

    /// <summary>
    /// Extra rerolls when the loot table rolls nothing.
    /// </summary>
    [DataField]
    public int DudRerolls;

    /// <summary>
    /// Extra minigame rules granted by this hook.
    /// </summary>
    [DataField]
    public FishingMinigameRules Rules = FishingMinigameRules.None;

    /// <summary>
    /// Multiplier for how bouncy the bait is at the edges of the minigame bar.
    /// </summary>
    [DataField]
    public float BouncinessMultiplier = 1f;

    /// <summary>
    /// Multiplier for the gravity pulling the bait down during the minigame.
    /// </summary>
    [DataField]
    public float GravityMultiplier = 1f;
}
