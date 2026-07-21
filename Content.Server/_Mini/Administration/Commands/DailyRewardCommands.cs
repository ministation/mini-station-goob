// SPDX-FileCopyrightText: 2026 Casha
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT
using System;
using System.Diagnostics.CodeAnalysis;
using Content.Server.Administration;
using Content.Server.Commands;
using Content.Server.Database;
using Content.Server._Mini.DailyRewards;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Server._Mini.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class DailyRewardStatusCommand : IConsoleCommand
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "dailyrewardstatus";
    public string Description => "Shows daily reward state for an online player.";
    public string Help => "Usage: dailyrewardstatus [username]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!DailyRewardCommandHelpers.TryResolveSession(shell, args, _playerManager, out var session))
            return;

        var system = _entities.System<DailyRewardSystem>();
        if (!system.TryGetDebugState(session.UserId, out var progress))
        {
            shell.WriteError("Daily reward state is not loaded yet.");
            return;
        }

        shell.WriteLine($"Player: {session.Name}");
        shell.WriteLine($"Streak: {progress.CurrentStreak}");
        shell.WriteLine($"Last claim UTC: {progress.LastClaimTime?.ToString("O") ?? "never"}");
        shell.WriteLine($"Active date UTC: {progress.PendingActiveDate?.ToString("yyyy-MM-dd") ?? "none"}");
        shell.WriteLine($"Active time today: {progress.PendingActiveTime}");
    }
}

[AdminCommand(AdminFlags.Host)]
public sealed class DailyRewardSetStreakCommand : IConsoleCommand
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IServerDbManager _db = default!;

    public string Command => "dailyrewardsetstreak";
    public string Description => "Sets streak in DB for a player (offline ok).";
    public string Help => "Usage: dailyrewardsetstreak [username] <streak>";

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!int.TryParse(args[^1], out var streak) || streak < 0)
        {
            shell.WriteError("Streak must be a non-negative integer.");
            return;
        }

        var system = _entities.System<DailyRewardSystem>();

        if (args.Length == 2)
        {
            if (DailyRewardCommandHelpers.TryResolveOnlineSession(new[] { args[0] }, _playerManager, shell,
                    out var session, out _))
            {
                if (!system.SetStreak(session.UserId, streak))
                    shell.WriteError("Failed to update streak.");
                else
                    shell.WriteLine($"Set streak for {session.Name} to {streak}.");
                return;
            }

            var record = await _db.GetPlayerRecordByUserName(args[0]);
            if (record == null)
            {
                var normalized = DailyRewardCommandHelpers.NormalizeTarget(args[0]);
                if (normalized != args[0])
                    record = await _db.GetPlayerRecordByUserName(normalized);
            }

            if (record == null)
            {
                shell.WriteError($"No player record for '{args[0]}'.");
                return;
            }

            if (!await system.SetStreakForPlayerAsync(record.UserId, streak))
            {
                shell.WriteError("Failed to update streak.");
                return;
            }

            shell.WriteLine($"Set streak for {record.LastSeenUserName} to {streak}.");
            return;
        }

        if (shell.Player is null)
        {
            shell.WriteError("From server console use: dailyrewardsetstreak <username> <streak>");
            return;
        }

        if (!system.SetStreak(shell.Player.UserId, streak))
            shell.WriteError("Failed to update streak.");
        else
            shell.WriteLine($"Set streak for {shell.Player.Name} to {streak}.");
    }
}

[AdminCommand(AdminFlags.Host)]
public sealed class DailyRewardSetLastClaimCommand : IConsoleCommand
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "dailyrewardsetlastclaim";
    public string Description => "Sets last claim time for an online player.";
    public string Help => "Usage: dailyrewardsetlastclaim [username] <hours-ago|none>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!DailyRewardCommandHelpers.TryResolveSession(shell, args, _playerManager, out var session, out var consumedArgs))
            return;

        DateTime? lastClaim;
        if (string.Equals(args[consumedArgs], "none", StringComparison.OrdinalIgnoreCase))
        {
            lastClaim = null;
        }
        else
        {
            if (!double.TryParse(args[consumedArgs], out var hoursAgo) || hoursAgo < 0)
            {
                shell.WriteError("Value must be a non-negative number of hours or 'none'.");
                return;
            }

            lastClaim = DateTime.UtcNow - TimeSpan.FromHours(hoursAgo);
        }

        var system = _entities.System<DailyRewardSystem>();
        if (!system.SetLastClaimTime(session.UserId, lastClaim))
        {
            shell.WriteError("Failed to update last claim time.");
            return;
        }

        shell.WriteLine(lastClaim == null
            ? $"Cleared last claim time for {session.Name}."
            : $"Set last claim time for {session.Name} to {lastClaim:O} UTC.");
    }
}

[AdminCommand(AdminFlags.Host)]
public sealed class DailyRewardSetTimeCommand : IConsoleCommand
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "dailyrewardsettime";
    public string Description => "Sets today's tracked round time for an online player.";
    public string Help => "Usage: dailyrewardsettime [username] <minutes>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!DailyRewardCommandHelpers.TryResolveSession(shell, args, _playerManager, out var session, out var consumedArgs))
            return;

        if (!double.TryParse(args[consumedArgs], out var minutes) || minutes < 0)
        {
            shell.WriteError("Minutes must be a non-negative number.");
            return;
        }

        var system = _entities.System<DailyRewardSystem>();
        if (!system.SetTodayActiveTime(session.UserId, TimeSpan.FromMinutes(minutes)))
        {
            shell.WriteError("Failed to update daily reward time.");
            return;
        }

        shell.WriteLine($"Set today's active time for {session.Name} to {minutes} minute(s).");
    }
}

[AdminCommand(AdminFlags.Host)]
public sealed class DailyRewardReadyCommand : IConsoleCommand
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "dailyrewardready";
    public string Description => "Makes an online player ready to claim today's daily reward.";
    public string Help => "Usage: dailyrewardready [username]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!DailyRewardCommandHelpers.TryResolveSession(shell, args, _playerManager, out var session))
            return;

        var system = _entities.System<DailyRewardSystem>();
        if (!system.MakeReadyToClaim(session.UserId))
        {
            shell.WriteError("Failed to prepare daily reward state.");
            return;
        }

        shell.WriteLine($"{session.Name} can now claim the next daily reward.");
    }
}

[AdminCommand(AdminFlags.Host)]
public sealed class DailyRewardResetCommand : IConsoleCommand
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "dailyrewardreset";
    public string Description => "Resets daily reward progress for an online player.";
    public string Help => "Usage: dailyrewardreset [username]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!DailyRewardCommandHelpers.TryResolveSession(shell, args, _playerManager, out var session))
            return;

        var system = _entities.System<DailyRewardSystem>();
        if (!system.ResetProgress(session.UserId))
        {
            shell.WriteError("Failed to reset daily reward progress.");
            return;
        }

        shell.WriteLine($"Reset daily reward progress for {session.Name}.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class DailyRewardOpenCommand : IConsoleCommand
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "dailyrewardopen";
    public string Description => "Forces the daily reward UI to open for an online player.";
    public string Help => "Usage: dailyrewardopen [username]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!DailyRewardCommandHelpers.TryResolveSession(shell, args, _playerManager, out var session))
            return;

        var system = _entities.System<DailyRewardSystem>();
        if (!system.TryOpenForSession(session))
        {
            shell.WriteError("Failed to open daily reward UI for that player.");
            return;
        }

        shell.WriteLine($"Opened daily reward UI for {session.Name}.");
    }
}

internal static class DailyRewardCommandHelpers
{
    public static bool TryResolveSession(
        IConsoleShell shell,
        string[] args,
        IPlayerManager playerManager,
        [NotNullWhen(true)] out ICommonSession? session)
    {
        return TryResolveSession(shell, args, playerManager, out session, out _);
    }

    public static bool TryResolveSession(
        IConsoleShell shell,
        string[] args,
        IPlayerManager playerManager,
        [NotNullWhen(true)] out ICommonSession? session,
        out int consumedArgs)
    {
        consumedArgs = 0;
        session = null;

        if (args.Length == 0)
        {
            if (shell.Player is null)
            {
                shell.WriteError("Specify a player name when running this from the server console.");
                return false;
            }

            session = shell.Player;
            return true;
        }

        var rawTarget = args[0];
        var normalizedTarget = NormalizeTarget(rawTarget);

        if (shell.Player is { } self &&
            (string.Equals(rawTarget, self.Name, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(normalizedTarget, self.Name, StringComparison.OrdinalIgnoreCase)))
        {
            session = self;
            consumedArgs = 1;
            return true;
        }

        if (shell.Player is { } performer &&
            (CommandUtils.TryGetSessionByUsernameOrId(shell, rawTarget, performer, out session) ||
             (normalizedTarget != rawTarget &&
              CommandUtils.TryGetSessionByUsernameOrId(shell, normalizedTarget, performer, out session))))
        {
            consumedArgs = 1;
            return true;
        }

        if (playerManager.TryGetSessionByUsername(rawTarget, out session) ||
            (normalizedTarget != rawTarget && playerManager.TryGetSessionByUsername(normalizedTarget, out session)))
        {
            consumedArgs = 1;
            return true;
        }

        shell.WriteError($"Player '{rawTarget}' is not online.");
        return false;
    }

    public static bool TryResolveOnlineSession(
        string[] args,
        IPlayerManager playerManager,
        IConsoleShell shell,
        [NotNullWhen(true)] out ICommonSession? session,
        out int consumedArgs)
    {
        consumedArgs = 0;
        session = null;

        if (args.Length == 0)
            return false;

        var rawTarget = args[0];
        var normalizedTarget = NormalizeTarget(rawTarget);

        if (shell.Player is { } self &&
            (string.Equals(rawTarget, self.Name, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(normalizedTarget, self.Name, StringComparison.OrdinalIgnoreCase)))
        {
            session = self;
            consumedArgs = 1;
            return true;
        }

        if (shell.Player is { } performer &&
            (CommandUtils.TryGetSessionByUsernameOrId(shell, rawTarget, performer, out session) ||
             (normalizedTarget != rawTarget &&
              CommandUtils.TryGetSessionByUsernameOrId(shell, normalizedTarget, performer, out session))))
        {
            consumedArgs = 1;
            return true;
        }

        if (playerManager.TryGetSessionByUsername(rawTarget, out session) ||
            (normalizedTarget != rawTarget &&
             playerManager.TryGetSessionByUsername(normalizedTarget, out session)))
        {
            consumedArgs = 1;
            return true;
        }

        return false;
    }

    internal static string NormalizeTarget(string rawTarget)
    {
        var atIndex = rawTarget.LastIndexOf('@');
        if (atIndex >= 0 && atIndex < rawTarget.Length - 1)
            return rawTarget[(atIndex + 1)..];

        return rawTarget;
    }
}
