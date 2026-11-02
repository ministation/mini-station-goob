// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

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

    /// <summary>
    /// Shiny hooks make fish with the shiny lover trait easier to find and catch.
    /// </summary>
    [DataField]
    public bool AttractsShinyLovers;

    /// <summary>
    /// Whether this hook can grab living creatures, not only items. The rescue and jawed hooks can.
    /// </summary>
    [DataField]
    public bool CanHookLiving;

    /// <summary>
    /// Whether this hook can be used for regular fishing. The rescue hook cannot.
    /// </summary>
    [DataField]
    public bool CanFish = true;

    /// <summary>
    /// tg's magnet hook removes duds from the loot table entirely instead of only rerolling them.
    /// </summary>
    [DataField]
    public bool RemoveDuds;

    /// <summary>
    /// Set while an anomaly core sits in the hook's core slot, drives its overlay.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AnomalyCoreInstalled;
}

/// <summary>
/// Extra effect an anomaly core grants to the anomalous lure hook.
/// </summary>
public enum FishingAnomalyEffect : byte
{
    None,
    /// <summary>
    /// Gravity core: the lure sinks slower in the minigame.
    /// </summary>
    Gravity,
    /// <summary>
    /// Bluespace core: the catch may inherit a random material composition.
    /// </summary>
    DimensionalMaterials,
    /// <summary>
    /// Flesh core: the catch may be resized and gain a random fish trait.
    /// </summary>
    Bioscramble,
}

/// <summary>
/// Visualizer keys for the anomalous lure hook overlay.
/// </summary>
[Serializable, NetSerializable]
public enum FishingAnomalyCoreVisuals : byte
{
    Installed,
}
