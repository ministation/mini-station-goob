// SPDX-FileCopyrightText: 2026 Casha
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT
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
