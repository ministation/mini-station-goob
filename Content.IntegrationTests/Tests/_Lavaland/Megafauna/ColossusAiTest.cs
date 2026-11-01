// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

[TestFixture]
[TestOf(typeof(MegafaunaAiComponent))]
public sealed class ColossusAiTest
{
    [Test]
    public async Task ColossusAiStartsAndChestFills()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var entSysMan = server.ResolveDependency<IEntitySystemManager>();

        EntityUid colossus = default;
        EntityUid chest = default;
        MegafaunaAiComponent ai = null;

        await server.WaitPost(() =>
        {
            colossus = entMan.SpawnAtPosition("LavalandBossColossus", testMap.GridCoords);
            chest = entMan.SpawnAtPosition("LavalandCrateColossus", testMap.GridCoords);
            ai = entMan.GetComponent<MegafaunaAiComponent>(colossus);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var megafaunaSystem = entSysMan.GetEntitySystem<MegafaunaSystem>();
            Assert.DoesNotThrow(() => megafaunaSystem.StartupMegafauna((colossus, ai)));
            Assert.That(ai.Schedule, Is.Not.Empty);
            megafaunaSystem.ShutdownMegafauna((colossus, ai));

            // The colossus chest filled with exactly one loot entry.
            Assert.That(entMan.TryGetComponent<ContainerManagerComponent>(chest, out var containers));
            var found = 0;
            foreach (var container in containers.Containers.Values)
                found += container.ContainedEntities.Count;
            Assert.That(found, Is.EqualTo(1), "colossus chest should contain exactly one loot group");
        });

        await pair.CleanReturnAsync();
    }
}
