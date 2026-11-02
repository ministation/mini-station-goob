// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

/// <summary>
/// Guards that the boss HUD stage reflects the real fight state: mob phases for the
/// colossus and hierophant, the half health enrage for Bubblegum, the split for the legion.
/// </summary>
[TestFixture]
public sealed class MegafaunaStageTest
{
    [Test]
    public async Task BubblegumStageFollowsHalfHealthEnrage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid boss = default;
        await server.WaitPost(() =>
        {
            boss = entMan.SpawnEntity("LavalandBossBubblegum", testMap.GridCoords);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var stage = entMan.GetComponent<MegafaunaStageComponent>(boss);
            Assert.Multiple(() =>
            {
                Assert.That(stage.Stage, Is.EqualTo(1), "Bubblegum starts in the first stage");
                Assert.That(stage.MaxStage, Is.EqualTo(2), "Bubblegum has an enrage stage");
            });
        });

        await server.WaitPost(() =>
        {
            entMan.System<DamageableSystem>().TryChangeDamage(
                boss,
                new DamageSpecifier { DamageDict = { { "Blunt", 1400f } } },
                true);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var stage = entMan.GetComponent<MegafaunaStageComponent>(boss);
            Assert.That(stage.Stage, Is.EqualTo(2), "Bubblegum must show the enrage stage below half health");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PhaseBossesExposeAllStages()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid colossus = default;
        EntityUid hierophant = default;
        EntityUid legion = default;
        await server.WaitPost(() =>
        {
            colossus = entMan.SpawnEntity("LavalandBossColossus", testMap.GridCoords);
            hierophant = entMan.SpawnEntity("MobHierophant", testMap.GridCoords);
            legion = entMan.SpawnEntity("LavalandBossMegaLegion", testMap.GridCoords);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var colossusStage = entMan.GetComponent<MegafaunaStageComponent>(colossus);
            var hierophantStage = entMan.GetComponent<MegafaunaStageComponent>(hierophant);
            var legionStage = entMan.GetComponent<MegafaunaStageComponent>(legion);

            Assert.Multiple(() =>
            {
                Assert.That(colossusStage.Stage, Is.EqualTo(1), "colossus starts in the first phase");
                Assert.That(colossusStage.MaxStage, Is.EqualTo(3), "colossus has three phases");
                Assert.That(hierophantStage.Stage, Is.EqualTo(1), "hierophant starts in the first phase");
                Assert.That(hierophantStage.MaxStage, Is.EqualTo(3), "hierophant has three phases");
                Assert.That(legionStage.Stage, Is.EqualTo(1), "legion starts in the first stage");
                Assert.That(legionStage.MaxStage, Is.EqualTo(2), "legion has a second stage after the split");
            });
        });

        await pair.CleanReturnAsync();
    }
}
