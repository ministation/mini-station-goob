using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Immutable;
using Npgsql;
using Robust.Shared.IoC;
using Robust.Shared.Configuration;
using Robust.Shared.Log;
using Robust.Shared.GameObjects; // Нужно для EntitySystem

public sealed class SponsorSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    // Оставляем только это определение
    public record struct SponsorInfo(string Uid, int Level);

    public ImmutableList<SponsorInfo> Sponsors { get; private set; } = ImmutableList<SponsorInfo>.Empty;

    public override void Initialize()
    {
        base.Initialize();
        // Используем _ = для игнорирования возвращаемого значения Task
        _ = LoadSponsors();
    }

    private async Task LoadSponsors()
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
            Log.Warning("[Sponsors] PostgreSQL is not configured, skipping sponsor preload.");
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

            // ИСПРАВЛЕНО: используем локальный SponsorInfo вместо SponsorInfoComponent.SponsorInfo
            var tempList = new List<SponsorInfo>();

            while (await reader.ReadAsync())
            {
                // ИСПРАВЛЕНО: Создаем объект правильного типа
                tempList.Add(new SponsorInfo(
                    reader.GetGuid(0).ToString(),
                    reader.GetInt32(1)
                ));
            }

            Sponsors = tempList.ToImmutableList();
            Log.Info($"[Sponsors] Загружено {Sponsors.Count} спонсоров.");
        }
        catch (Exception ex)
        {
            Log.Error($"Ошибка БД спонсоров: {ex}");
        }
    }

    private static bool IsPostgresUnconfigured(NpgsqlConnectionStringBuilder builder)
    {
        if (!string.IsNullOrWhiteSpace(builder.Password))
            return false;

        return builder.Host is "localhost" or "127.0.0.1";
    }
}
