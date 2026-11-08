// SPDX-License-Identifier: LicenseRef-MiniStation-1.0


using Content.Client.Administration.UI.Tabs.AdminTab;
using Content.Client.Eui;
using Content.Shared._Mini.ERT;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._Mini.ERT;

[UsedImplicitly]
public sealed class AdminErtEui : BaseEui
{
    private readonly ERTCallWindow _window;

    public AdminErtEui()
    {
        _window = new ERTCallWindow();
        _window.OnClose += () => SendMessage(new AdminErtEuiMsg.Close());
    }

    public override void Opened()
    {
        _window.OpenCentered();
    }

    public override void Closed()
    {
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        // no state currently
    }
}
