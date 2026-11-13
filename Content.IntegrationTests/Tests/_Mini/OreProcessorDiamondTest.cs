// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Lathe;
using Content.Server.Power.Components;
using Content.Shared._DV.Salvage.Components;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Mini;

/// <summary>
///     Переработчик руды плавит руду без потерь: 1 руда = 1 очищенный алмаз.
/// </summary>
[TestFixture]
public sealed class OreProcessorDiamondTest
{
    [Test]
    public async Task DiamondOreSmeltsWithoutLoss()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var mapData = await pair.CreateTestMap();
        await server.WaitIdleAsync();

        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;
        var materials = server.System<SharedMaterialStorageSystem>();
        var lathe = server.System<LatheSystem>();
        var diamond = protoMan.Index<LatheRecipePrototype>("MaterialDiamond");

        EntityUid processor = default;

        await server.WaitPost(() =>
        {
            processor = entMan.SpawnEntity("OreProcessor", mapData.GridCoords);
            if (entMan.TryGetComponent<ApcPowerReceiverComponent>(processor, out var power))
            {
                power.NeedsPower = false;
                power.Powered = true;
            }

            var user = entMan.SpawnEntity("MobHuman", mapData.GridCoords);
            for (var i = 0; i < 4; i++)
            {
                var ore = entMan.SpawnEntity("DiamondOre1Unprocessed", mapData.GridCoords);
                Assert.That(materials.TryInsertMaterialEntity(user, ore, processor), Is.True, "руда должна вставляться");
            }
        });
        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            Assert.That(materials.GetMaterialAmount(processor, "RawDiamond"), Is.EqualTo(400));
            Assert.That(entMan.GetComponent<MiningPointsComponent>(processor).Points, Is.EqualTo(4000u));

            // 1 алмаз за раз: именно так игрок плавит редкую руду, раньше тут не выдавалось ничего.
            Assert.That(lathe.TryAddToQueue(processor, diamond, 1), Is.True);
            lathe.TryStartProducing(processor);
        });
        await server.WaitRunTicks(2);
        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            Assert.That(materials.GetMaterialAmount(processor, "RawDiamond"), Is.EqualTo(300));
            Assert.That(materials.GetMaterialAmount(processor, "Diamond"), Is.EqualTo(100), "1 руда должна дать 1 алмаз");

            Assert.That(lathe.TryAddToQueue(processor, diamond, 3), Is.True);
            lathe.TryStartProducing(processor);
        });
        await server.WaitRunTicks(2);
        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            Assert.That(materials.GetMaterialAmount(processor, "RawDiamond"), Is.EqualTo(0));
            Assert.That(materials.GetMaterialAmount(processor, "Diamond"), Is.EqualTo(400), "3 руды должны дать 3 алмаза");
        });

        await pair.CleanReturnAsync();
    }
}
