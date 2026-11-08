// SPDX-License-Identifier: LicenseRef-MiniStation-1.0
// Mechanics reference: tgstation/tgstation (code/modules/fishing).

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
