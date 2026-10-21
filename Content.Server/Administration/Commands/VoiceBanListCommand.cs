// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration.BanList;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Shared.Console;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Ban)]
public sealed class VoiceBanListCommand : IConsoleCommand
{
    [Dependency] private readonly IServerDbManager _dbManager = default!;

    [Dependency] private readonly EuiManager _eui = default!;

    [Dependency] private readonly IPlayerLocator _locator = default!;

    public string Command => "voicebanlist";
    public string Description => Loc.GetString("cmd-voicebanlist-desc");
    public string Help => Loc.GetString("cmd-voicebanlist-help");

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 && args.Length != 2)
        {
            shell.WriteLine($"Invalid amount of args. {Help}");
            return;
        }

        var includeUnbanned = true;
        if (args.Length == 2 && !bool.TryParse(args[1], out includeUnbanned))
        {
            shell.WriteLine(Loc.GetString("cmd-voicebanlist-invalid-bool", ("arg", args[1])));
            return;
        }

        var data = await _locator.LookupIdByNameOrIdAsync(args[0]);

        if (data == null)
        {
            shell.WriteError(Loc.GetString("cmd-voicebanlist-player"));
            return;
        }

        if (shell.Player is not { } player)
        {
            var bans = await _dbManager.GetBansAsync(data.LastAddress, data.UserId, data.LastLegacyHWId, data.LastModernHWIds, includeUnbanned, type: BanType.Voice);

            if (bans.Count == 0)
            {
                shell.WriteLine(Loc.GetString("cmd-voicebanlist-empty"));
                return;
            }

            foreach (var ban in bans)
            {
                shell.WriteLine(Loc.GetString("cmd-voicebanlist-entry", ("id", ban.Id ?? 0), ("reason", ban.Reason)));
            }
            return;
        }

        var ui = new BanListEui();
        _eui.OpenEui(ui, player);
        await ui.ChangeBanListPlayer(data.UserId);
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(CompletionHelper.SessionNames(),
                Loc.GetString("cmd-voicebanlist-hint-1")),
            2 => CompletionResult.FromHintOptions(CompletionHelper.Booleans,
                Loc.GetString("cmd-voicebanlist-hint-2")),
            _ => CompletionResult.Empty
        };
    }
}
