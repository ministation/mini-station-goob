// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Administration;
using Content.Server.Database;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server.Administration.Commands;

/// <summary>
///     Mini: показывает аккаунты, чей последний известный IP/HWID совпадает с указанным игроком,
///     и их активные серверные баны. Вспомогательная команда для системы связки банов.
/// </summary>
[AdminCommand(AdminFlags.Ban)]
public sealed class FindAltsCommand : LocalizedCommands
{
    [Dependency] private readonly IPlayerLocator _locator = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override string Command => "findalts";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine(Help);
            return;
        }

        var located = await _locator.LookupIdByNameOrIdAsync(args[0]);
        if (located == null)
        {
            shell.WriteError(Loc.GetString("cmd-findalts-player-not-found", ("target", args[0])));
            return;
        }

        var players = await _db.GetPlayersWithLastConnectionAsync();

        const int maxOutput = 50;
        var matches = players
            .Where(p => p.UserId != located.UserId)
            .Where(p => p.MatchesConnection(
                located.LastAddress,
                located.LastLegacyHWId,
                located.LastModernHWIds,
                matchIp: true,
                matchHwid: true))
            .OrderByDescending(p => p.LastSeenTime)
            .Take(maxOutput)
            .ToList();

        if (matches.Count == 0)
        {
            shell.WriteLine(Loc.GetString("cmd-findalts-none", ("target", located.Username)));
            return;
        }

        shell.WriteLine(Loc.GetString("cmd-findalts-header",
            ("target", located.Username),
            ("count", matches.Count)));

        foreach (var alt in matches)
        {
            var banText = alt.ActiveBan == null
                ? Loc.GetString("cmd-findalts-ban-none")
                : Loc.GetString("cmd-findalts-ban",
                    ("banId", alt.ActiveBan.BanId),
                    ("reason", alt.ActiveBan.Reason));

            shell.WriteLine(Loc.GetString("cmd-findalts-entry",
                ("player", alt.LastSeenUserName),
                ("userId", alt.UserId),
                ("lastSeen", alt.LastSeenTime.ToString("g")),
                ("ban", banText)));
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var options = _playerManager.Sessions.Select(c => c.Name).OrderBy(c => c).ToArray();
            return CompletionResult.FromHintOptions(options, Loc.GetString("cmd-findalts-hint"));
        }

        return CompletionResult.Empty;
    }
}
