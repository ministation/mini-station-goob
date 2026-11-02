// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

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
