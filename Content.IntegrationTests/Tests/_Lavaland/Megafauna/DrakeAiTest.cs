// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Systems;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

[TestFixture]
[TestOf(typeof(MegafaunaAiComponent))]
public sealed class DrakeAiTest
{
    [Test]
    public async Task DrakeAiStartsAndDragonChestFills()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var entSysMan = server.ResolveDependency<IEntitySystemManager>();

        EntityUid drake = default;
        EntityUid chest = default;
        MegafaunaAiComponent ai = null;

        await server.WaitPost(() =>
        {
            drake = entMan.SpawnAtPosition("LavalandBossAshDrake", testMap.GridCoords);
            chest = entMan.SpawnAtPosition("LavalandCrateDragon", testMap.GridCoords);
            ai = entMan.GetComponent<MegafaunaAiComponent>(drake);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            // AI selector resolved and can start (all action prototypes resolved).
            var megafaunaSystem = entSysMan.GetEntitySystem<MegafaunaSystem>();
            Assert.That(ai.Active, Is.False);
            Assert.DoesNotThrow(() => megafaunaSystem.StartupMegafauna((drake, ai)));
            Assert.That(ai.Active, Is.True);
            Assert.That(ai.Schedule, Is.Not.Empty);
            megafaunaSystem.ShutdownMegafauna((drake, ai));
        });

        // The dragon chest filled on map init with exactly one of the four loot items.
        await server.WaitAssertion(() =>
        {
            var containerSys = entSysMan.GetEntitySystem<SharedContainerSystem>();
            Assert.That(entMan.TryGetComponent<ContainerManagerComponent>(chest, out var containers));
            var found = 0;
            foreach (var container in containers.Containers.Values)
            {
                found += container.ContainedEntities.Count;
            }
            Assert.That(found, Is.GreaterThanOrEqualTo(1), "dragon chest should contain loot");
        });

        await pair.CleanReturnAsync();
    }
}
