// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

namespace Content.Client._Mini.Fishing.Overlays;

/// <summary>
/// Draws only the fishing line, so it takes the lighting and never covers the minigame panel.
/// </summary>
public sealed class FishingLineOverlay : FishingOverlay
{
    protected override bool LinesOnly => true;
}
