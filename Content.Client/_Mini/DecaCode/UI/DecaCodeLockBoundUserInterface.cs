// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared._Mini.DecaCode;
using JetBrains.Annotations;

namespace Content.Client._Mini.DecaCode.UI;

[UsedImplicitly]
public sealed class DecaCodeLockBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private DecaCodeLockWindow? _window;

    public DecaCodeLockBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new DecaCodeLockWindow();
        _window.OnSubmit += code => SendMessage(new DecaCodeSubmitMessage(code));
        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not DecaCodeLockState cast)
            return;

        _window?.UpdateState(cast);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _window?.Close();
    }
}
