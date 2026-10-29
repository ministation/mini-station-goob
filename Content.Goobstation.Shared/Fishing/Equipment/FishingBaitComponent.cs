// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.Fishing.Components;

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
}
