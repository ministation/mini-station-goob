// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

#nullable enable

using Content.Shared.Inventory;
using Content.Shared.Movement.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Mini;

/// <summary>
/// Тяжёлые скафандры (джаггернаут) обязаны замедлять носящего через
/// ClothingSpeedModifier: модификаторы 0.65 должны доезжать до
/// MovementSpeedModifierComponent после надевания. Диона после волны #557
/// получила SpeedModifierImmunity и перестала замедляться - регрессия.
/// </summary>
[TestFixture]
public sealed class JuggernautSlowdownTest
{
    [Test]
    public async Task CybersunJuggernautSlowsWearer()
    {
        await SuitSlowsWearer("ClothingOuterHardsuitJuggernaut", "MobHuman");
    }

    [Test]
    public async Task ErtJuggernautSlowsWearer()
    {
        await SuitSlowsWearer("ClothingOuterHardsuitERTJuggernaut", "MobHuman");
    }

    [Test]
    public async Task DionaJuggernautSlowsWearer()
    {
        await SuitSlowsWearer("ClothingOuterHardsuitJuggernaut", "MobDiona");
    }

    private static async Task SuitSlowsWearer(string prototype, string mobPrototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = true,
        });

        var map = await pair.CreateTestMap();
        var grid = map.Grid.Owner;
        var sEntMan = pair.Server.ResolveDependency<IEntityManager>();
        EntityUid mob = default;

        await pair.Server.WaitPost(() =>
        {
            var coords = new EntityCoordinates(grid, 0.5f, 0.5f);
            mob = sEntMan.SpawnEntity(mobPrototype, coords);
            var suit = sEntMan.SpawnEntity(prototype, coords);
            var inventory = sEntMan.System<InventorySystem>();
            var equipped = inventory.TryEquip(mob, suit, "outerClothing", force: true);
            Assert.That(equipped, Is.True, "not equipped: " + prototype);
        });

        await pair.RunTicksSync(5);

        await pair.Server.WaitAssertion(() =>
        {
            var move = sEntMan.GetComponent<MovementSpeedModifierComponent>(mob);
            Assert.That(move.WalkSpeedModifier, Is.EqualTo(0.65f).Within(0.02f),
                $"{mobPrototype}/{prototype}: walk slowdown missing");
            Assert.That(move.SprintSpeedModifier, Is.EqualTo(0.65f).Within(0.02f),
                $"{mobPrototype}/{prototype}: sprint slowdown missing");
        });

        await pair.CleanReturnAsync();
    }
}
