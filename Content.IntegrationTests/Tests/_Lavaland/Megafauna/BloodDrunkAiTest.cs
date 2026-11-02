// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

[TestFixture]
[TestOf(typeof(MegafaunaAiComponent))]
public sealed class BloodDrunkAiTest
{
    [Test]
    public async Task AllBloodDrunkVariantsStartAi()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var entSysMan = server.ResolveDependency<IEntitySystemManager>();

        var megafaunaSystem = entSysMan.GetEntitySystem<MegafaunaSystem>();

        foreach (var proto in new[]
                 {
                     "LavalandBossBloodDrunk",
                     "LavalandBossBloodDrunkGuidance",
                     "LavalandBossBloodDrunkHunter",
                 })
        {
            EntityUid boss = default;
            MegafaunaAiComponent ai = null;

            await server.WaitPost(() =>
            {
                boss = entMan.SpawnAtPosition(proto, testMap.GridCoords);
                ai = entMan.GetComponent<MegafaunaAiComponent>(boss);
            });

            await server.WaitRunTicks(5);

            await server.WaitAssertion(() =>
            {
                Assert.DoesNotThrow(() => megafaunaSystem.StartupMegafauna((boss, ai)));
                Assert.That(ai.Schedule, Is.Not.Empty, $"{proto} should have scheduled actions");
                megafaunaSystem.ShutdownMegafauna((boss, ai));
            });
        }

        await pair.CleanReturnAsync();
    }
}
