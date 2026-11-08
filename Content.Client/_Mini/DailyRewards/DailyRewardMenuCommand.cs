// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Localization;

namespace Content.Client._Mini.DailyRewards;

[AnyCommand]
public sealed class DailyRewardMenuCommand : LocalizedEntityCommands
{
    public override string Command => "dailyrewardmenu";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteLine(Help);
            return;
        }

        EntityManager.System<DailyRewardUiSystem>().RequestOpen();
        shell.WriteLine(Loc.GetString("daily-reward-window-opening"));
    }
}
