// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.Client._Mini.Katorga;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Mini.Katorga;
using Content.Shared._Mini.Katorga.Components;
using Content.Shared._Mini.MiniCCVars;
using NUnit.Framework;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests._Mini;

/// <summary>
///     Проверяет должность заключённого на станции и доставку срока бана на клиент:
///     на станции «Заключённый» существует без лимита слотов, а клиент получает
///     остаток временного бана по net-сообщению и показывает срок в лобби.
/// </summary>
[TestFixture]
public sealed class KatorgaPrisonerTest
{
    private static async Task<(Pair.TestPair Pair, ICommonSession Player)> SetupConfinedPlayer()
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            InLobby = true,
            Destructive = true,
        });

        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var banMan = server.ResolveDependency<IBanManager>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var ticker = entMan.System<GameTicker>();

        // Каторга включена до старта раунда: на RoundStarting загрузится карта каторги.
        cfg.SetCVar(MiniCCVars.KatorgaEnabled, true);
        ticker.ToggleReadyAll(true);
        await server.WaitPost(() => ticker.StartRound());
        await pair.RunTicksSync(10);

        var player = playerMan.Sessions.Single();

        // Спавн может быть отложен до фоновой проверки бана (каторга включается после
        // подключения игрока), поэтому ждём, пока проверка завершится и игрок появится в мире.
        for (var i = 0; i < 100; i++)
        {
            if (player.Status == SessionStatus.InGame && player.AttachedEntity != null)
                break;

            await pair.RunTicksSync(1);
            await Task.Delay(20);
        }

        Assert.That(player.Status, Is.EqualTo(SessionStatus.InGame));
        Assert.That(player.AttachedEntity, Is.Not.Null);

        // Тестовый клиент по умолчанию админ, а админам каторга не мешает: снимаем админку,
        // иначе проверяется не тот сценарий.
        var adminMan = server.ResolveDependency<IAdminManager>();
        await server.WaitPost(() => adminMan.DeAdmin(player));

        // Временный бан как из админ-панели.
        await server.WaitPost(() =>
        {
            var info = new CreateServerBanInfo("Каторга-тест");
            info.AddUser(player.UserId, player.Name);
            info.WithMinutes(60);
            banMan.CreateServerBan(info);
        });

        // Бан пишется в базу асинхронно, ждём применения.
        for (var i = 0; i < 40; i++)
        {
            await pair.RunTicksSync(1);
            if (player.AttachedEntity is { } mob && entMan.HasComponent<KatorgaBoundComponent>(mob))
                break;
            await Task.Delay(50);
        }

        return (pair, player);
    }

    [Test]
    public async Task PrisonerJobIsUnlimitedOnStations()
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            InLobby = true,
            Destructive = true,
        });

        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var ticker = entMan.System<GameTicker>();

        // Каторга включает обработчик StationInitialized, который делает должность безлимитной.
        cfg.SetCVar(MiniCCVars.KatorgaEnabled, true);
        ticker.ToggleReadyAll(true);
        await server.WaitPost(() => ticker.StartRound());

        // Карта грузится асинхронно: ждём, пока появятся станции с должностями.
        var stations = new List<EntityUid>();
        for (var i = 0; i < 100 && stations.Count == 0; i++)
        {
            await pair.RunTicksSync(1);
            var query = entMan.EntityQueryEnumerator<StationJobsComponent>();
            while (query.MoveNext(out var station, out _))
                stations.Add(station);
        }

        Assert.That(stations, Is.Not.Empty, "на тестовой карте должна быть станция с должностями");

        var stationJobs = entMan.System<StationJobsSystem>();
        Assert.Multiple(() =>
        {
            foreach (var station in stations)
            {
                Assert.That(stationJobs.TryGetJobSlot(station, KatorgaJobs.Prisoner, out var slots), Is.True,
                    "должность заключённого должна быть на станции");
                Assert.That(slots, Is.Null, "у заключённого не должно быть лимита слотов");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BanStatusReachesClient()
    {
        var (pair, _) = await SetupConfinedPlayer();

        // Срок передаётся клиенту net-сообщением, ему нужны тики на доставку.
        await pair.RunTicksSync(20);

        var bans = pair.Client.ResolveDependency<KatorgaBanManager>();

        Assert.That(bans.TryGetActiveBan(out var remaining), Is.True, "клиент должен получить срок бана");
        Assert.Multiple(() =>
        {
            Assert.That(remaining, Is.GreaterThan(TimeSpan.Zero), "срок должен быть положительным");
            Assert.That(remaining, Is.LessThanOrEqualTo(TimeSpan.FromMinutes(61)),
                "срок не должен превышать выданный бан");
        });

        await pair.CleanReturnAsync();
    }
}
