// SPDX-License-Identifier: LicenseRef-MiniStation-1.0
// Mechanics reference: tgstation/tgstation (code/modules/fishing).

namespace Content.Client._Mini.Fishing.Overlays;

/// <summary>
/// Draws only the fishing line, so it takes the lighting and never covers the minigame panel.
/// </summary>
public sealed class FishingLineOverlay : FishingOverlay
{
    protected override bool LinesOnly => true;
}
