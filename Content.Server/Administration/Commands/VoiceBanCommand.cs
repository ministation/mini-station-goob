// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Ban)]
public sealed class VoiceBanCommand : LocalizedCommands
{
    [Dependency] private readonly IPlayerLocator _locator = default!;
    [Dependency] private readonly IBanManager _bans = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly ILogManager _logManager = default!;

    public override string Command => "voiceban";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        string target;
        string reason;
        uint minutes;

        if (!Enum.TryParse(_cfg.GetCVar(CCVars.VoiceBanDefaultSeverity), true, out NoteSeverity severity))
        {
            _logManager.GetSawmill("admin.voice_ban")
                .Warning("Voice ban severity could not be parsed from config! Defaulting to medium.");
            severity = NoteSeverity.Medium;
        }

        switch (args.Length)
        {
            case 2:
                target = args[0];
                reason = args[1];
                minutes = 0;
                break;
            case 3:
                target = args[0];
                reason = args[1];

                if (!uint.TryParse(args[2], out minutes))
                {
                    shell.WriteLine(Loc.GetString("cmd-voiceban-invalid-minutes", ("minutes", args[2])));
                    shell.WriteLine(Help);
                    return;
                }

                break;
            case 4:
                target = args[0];
                reason = args[1];

                if (!uint.TryParse(args[2], out minutes))
                {
                    shell.WriteLine(Loc.GetString("cmd-voiceban-invalid-minutes", ("minutes", args[2])));
                    shell.WriteLine(Help);
                    return;
                }

                if (!Enum.TryParse(args[3], ignoreCase: true, out severity))
                {
                    shell.WriteLine(Loc.GetString("cmd-voiceban-invalid-severity", ("severity", args[3])));
                    shell.WriteLine(Help);
                    return;
                }

                break;
            default:
                shell.WriteLine(Loc.GetString("cmd-voiceban-invalid-arguments"));
                shell.WriteLine(Help);
                return;
        }

        var located = await _locator.LookupIdByNameOrIdAsync(target);

        if (located == null)
        {
            shell.WriteError(Loc.GetString("cmd-voiceban-player"));
            return;
        }

        var banInfo = new CreateVoiceBanInfo(reason);
        banInfo.WithBanningAdmin(shell.Player?.UserId);
        banInfo.AddUser(located.UserId, located.Username);
        banInfo.AddHWId(located.LastHWId);
        if (minutes > 0)
            banInfo.WithMinutes(minutes);
        banInfo.WithSeverity(severity);

        _bans.CreateVoiceBan(banInfo);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var options = _playerManager.Sessions.Select(c => c.Name).OrderBy(c => c).ToArray();
            return CompletionResult.FromHintOptions(options, Loc.GetString("cmd-voiceban-hint"));
        }

        if (args.Length == 2)
            return CompletionResult.FromHint(Loc.GetString("cmd-voiceban-hint-reason"));

        if (args.Length == 3)
        {
            var durations = new CompletionOption[]
            {
                new("0", Loc.GetString("cmd-ban-hint-duration-1")),
                new("1440", Loc.GetString("cmd-ban-hint-duration-2")),
                new("4320", Loc.GetString("cmd-ban-hint-duration-3")),
                new("10080", Loc.GetString("cmd-ban-hint-duration-4")),
                new("20160", Loc.GetString("cmd-ban-hint-duration-5")),
                new("43800", Loc.GetString("cmd-ban-hint-duration-6")),
            };

            return CompletionResult.FromHintOptions(durations, Loc.GetString("cmd-voiceban-hint-duration"));
        }

        if (args.Length == 4)
        {
            var severities = new CompletionOption[]
            {
                new("none", Loc.GetString("admin-note-editor-severity-none")),
                new("minor", Loc.GetString("admin-note-editor-severity-low")),
                new("medium", Loc.GetString("admin-note-editor-severity-medium")),
                new("high", Loc.GetString("admin-note-editor-severity-high")),
            };

            return CompletionResult.FromHintOptions(severities, Loc.GetString("cmd-voiceban-hint-severity"));
        }

        return CompletionResult.Empty;
    }
}
