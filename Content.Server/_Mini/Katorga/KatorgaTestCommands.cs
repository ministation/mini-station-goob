// ВРЕМЕННЫЕ команды для локальной проверки каторги.
// Этот файл живёт в отдельном коммите и будет убран после теста: в PR их быть не должно.
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Server._Mini.Katorga;

/// <summary>
///     ВРЕМЕННО: выдаёт вызвавшему реальный временный бан и сразу отправляет на каторгу.
///     Позволяет проверить весь цикл на локалке без настоящего нарушения.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class KatorgaTestCommand : IConsoleCommand
{
    [Dependency] private readonly IBanManager _banManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "katorgatest";
    public string Description => "Выдать себе временный бан и отправиться на каторгу (проверка)";
    public string Help => "katorgatest [минуты=30]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError("Команда только для игроков.");
            return;
        }

        var minutes = 30;
        if (args.Length > 0 && (!int.TryParse(args[0], out minutes) || minutes <= 0))
        {
            shell.WriteError("Минуты должны быть положительным числом.");
            return;
        }

        var info = new CreateServerBanInfo("Каторга-тест " + DateTime.UtcNow.ToString("u"));
        info.AddUser(player.UserId, player.Name);
        info.WithMinutes(minutes);
        info.WithSeverity(NoteSeverity.Minor);
        _banManager.CreateServerBan(info);

        shell.WriteLine("Выдан тестовый бан на " + minutes + " мин, отправляю на каторгу...");
        // Бан пишется в базу асинхронно: даём ему доехать и отправляем заключённого.
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            await System.Threading.Tasks.Task.Delay(1500);
            _entityManager.System<KatorgaSystem>().TryConfine(player);
        });
    }
}

/// <summary>
///     ВРЕМЕННО: снимает с вызвавшего все тестовые баны каторги.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class KatorgaFreeCommand : IConsoleCommand
{
    [Dependency] private readonly IServerDbManager _db = default!;

    public string Command => "katorgafree";
    public string Description => "Снять с себя тестовые баны каторги";
    public string Help => "katorgafree";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError("Команда только для игроков.");
            return;
        }

        var bans = _db.GetBansAsync(null, player.UserId, null, null, includeUnbanned: false, BanType.Server)
            .GetAwaiter().GetResult();

        var removed = 0;
        foreach (var ban in bans)
        {
            if (ban.ExpirationTime == null || !ban.Reason.StartsWith("Каторга-тест"))
                continue;

            _db.AddUnbanAsync(new UnbanDef(ban.Id!.Value, player.UserId, DateTimeOffset.UtcNow))
                .GetAwaiter().GetResult();
            removed++;
        }

        shell.WriteLine(removed > 0
            ? "Снято тестовых банов: " + removed + ". Переподключись, чтобы зайти свободно."
            : "Тестовых банов не найдено.");
    }
}
