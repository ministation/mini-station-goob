// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Linq;
using System.Threading.Tasks;
using Content.Server._Mini.Katorga;
using Content.Server.Administration.Managers;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Ghost;
using Content.Shared._Mini.Katorga.Components;
using Content.Shared._Mini.MiniCCVars;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mining.Components;
using NUnit.Framework;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests._Mini;

/// <summary>
///     Проверяет сценарии каторги для онлайн-игрока: временный бан не кикает с сервера,
///     а отправляет отбывать срок; после реконнекта игрок снова оказывается на каторге;
///     гостование заключённому запрещено.
/// </summary>
[TestFixture]
public sealed class KatorgaBanTest
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

    private static bool IsConfined(IEntityManager entMan, ICommonSession player)
    {
        return player.AttachedEntity is { } mob && entMan.HasComponent<KatorgaBoundComponent>(mob);
    }

    private static string GetSlotProto(IEntityManager entMan, EntityUid mob, string slot)
    {
        return entMan.System<InventorySystem>().TryGetSlotEntity(mob, slot, out var item) &&
               entMan.TryGetComponent(item, out MetaDataComponent meta)
            ? meta.EntityPrototype?.ID
            : null;
    }

    [Test]
    public async Task TimedBanSendsOnlinePlayerToKatorgaInsteadOfKick()
    {
        var (pair, player) = await SetupConfinedPlayer();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var ticker = entMan.System<GameTicker>();

        Assert.Multiple(() =>
        {
            Assert.That(playerMan.Sessions.Contains(player), "временный бан не должен кикать игрока");
            Assert.That(player.Status, Is.Not.EqualTo(SessionStatus.Disconnected));
            Assert.That(player.AttachedEntity, Is.Not.Null);
            Assert.That(IsConfined(entMan, player), Is.True, "игрок должен отбывать срок на каторге");
        });

        // Каторга это отдельная карта, а не станция.
        var mob = player.AttachedEntity!.Value;
        var mapId = entMan.GetComponent<TransformComponent>(mob).MapID;
        Assert.That(mapId, Is.Not.EqualTo(ticker.DefaultMap));

        // Заключённый не голый: выдаётся комплект утилизатора, сканер минералов лежит в кармане.
        Assert.Multiple(() =>
        {
            Assert.That(GetSlotProto(entMan, mob, "outerClothing"), Is.EqualTo("ClothingOuterHardsuitSalvage"),
                "заключённому должен выдаваться комбинезон утилизатора");
            Assert.That(GetSlotProto(entMan, mob, "pocket1"), Is.EqualTo("KatorgaMineralScanner"),
                "сканер минералов должен лежать в кармане");
        });

        // Каторга дышит: точка спавна не в вакууме и на станционном гриде есть достижимая руда.
        await pair.RunTicksSync(120);
        var atmos = entMan.System<AtmosphereSystem>();
        var mix = atmos.GetTileMixture(mob);
        Assert.That(mix?.TotalMoles ?? 0f, Is.GreaterThan(50f), "точка спавна каторги не должна быть в вакууме");

        // Руда у выхода: ручные камни KatorgaMineRock (если расставлены на карте) плюс
        // биомные KatorgaWallRock, которые биом генерирует в открытых тайлах вокруг игрока.
        // Биому нужно несколько тиков на генерацию чанков вокруг точки спавна - ждём.
        var mineRocksWithOre = 0;
        for (var attempt = 0; attempt < 40 && mineRocksWithOre < 10; attempt++)
        {
            await pair.RunTicksSync(15);

            mineRocksWithOre = 0;
            var rocks = entMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (rocks.MoveNext(out var uid, out var meta, out var xform))
            {
                var protoId = meta.EntityPrototype?.ID;
                if (xform.MapID != mapId ||
                    (protoId != "KatorgaMineRock" && protoId != "KatorgaWallRock"))
                {
                    continue;
                }

                if (entMan.TryGetComponent<OreVeinComponent>(uid, out var vein) && vein.CurrentOre != null)
                    mineRocksWithOre++;
            }
        }

        Assert.That(mineRocksWithOre, Is.GreaterThanOrEqualTo(10), "у выхода на каторгу должна быть руда");

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReconnectKeepsPlayerOnKatorga()
    {
        var (pair, player) = await SetupConfinedPlayer();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();

        var name = player.Name;
        var oldEntity = player.AttachedEntity;
        Assert.That(oldEntity, Is.Not.Null);
        var oldCoords = entMan.GetComponent<TransformComponent>(oldEntity!.Value).Coordinates;

        // Реконнект: бан-гейт должен пропустить (бан временный), а игрок вернуться на каторгу.
        var netManager = pair.Client.ResolveDependency<Robust.Shared.Network.IClientNetManager>();
        await pair.Client.WaitAssertion(() => netManager.ClientDisconnect("katorga-reconnect-test"));
        await pair.RunTicksSync(5);

        await pair.Client.WaitIdleAsync();
        pair.Client.SetConnectTarget(pair.Server);
        await pair.Client.WaitPost(() => netManager.ClientConnect(null!, 0, name));
        await pair.RunTicksSync(20);

        var newPlayer = playerMan.Sessions.Single();
        Assert.Multiple(() =>
        {
            Assert.That(newPlayer.Status, Is.EqualTo(SessionStatus.InGame), "реконнект не должен упираться в бан");
            Assert.That(IsConfined(entMan, newPlayer), Is.True, "после реконнекта игрок снова на каторге");

            // Реконнект не должен пересоздавать персонажа и раздевать его: тело и вещи остаются.
            Assert.That(newPlayer.AttachedEntity, Is.EqualTo(oldEntity), "после реконнекта должно остаться то же тело");
            Assert.That(GetSlotProto(entMan, newPlayer.AttachedEntity!.Value, "outerClothing"),
                Is.EqualTo("ClothingOuterHardsuitSalvage"), "снаряжение должно остаться на персонаже");
            Assert.That(GetSlotProto(entMan, newPlayer.AttachedEntity!.Value, "pocket1"),
                Is.EqualTo("KatorgaMineralScanner"), "сканер должен остаться в кармане");
            Assert.That(entMan.GetComponent<TransformComponent>(newPlayer.AttachedEntity!.Value).Coordinates,
                Is.EqualTo(oldCoords), "реконнект не должен телепортировать заключённого");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GhostAttemptBlockedForInmate()
    {
        var (pair, player) = await SetupConfinedPlayer();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        var ghostSystem = entMan.System<GhostSystem>();
        var mindSystem = entMan.System<SharedMindSystem>();
        Assert.That(mindSystem.TryGetMind(player.UserId, out var mindId, out _), Is.True);

        var ghosted = true;
        await server.WaitPost(() => ghosted = ghostSystem.OnGhostAttempt(mindId!.Value, true));

        Assert.That(ghosted, Is.False, "заключённому нельзя гостоваться");
        Assert.That(entMan.HasComponent<Content.Shared.Ghost.GhostComponent>(player.AttachedEntity!.Value), Is.False);

        await pair.CleanReturnAsync();
    }
}
