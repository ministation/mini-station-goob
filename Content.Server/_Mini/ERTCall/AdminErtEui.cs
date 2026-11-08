// SPDX-License-Identifier: LicenseRef-MiniStation-1.0


using Content.Server.EUI;
using Content.Shared._Mini.ERT;
using Content.Shared.Eui;

namespace Content.Server._Mini.ERT;

public sealed class AdminErtEui : BaseEui
{
    public override void Opened()
    {
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        return new AdminErtEuiState();
    }

    // no messages to handle for now
}
