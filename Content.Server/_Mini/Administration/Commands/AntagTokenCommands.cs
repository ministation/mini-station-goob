// SPDX-FileCopyrightText: 2026 Casha
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT
using System;
using Content.Server.Administration;
using Content.Server._Mini.AntagTokens;
using Content.Shared._Mini.AntagTokens;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;

namespace Content.Server._Mini.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class AntagTokenDatabaseSyncCommand : IConsoleCommand
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "antagtokendbsync";
    public string Description => "Reloads antagonist token state from the database for an online player (external Discord/DB changes).";
    public string Help => "Usage: antagtokendbsync [username]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!DailyRewardCommandHelpers.TryResolveSession(shell, args, _playerManager, out var session))
            return;

        var system = _entities.System<AntagTokenSystem>();
        system.RequestAntagTokenDatabaseSync(session);
        shell.WriteLine($"Requested database sync for {session.Name}.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class AntagTokenStatusCommand : IConsoleCommand
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "antagtokenstatus";
    public string Description => "Shows antagonist store state for an online player.";
    public string Help => "Usage: antagtokenstatus [username]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!DailyRewardCommandHelpers.TryResolveSession(shell, args, _playerManager, out var session))
            return;

        var system = _entities.System<AntagTokenSystem>();
        if (!system.TryGetDebugState(session.UserId, out var state))
        {
            shell.WriteError("Store state is not loaded yet.");
            return;
        }

        shell.WriteLine($"Player: {session.Name}");
        shell.WriteLine($"Balance: {state.Balance}");
        shell.WriteLine($"Sponsor level: {system.GetEffectiveSponsorLevel(session.UserId)}");
        shell.WriteLine($"Monthly cap: {AntagTokenCatalog.GetSponsorMonthlyCap(system.GetEffectiveSponsorLevel(session.UserId))?.ToString() ?? "none"}");
        shell.WriteLine($"Monthly earned: {state.MonthlyEarned}");
        shell.WriteLine($"Month: {state.MonthlyYear:D4}-{state.MonthlyMonth:D2}");
        shell.WriteLine($"Deposit: {state.PendingDepositRoleId ?? "none"}");
    }
}

[AdminCommand(AdminFlags.Host)]
public sealed class AntagTokenAddCommand : IConsoleCommand
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "antagtokenadd";
    public string Description => "Adds store tokens to an online player.";
    public string Help => "Usage: antagtokenadd [username] <amount>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!DailyRewardCommandHelpers.TryResolveSession(shell, args, _playerManager, out var session, out var consumedArgs))
            return;

        if (!int.TryParse(args[consumedArgs], out var amount) || amount <= 0)
        {
            shell.WriteError("Amount must be a positive integer.");
            return;
        }

        var system = _entities.System<AntagTokenSystem>();
        if (!system.AddBalance(session.UserId, amount, out var granted, out var note))
        {
            shell.WriteError("Failed to add balance.");
            return;
        }

        shell.WriteLine($"Granted {granted} token(s) to {session.Name}.");
        if (note != null)
            shell.WriteLine(note);
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class AntagTokenSetBalanceCommand : IConsoleCommand
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "antagtokenset";
    public string Description => "Sets exact store balance for an online player.";
    public string Help => "Usage: antagtokenset [username] <amount>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!DailyRewardCommandHelpers.TryResolveSession(shell, args, _playerManager, out var session, out var consumedArgs))
            return;

        if (!int.TryParse(args[consumedArgs], out var amount) || amount < 0)
        {
            shell.WriteError("Amount must be zero or positive.");
            return;
        }

        var system = _entities.System<AntagTokenSystem>();
        if (!system.SetBalance(session.UserId, amount))
        {
            shell.WriteError("Failed to set balance.");
            return;
        }

        shell.WriteLine($"Set balance for {session.Name} to {amount}.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class AntagTokenBuyCommand : IConsoleCommand
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "antagtokenbuy";
    public string Description => "Purchases a store role for an online player.";
    public string Help => "Usage: antagtokenbuy [username] <roleId>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!DailyRewardCommandHelpers.TryResolveSession(shell, args, _playerManager, out var session, out var consumedArgs))
            return;

        var roleId = args[consumedArgs];
        if (!_entities.System<AntagTokenListingSystem>().TryGetListing(roleId, out _))
        {
            shell.WriteError("Unknown role id.");
            return;
        }

        var system = _entities.System<AntagTokenSystem>();
        if (!system.TryPurchaseForSession(session, roleId, out var error))
        {
            shell.WriteError(error ?? "Purchase failed.");
            return;
        }

        shell.WriteLine($"Purchased {roleId} for {session.Name}.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class AntagTokenClearCommand : IConsoleCommand
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "antagtokenclear";
    public string Description => "Clears the pending deposited role for an online player and refunds it.";
    public string Help => "Usage: antagtokenclear [username]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!DailyRewardCommandHelpers.TryResolveSession(shell, args, _playerManager, out var session))
            return;

        var system = _entities.System<AntagTokenSystem>();
        if (!system.ClearDeposit(session.UserId, out var error))
        {
            shell.WriteError(error ?? "Failed to clear deposit.");
            return;
        }

        shell.WriteLine($"Cleared deposited role for {session.Name}.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class AntagTokenOpenCommand : IConsoleCommand
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "antagtokenopen";
    public string Description => "Opens the antagonist store UI for an online player.";
    public string Help => "Usage: antagtokenopen [username]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!DailyRewardCommandHelpers.TryResolveSession(shell, args, _playerManager, out var session))
            return;

        var system = _entities.System<AntagTokenSystem>();
        if (!system.TryOpenForSession(session))
        {
            shell.WriteError("Failed to open antagonist store UI for that player.");
            return;
        }

        shell.WriteLine($"Opened antagonist store UI for {session.Name}.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class AntagTokenSetSponsorCommand : IConsoleCommand
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "antagtokensetsponsor";
    public string Description => "Sets a local debug sponsor level override for antagonist token cap tests.";
    public string Help => "Usage: antagtokensetsponsor [username] <level|clear>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!DailyRewardCommandHelpers.TryResolveSession(shell, args, _playerManager, out var session, out var consumedArgs))
            return;

        int? sponsorLevel = null;
        var value = args[consumedArgs];
        if (!value.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(value, out var parsed) || parsed is < 0 or > 5)
            {
                shell.WriteError("Sponsor level must be 0-5 or 'clear'.");
                return;
            }

            sponsorLevel = parsed;
        }

        var system = _entities.System<AntagTokenSystem>();
        system.SetSponsorLevelOverride(session.UserId, sponsorLevel);
        shell.WriteLine(sponsorLevel == null
            ? $"Cleared sponsor override for {session.Name}."
            : $"Set sponsor override for {session.Name} to {sponsorLevel}.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class AntagTokenSetMonthlyEarnedCommand : IConsoleCommand
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "antagtokensetmonth";
    public string Description => "Sets current month earned token amount for antagonist token cap tests.";
    public string Help => "Usage: antagtokensetmonth [username] <earned>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!DailyRewardCommandHelpers.TryResolveSession(shell, args, _playerManager, out var session, out var consumedArgs))
            return;

        if (!int.TryParse(args[consumedArgs], out var earned) || earned < 0)
        {
            shell.WriteError("Earned amount must be zero or positive.");
            return;
        }

        var system = _entities.System<AntagTokenSystem>();
        if (!system.SetMonthlyEarned(session.UserId, earned))
        {
            shell.WriteError("Failed to set monthly earned amount.");
            return;
        }

        shell.WriteLine($"Set monthly earned for {session.Name} to {earned}.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class AntagTokenStoreCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "antagtokenstore";
    public string Description => "Enables or disables the antagonist token menu and role выдачу.";
    public string Help => "Usage: antagtokenstore <on|off|status>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine(Help);
            return;
        }

        var system = _entities.System<AntagTokenSystem>();
        switch (args[0].ToLowerInvariant())
        {
            case "on":
                system.SetStoreEnabled(true);
                shell.WriteLine("Antagonist token menu enabled.");
                break;
            case "off":
                system.SetStoreEnabled(false);
                shell.WriteLine("Antagonist token menu disabled. UI opening and antagonist issuance are blocked.");
                break;
            case "status":
                shell.WriteLine(system.IsStoreEnabled()
                    ? "Antagonist token menu is enabled."
                    : "Antagonist token menu is disabled.");
                break;
            default:
                shell.WriteLine(Help);
                break;
        }
    }
}
