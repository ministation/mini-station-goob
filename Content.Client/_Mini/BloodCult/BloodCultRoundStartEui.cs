// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Client.Eui;

namespace Content.Client._Mini.BloodCult;

public sealed class BloodCultRoundStartEui : BaseEui
{
    private readonly BloodCultRoundStartMenu _menu;

    public BloodCultRoundStartEui() => _menu = new BloodCultRoundStartMenu();

    public override void Opened() => _menu.OpenCentered();

    public override void Closed()
    {
        base.Closed();
        _menu.Close();
    }
}
