// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Goobstation.Shared.Fishing.Aquarium;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Goobstation.Client.Fishing.Aquarium;

[UsedImplicitly]
public sealed class AquariumBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private AquariumWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<AquariumWindow>();

        _window.OnTemperatureSelected += temperature => SendMessage(new AquariumSetTemperatureMessage(temperature));
        _window.OnFluidSelected += fluid => SendMessage(new AquariumSetFluidMessage(fluid));
        _window.OnModeSelected += mode => SendMessage(new AquariumSetModeMessage(mode));
        _window.OnFeed += () => SendMessage(new AquariumFeedMessage());
        _window.OnRemove += entity => SendMessage(new AquariumRemoveMessage(entity));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is AquariumBoundUserInterfaceState aquarium)
            _window?.UpdateState(aquarium);
    }
}
