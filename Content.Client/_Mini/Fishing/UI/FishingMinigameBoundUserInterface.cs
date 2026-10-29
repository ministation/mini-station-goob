// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using JetBrains.Annotations;

namespace Content.Client._Mini.Fishing.UI;

/// <summary>
/// There is no window and no control here on purpose. The minigame is drawn by the overlay in the
/// world next to the float, and the wheel input is owned by <see cref="FishingSystem"/> so it can
/// exist only during the bite window and the fight.
/// </summary>
[UsedImplicitly]
public sealed class FishingMinigameBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
}
