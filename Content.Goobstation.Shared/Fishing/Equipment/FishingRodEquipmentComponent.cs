// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.Fishing.Components;

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
