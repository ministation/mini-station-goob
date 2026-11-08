// SPDX-License-Identifier: LicenseRef-MiniStation-1.0
// Mechanics reference: tgstation/tgstation (code/modules/fishing).

using Robust.Shared.GameStates;

namespace Content.Shared._Mini.Fishing.Components;

/// <summary>
/// Bookkeeping for equipment driven stat changes on a fishing rod.
/// </summary>
[RegisterComponent]
public sealed partial class FishingRodEquipmentComponent : Component
{
    /// <summary>
    /// Rod casting range without any line slotted.
    /// </summary>
    [DataField]
    public float BaseCastRange;

    [DataField]
    public bool Initialized;
}
