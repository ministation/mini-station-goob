// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Shared.GameStates;

namespace Content.Shared._Mini.Fishing.Components;

/// <summary>
/// How good a bait is at luring fish.
/// </summary>
public enum FishingBaitQuality : byte
{
    Basic,
    Good,
    Great,
}

/// <summary>
/// What the bait is made of. Fish traits like carnivore or herbivore only bite matching bait.
/// </summary>
[Flags]
public enum FishingBaitFlags : byte
{
    None = 0,
    Meat = 1 << 0,
    Produce = 1 << 1,
    /// <summary>
    /// Lures and synthetic bait: every fish accepts it, like tg's omni bait.
    /// </summary>
    Artificial = 1 << 2,
}

/// <summary>
/// Bait or an artificial lure that can be slotted into a fishing rod.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishingBaitComponent : Component
{
    /// <summary>
    /// Sprite state of the bait overlay on the rod.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string RodOverlayState = "worm_overlay";

    [DataField]
    public FishingBaitQuality Quality = FishingBaitQuality.Basic;

    /// <summary>
    /// Artificial lures are not used up when a fish is caught.
    /// </summary>
    [DataField]
    public bool Consumable = true;

    [DataField]
    public FishingBaitFlags Flags = FishingBaitFlags.None;

    /// <summary>
    /// Seconds from the start of the wait until an artificial lure can be spun. Zero means the
    /// bait is not a lure and does not need spinning.
    /// </summary>
    [DataField]
    public float SpinFrequencyMin;

    /// <summary>
    /// Seconds from the start of the wait after which an unspun lure stops spinning and the wait
    /// grows by a few seconds.
    /// </summary>
    [DataField]
    public float SpinFrequencyMax;

    /// <summary>
    /// Fish a lure is able to catch. Null for normal bait, which is not picky about the species.
    /// </summary>
    [DataField]
    public FishingLureFilter? LureFilter;

    /// <summary>
    /// Whether the bait attracts shiny-loving fish like the shiny hook does.
    /// </summary>
    [DataField]
    public bool AttractsShinyLovers;
}
