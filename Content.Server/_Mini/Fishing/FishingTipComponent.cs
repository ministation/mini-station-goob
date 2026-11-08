// SPDX-License-Identifier: LicenseRef-MiniStation-1.0
// Mechanics reference: tgstation/tgstation (code/modules/fishing).

namespace Content.Server._Mini.Fishing;

/// <summary>
/// A paper that gets its text on spawn: either a random fishing tip or the lure instructions.
/// </summary>
[RegisterComponent]
public sealed partial class FishingTipComponent : Component
{
    /// <summary>
    /// Use the lure instructions text instead of a random fish tip.
    /// </summary>
    [DataField]
    public bool Instructions;
}
