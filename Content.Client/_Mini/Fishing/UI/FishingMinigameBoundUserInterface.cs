// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Mini.Fishing.UI;

/// <summary>
/// There is no window any more: the minigame bar is drawn in the world next to the float by the overlay,
/// and this BUI only owns the invisible input control that collects mouse wheel notches.
/// </summary>
[UsedImplicitly]
public sealed partial class FishingMinigameBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private IUserInterfaceManager _ui = default!;

    private FishingMinigameInputControl? _input;

    protected override void Open()
    {
        base.Open();

        _input = new FishingMinigameInputControl(Owner);
        _input.OnReel += direction => SendMessage(new FishingMinigameReelMessage(direction));
        _ui.StateRoot.AddChild(_input);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing || _input == null)
            return;

        if (_input.Parent != null)
            _input.Orphan();

        _input.Dispose();
        _input = null;
    }
}
