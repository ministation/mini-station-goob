// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;

namespace Content.Client._Mini.AntagTokens;

[AnyCommand]
public sealed class AntagTokenMenuCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "antagtokenmenu";
    public string Description => "Opens the antagonist token menu.";
    public string Help => "Usage: antagtokenmenu";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteLine(Help);
            return;
        }

        _entities.System<AntagTokenUiSystem>().RequestOpen();
    }
}
