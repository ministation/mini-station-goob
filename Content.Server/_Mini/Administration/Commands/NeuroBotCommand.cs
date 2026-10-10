// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Mini.Administration.Commands;

[AnyCommand]
public sealed class NeuroBotCommand : IConsoleCommand
{
    [Dependency] private readonly IAdminManager _admin = default!;

    public string Command => "neurobots";
    public string Description => "Управление нейроигроками (боты с LLM-чатом).";
    public string Help => "Usage: neurobots <spawn|despawn|status>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || args[0] is not ("spawn" or "despawn" or "status"))
        {
            shell.WriteLine(Help);
            return;
        }

        // Local console or admins only.
        if (!shell.IsLocal && (shell.Player is not { } session || !_admin.IsAdmin(session, false)))
        {
            shell.WriteError("Требуются права администратора.");
            return;
        }

        var neuro = IoCManager.Resolve<IEntityManager>().System<Content.Server._Mini.NeuroPlayer.NeuroPlayerSystem>();

        switch (args[0])
        {
            case "spawn":
                neuro.SpawnBots();
                shell.WriteLine("Нейроигроки: спавн запрошен (см. лог neuroplayer).");
                break;
            case "despawn":
                neuro.DespawnBots();
                shell.WriteLine("Нейроигроки удалены.");
                break;
            case "status":
                var (enabled, configured, bots, requests) = neuro.GetStatus();
                shell.WriteLine($"enabled={enabled}, apiConfigured={configured}, bots={bots}, dailyRequests={requests}");
                break;
        }
    }
}
