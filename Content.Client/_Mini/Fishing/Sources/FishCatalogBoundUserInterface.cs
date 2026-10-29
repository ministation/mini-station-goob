// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Client.UserInterface;

namespace Content.Client._Mini.Fishing.Sources;

public sealed class FishCatalogBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private FishCatalogWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<FishCatalogWindow>();
    }
}
