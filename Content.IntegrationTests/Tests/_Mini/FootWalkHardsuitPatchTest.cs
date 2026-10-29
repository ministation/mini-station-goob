// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

#nullable enable

using System.Numerics;
using Content.Client._Mini.FootWalk;
using Content.IntegrationTests.Pair;
using Content.Shared._Mini.FootWalk;
using Content.Shared.Gravity;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests._Mini;

/// <summary>
/// Регрессия: у скафандра, порезанного по линии бёдер, прозрачные участки его же спрайта
/// (пах, проймы) уезжали вверх вместе с половинками/полосой и на каждом шаге на пару
/// пикселей открывали голые руки и таз. Под движущейся полосой теперь лежит неподвижная
/// копия в позе покоя, поэтому открывать нечего.
/// </summary>
[TestFixture]
[TestOf(typeof(FootWalkAnimationSystem))]
public sealed class FootWalkHardsuitPatchTest
{
    /// <summary>
    /// Скафандр: под движущейся полосой есть статичная копия, и она не двигается вместе с ногами.
    /// </summary>
    [Test]
    public async Task HardsuitGetsStaticHipPatch()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = true,
        });

        var mob = await SpawnWalkingHuman(pair, "ClothingOuterHardsuitSpatio");
        var cEntMan = pair.Client.ResolveDependency<IEntityManager>();
        var cMob = pair.ToClientUid(mob);
        var sprite = cEntMan.GetComponent<SpriteComponent>(cMob);
        var spriteSystem = cEntMan.System<SpriteSystem>();

        FootWalkAnimationComponent walk = default!;
        string patchKey = string.Empty;
        Vector2 patchOffset = default;
        Vector2 suitOffset = default;
        var patchVisible = false;

        await pair.Client.WaitAssertion(() =>
        {
            walk = cEntMan.GetComponent<FootWalkAnimationComponent>(cMob);
            Assert.That(walk.OuterHipPatchKeys, Is.Not.Empty,
                "скафандр режется по бёдрам, значит под полосой должна быть неподвижная копия");
            Assert.That(walk.OuterSideBandKeys, Is.Not.Empty, "полоса скафандра не собралась");

            patchKey = walk.OuterHipPatchKeys[0];
            Assert.That(spriteSystem.LayerMapTryGet((cMob, sprite), patchKey, out var patchIndex, false), Is.True,
                "слой неподвижной копии не создан");
            Assert.That(spriteSystem.TryGetLayer((cMob, sprite), patchIndex, out var patchLayer, false), Is.True,
                "слой неподвижной копии не читается");
            patchVisible = patchLayer!.Visible;
            patchOffset = patchLayer.Offset;

            // Оригинальный слой скафандра остаётся в позе покоя: копия обязана совпадать с ним.
            var bandKey = walk.OuterSideBandKeys[0];
            var suitKey = bandKey[..^"-walk-band".Length];
            Assert.That(spriteSystem.LayerMapTryGet((cMob, sprite), suitKey, out var suitIndex, false), Is.True,
                "не найден оригинальный слой скафандра");
            Assert.That(spriteSystem.TryGetLayer((cMob, sprite), suitIndex, out var suitLayer, false), Is.True,
                "оригинальный слой скафандра не читается");
            suitOffset = suitLayer!.Offset;
        });

        Assert.Multiple(() =>
        {
            Assert.That(patchVisible, Is.True, "неподвижная копия полосы должна быть видна");
            Assert.That(patchOffset, Is.EqualTo(suitOffset),
                "неподвижная копия не должна ехать вместе с шагающей полосой");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Обратный контроль: обычное пальто не режется по бёдрам, копия ему не нужна.
    /// </summary>
    [Test]
    public async Task CoatGetsNoHipPatch()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = true,
        });

        var mob = await SpawnWalkingHuman(pair, "ClothingOuterCoatLab");
        var cEntMan = pair.Client.ResolveDependency<IEntityManager>();
        var cMob = pair.ToClientUid(mob);

        FootWalkAnimationComponent walk = default!;
        await pair.Client.WaitAssertion(() =>
        {
            walk = cEntMan.GetComponent<FootWalkAnimationComponent>(cMob);
            Assert.That(walk.OuterSideBandKeys, Is.Not.Empty, "полоса пальто не собралась, контроль пустой");
        });

        Assert.That(walk.OuterHipPatchKeys, Is.Empty,
            "пальто режется по низу, копия полосы ему не нужна");

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Ставит MobHuman на грид с гравитацией, надевает outerProto и разгоняет моба, чтобы
    /// клиентская система ходьбы построила слои одежды.
    /// </summary>
    private static async Task<EntityUid> SpawnWalkingHuman(TestPair pair, string outerProto)
    {
        var map = await pair.CreateTestMap();
        var grid = map.Grid.Owner;
        var sEntMan = pair.Server.ResolveDependency<IEntityManager>();
        EntityUid mob = default;

        await pair.Server.WaitPost(() =>
        {
            var gravity = sEntMan.GetComponent<GravityComponent>(grid);
            gravity.Inherent = true;
            gravity.Enabled = true;
            sEntMan.Dirty(grid, gravity);

            mob = sEntMan.SpawnEntity("MobHuman", new EntityCoordinates(grid, 0.5f, 0.5f));
            var outer = sEntMan.SpawnEntity(outerProto, new EntityCoordinates(grid, 0.5f, 0.5f));
            var inventory = sEntMan.System<InventorySystem>();
            Assert.That(inventory.TryEquip(mob, outer, "outerClothing", force: true), Is.True,
                "не удалось надеть " + outerProto);

            var gravitySystem = sEntMan.System<SharedGravitySystem>();
            if (sEntMan.TryGetComponent<GravityAffectedComponent>(mob, out var affected))
                gravitySystem.RefreshWeightless((mob, affected));
        });

        await pair.RunTicksSync(5);

        var cEntMan = pair.Client.ResolveDependency<IEntityManager>();
        var cMob = pair.ToClientUid(mob);
        var cPhysics = cEntMan.System<SharedPhysicsSystem>();
        var walkSystem = cEntMan.System<FootWalkAnimationSystem>();

        await pair.Client.WaitPost(() =>
        {
            cPhysics.SetLinearVelocity(cMob, new Vector2(2.5f, 0f),
                body: cEntMan.GetComponent<PhysicsComponent>(cMob));

            for (var i = 0; i < 4; i++)
                walkSystem.FrameUpdate(1f / 60f);
        });

        return mob;
    }
}
