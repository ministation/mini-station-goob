// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
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

            // TG gives every reward at once: both daggers, the vocal cords and a random crystal.
            Assert.That(entMan.TryGetComponent<ContainerManagerComponent>(chest, out var containers));
            var found = containers.Containers.Values
                .SelectMany(container => container.ContainedEntities)
                .Select(entity => entMan.GetComponent<MetaDataComponent>(entity).EntityPrototype!.ID)
                .ToArray();
            Assert.That(found, Does.Contain("LavalandCainDagger"));
            Assert.That(found, Does.Contain("LavalandAbelDagger"));
            Assert.That(found, Does.Contain("LavalandVocalCords"));
            Assert.That(found, Does.Contain("LavalandRenewalCrystal"));
        });

        await pair.CleanReturnAsync();
    }
}
