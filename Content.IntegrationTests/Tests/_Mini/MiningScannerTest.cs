// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

#nullable enable

using System.Linq;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Mining;
using Content.Shared.Mining.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Mini;

/// <summary>
/// Сканер минералов должен видеть и руду, выбранную случайно на старте карты (каторга, камни
/// шахтёров): раньше у таких камней не было готового слоя подсветки, и сканер не показывал
/// вообще ничего.
/// </summary>
[TestFixture]
public sealed class MiningScannerTest
{
    [Test]
    public async Task ScannerShowsRandomOre()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, DummyTicker = true });
        var map = await pair.CreateTestMap();
        var grid = map.Grid.Owner;
        var sEntMan = pair.Server.ResolveDependency<IEntityManager>();
        var sInv = sEntMan.System<InventorySystem>();
        var sToggle = sEntMan.System<ItemToggleSystem>();

        EntityUid player = default;
        EntityUid rock = default;
        await pair.Server.WaitPost(() =>
        {
            player = sEntMan.SpawnEntity("MobHuman", new EntityCoordinates(grid, 0.5f, 0.5f));
            pair.Server.PlayerMan.SetAttachedEntity(pair.Player, player);

            var scanner = sEntMan.SpawnEntity("MineralScannerUnpowered", new EntityCoordinates(grid, 0.5f, 0.5f));
            Assert.That(sInv.TryEquip(player, scanner, "pocket1", force: true), Is.True, "сканер не влез в карман");
            Assert.That(sToggle.TryActivate(scanner), Is.True, "сканер не включился");

            // Камень со случайной рудой, как на каторге: слоя подсветки в прототипе нет.
            rock = sEntMan.SpawnEntity("KatorgaWallRock", new EntityCoordinates(grid, 1.5f, 0.5f));
            var vein = sEntMan.GetComponent<OreVeinComponent>(rock);
            vein.CurrentOre = "OreCoal";
            sEntMan.Dirty(rock, vein);
        });

        await pair.RunSeconds(6);

        var cEntMan = pair.Client.ResolveDependency<IEntityManager>();
        var protoMan = pair.Client.ResolveDependency<IPrototypeManager>();
        var cPlayer = pair.ToClientUid(player);
        var cRock = pair.ToClientUid(rock);

        await pair.Client.WaitAssertion(() =>
        {
            Assert.That(cEntMan.HasComponent<MiningScannerViewerComponent>(cPlayer), Is.True,
                "клиент не получил компонент сканера");
            Assert.That(cEntMan.GetComponent<MiningScannerViewerComponent>(cPlayer).LastPingLocation, Is.Not.Null,
                "сканер ни разу не пикнул");

            Assert.That(cEntMan.TryGetComponent<OreVeinComponent>(cRock, out var vein), Is.True,
                "клиент не получил руду камня");
            Assert.That(vein!.CurrentOre, Is.Not.Null, "у камня не выбрана руда");
            Assert.That(protoMan.TryIndex(vein.CurrentOre!.Value, out OrePrototype? ore), Is.True,
                "руда не найдена в прототипах");

            var oreSprite = ore!.OreSprite as SpriteSpecifier.Rsi;
            Assert.That(oreSprite, Is.Not.Null, "у руды нет арта для сканера");

            var sprite = cEntMan.GetComponent<SpriteComponent>(cRock);
            Assert.That(sprite.BaseRSI, Is.Not.Null, "у камня нет базового RSI");
            Assert.That(sprite.BaseRSI!.TryGetState(oreSprite!.RsiState, out _), Is.True,
                "в RSI камня нет состояния подсветки этой руды");
        });

        await pair.CleanReturnAsync();
    }
}
