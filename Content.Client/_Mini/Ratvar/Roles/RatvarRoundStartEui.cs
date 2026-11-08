// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Client.Eui;
using Content.Client._Mini.Ratvar.Roles;

namespace Content.Client._Mini.Ratvar.UI;

public sealed class RatvarRoundStartEui : BaseEui
{
    private readonly RatvarRoundStartMenu _menu;

    public RatvarRoundStartEui() => _menu = new RatvarRoundStartMenu();

    public override void Opened() => _menu.OpenCentered();

    public override void Closed()
    {
        base.Closed();
        _menu.Close();
    }
}
