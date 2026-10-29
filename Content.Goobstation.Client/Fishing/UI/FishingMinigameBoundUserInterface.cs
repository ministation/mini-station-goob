// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Goobstation.Shared.Fishing;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Goobstation.Client.Fishing.UI;

[UsedImplicitly]
public sealed class FishingMinigameBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private FishingMinigameWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<FishingMinigameWindow>();
        _window.OnReel += direction => SendMessage(new FishingMinigameReelMessage(direction));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is FishingMinigameBoundUserInterfaceState fishingState)
            _window?.UpdateState(fishingState);
    }
}
