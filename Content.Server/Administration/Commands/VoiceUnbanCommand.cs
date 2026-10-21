// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Ban)]
public sealed class VoiceUnbanCommand : LocalizedCommands
{
    [Dependency] private readonly IBanManager _banManager = default!;

    public override string Command => "voiceunban";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!int.TryParse(args[0], out var banId))
        {
            shell.WriteLine(Loc.GetString("cmd-voiceunban-unable-to-parse-id", ("id", args[0]), ("help", Help)));
            return;
        }

        var response = await _banManager.PardonVoiceBan(banId, shell.Player?.UserId, DateTimeOffset.Now);
        shell.WriteLine(response);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHint(Loc.GetString("cmd-voiceunban-hint-1")),
            _ => CompletionResult.Empty
        };
    }
}
