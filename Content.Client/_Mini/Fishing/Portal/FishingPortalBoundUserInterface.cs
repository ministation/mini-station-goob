// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;

namespace Content.Client._Mini.Fishing.Portal;

[UsedImplicitly]
public sealed partial class FishingPortalBoundUserInterface : BoundUserInterface
{
    [Dependency] private IClyde _displayManager = default!;
    [Dependency] private IInputManager _inputManager = default!;

    private FishingPortalRadialMenu? _window;

    public FishingPortalBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<FishingPortalRadialMenu>();
        _window.OnSelected += sourceId =>
        {
            SendMessage(new FishingPortalSelectMessage(sourceId));
            _window.Close();
        };

        var vpSize = _displayManager.ScreenSize;
        _window.OpenCenteredAt(_inputManager.MouseScreenPosition.Position / vpSize);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is FishingPortalBoundUserInterfaceState portalState)
            _window?.SetOptions(portalState.Options);
    }
}