// SPDX-FileCopyrightText: 2026 Casha
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT
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
