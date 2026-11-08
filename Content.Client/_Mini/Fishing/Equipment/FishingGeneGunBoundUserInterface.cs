// SPDX-License-Identifier: LicenseRef-MiniStation-1.0
// Mechanics reference: tgstation/tgstation (code/modules/fishing).

using Content.Shared._Mini.Fishing;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Mini.Fishing.Equipment;

[UsedImplicitly]
public sealed class FishingGeneGunBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private FishingGeneGunWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<FishingGeneGunWindow>();
        _window.OnTraitSelected += trait => SendMessage(new FishingGeneGunSelectTraitMessage(trait));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is FishingGeneGunBoundUserInterfaceState cast)
            _window?.UpdateState(cast);
    }
}
