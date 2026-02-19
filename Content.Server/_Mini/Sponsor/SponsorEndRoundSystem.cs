using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Content.Shared.GameTicking;
using Npgsql;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;

namespace Content.Server.Sponsors;

public sealed class SponsorSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    // Структура данных и список
    public record struct SponsorInfo(string Uid, int Level);
    public ImmutableList<SponsorInfo> Sponsors { get; private set; } = ImmutableList<SponsorInfo>.Empty;

    public override void Initialize()
    {
        base.Initialize();

        // Подписываемся на окончание раунда для обновления данных
        SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEnd);

        // Загружаем данные сразу при старте сервера
        _ = LoadSponsors();
    }

    private void OnRoundEnd(RoundEndMessageEvent ev)
    {
        // Обновляем список асинхронно, чтобы не задерживать показ статистики раунда
        _ = Task.Run(async () =>
        {
            try
            {
                await LoadSponsors();
                Log.Info("[Sponsors] Данные успешно обновлены после раунда.");
            }
            catch (Exception e)
            {
                Log.Error($"[Sponsors] Ошибка при фоновом обновлении: {e}");
            }
        });
    }

    public async Task LoadSponsors()
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = _cfg.GetCVar<string>("database.pg_host"),
            Port = _cfg.GetCVar<int>("database.pg_port"),
            Database = _cfg.GetCVar<string>("database.pg_database"),
            Username = _cfg.GetCVar<string>("database.pg_username"),
            Password = _cfg.GetCVar<string>("database.pg_password")
        };

        if (IsPostgresUnconfigured(builder))
        {
            Log.Warning("[Sponsors] PostgreSQL is not configured, skipping sponsor sync.");
            return;
        }

        try
        {
            await using var dataSource = NpgsqlDataSource.Create(builder.ConnectionString);

            await using var cmd = dataSource.CreateCommand(@"
                SELECT DISTINCT da.user_id, ds.sponsor_level
                FROM discord_sponsor ds
                JOIN discord_auth da ON ds.discord_id = da.discord_id");

            await using var reader = await cmd.ExecuteReaderAsync();
            var tempList = new List<SponsorInfo>();

            while (await reader.ReadAsync())
            {
                tempList.Add(new SponsorInfo(
                    reader.GetGuid(0).ToString(),
                    reader.GetInt32(1)
                ));
            }

            Sponsors = tempList.ToImmutableList();
            Log.Info($"[Sponsors] Загружено {Sponsors.Count} спонсоров из БД.");
        }
        catch (Exception ex)
        {
            Log.Error($"[Sponsors] Критическая ошибка БД: {ex}");
        }
    }

    private static bool IsPostgresUnconfigured(NpgsqlConnectionStringBuilder builder)
    {
        if (!string.IsNullOrWhiteSpace(builder.Password))
            return false;

        return builder.Host is "localhost" or "127.0.0.1";
    }
}
