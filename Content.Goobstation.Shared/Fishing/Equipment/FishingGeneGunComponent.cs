// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.Fishing.Components;

/// <summary>
/// Extracts fish traits into an empty syringe and injects stored traits into other fish.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishingGeneGunComponent : Component
{
    public const string InjectorSlotId = "injector";

    /// <summary>
    /// Fish the player is extracting a trait from, waiting for the trait picker.
    /// </summary>
    [ViewVariables]
    public EntityUid? PendingFish;

    /// <summary>
    /// Player that opened the trait picker.
    /// </summary>
    [ViewVariables]
    public EntityUid? PendingUser;
}
