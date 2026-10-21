// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared._Mini.NeuroPlayer;
using Robust.Shared.Console;

namespace Content.Server._Mini.Administration.Commands;

[AnyCommand]
public sealed class NeuroBotCommand : IConsoleCommand
{
    [Dependency] private readonly IAdminManager _admin = default!;

    public string Command => "neurobots";
    public string Description => "Управление нейроигроками (боты с LLM-чатом).";
    public string Help =>
        "Usage:\n" +
        "  neurobots spawn|despawn|status|providers|list\n" +
        "  neurobots mute <имя|NeuroPersona|all> [минут]   - без минут глушит навсегда\n" +
        "  neurobots unmute <имя|NeuroPersona|all>\n" +
        "  neurobots test [текст]                          - живой запрос к провайдеру";

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
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

        try
        {
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
                {
                    var (enabled, configured, bots, provider, model) = neuro.GetStatus();
                    shell.WriteLine(
                        $"enabled={enabled}, apiConfigured={configured}, bots={bots}, provider={provider}, model={model}");
                    shell.WriteLine(neuro.DescribeStats());
                    break;
                }

                case "providers":
                    shell.WriteLine($"Доступные провайдеры: {NeuroProviderPresets.AvailableIds}");
                    shell.WriteLine("Выбор: cvar neuroplayer.provider, ключ: neuroplayer.api_key " +
                                    "(или neuroplayer.<провайдер>.api_key).");
                    break;

                case "list":
                    shell.WriteLine(neuro.DescribeBots());
                    break;

                case "mute":
                {
                    if (args.Length < 2)
                    {
                        shell.WriteLine("Usage: neurobots mute <имя|NeuroPersona|all> [минут]");
                        break;
                    }

                    int? minutes = null;
                    if (args.Length >= 3)
                    {
                        if (!int.TryParse(args[2], out var parsed) || parsed <= 0)
                        {
                            shell.WriteError("Минуты должны быть положительным числом.");
                            break;
                        }

                        minutes = parsed;
                    }

                    var muted = neuro.MuteBots(args[1], minutes);
                    shell.WriteLine(muted == 0
                        ? "Никто не подошёл под запрос."
                        : $"Заглушено ботов: {muted}{(minutes == null ? " (навсегда)" : $" на {minutes} мин.")}");
                    break;
                }

                case "unmute":
                {
                    if (args.Length < 2)
                    {
                        shell.WriteLine("Usage: neurobots unmute <имя|NeuroPersona|all>");
                        break;
                    }

                    var unmuted = neuro.UnmuteBots(args[1]);
                    shell.WriteLine(unmuted == 0 ? "Никто не был заглушён." : $"Разглушено ботов: {unmuted}");
                    break;
                }

                case "test":
                {
                    var prompt = args.Length >= 2 ? string.Join(' ', args[1..]) : null;
                    shell.WriteLine("Нейроигроки: отправляю тестовый запрос к провайдеру...");
                    var result = await neuro.TestCompletionAsync(prompt);
                    shell.WriteLine(result);
                    break;
                }

                default:
                    shell.WriteLine(Help);
                    break;
            }
        }
        catch (Exception e)
        {
            shell.WriteError($"neurobots: {e.Message}");
        }
    }
}
