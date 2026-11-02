// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server._Lavaland.Ashwalker;
using Content.Shared._Lavaland.Ashwalker;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Lavaland.Antags;

[TestFixture]
[TestOf(typeof(AshwalkerTribeSystem))]
public sealed class AshwalkerTribeTest
{
    [Test]
    public async Task TribeMemberGetsProtectObjectiveAndSacrificesCount()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var mindSystem = entMan.EntitySysManager.GetEntitySystem<SharedMindSystem>();

        EntityUid nest = default;
        await server.WaitPost(() =>
        {
            nest = entMan.SpawnEntity("LavalandAshwalkerNest", testMap.GridCoords);
        });

        await server.WaitRunTicks(2);

        EntityUid mob = default;
        EntityUid mind = default;
        await server.WaitPost(() =>
        {
            var nestXform = entMan.GetComponent<TransformComponent>(nest);
            mob = entMan.SpawnEntity("MobAshwalker", nestXform.Coordinates.Offset(new Vector2(1f, 0f)));
            mind = mindSystem.CreateMind(null).Owner;
            mindSystem.TransferTo(mind, mob);
        });

        await server.WaitRunTicks(2);

        EntityUid objective = default;
        await server.WaitAssertion(() =>
        {
            var tribeSystem = entMan.EntitySysManager.GetEntitySystem<AshwalkerTribeSystem>();
            var tribe = entMan.GetComponent<AshwalkerTribeComponent>(nest);

            Assert.That(entMan.HasComponent<AshwalkerTribeMemberComponent>(mob), Is.True,
                "ash walkers must carry the tribe member marker");
            Assert.That(tribe.Members, Does.Contain(mind), "ash walker must join the nearest tribe");

            var mindComp = entMan.GetComponent<MindComponent>(mind);
            Assert.That(mindComp.Objectives, Is.Not.Empty, "tribe member must get the protect objective");
            objective = mindComp.Objectives[0];

            var progress = new ObjectiveGetProgressEvent(mind, mindComp);
            entMan.EventBus.RaiseLocalEvent(objective, ref progress);
            Assert.That(progress.Progress, Is.EqualTo(1f), "objective must be complete while the nest stands");

            tribeSystem.RegisterSacrifice(nest);
            Assert.That(tribe.Sacrifices, Is.EqualTo(1), "feeding a corpse must count as a sacrifice");
        });

        // With the necropolis destroyed the protect objective fails.
        await server.WaitPost(() => entMan.DeleteEntity(nest));

        await server.WaitAssertion(() =>
        {
            var mindComp = entMan.GetComponent<MindComponent>(mind);
            var progress = new ObjectiveGetProgressEvent(mind, mindComp);
            entMan.EventBus.RaiseLocalEvent(objective, ref progress);
            Assert.That(progress.Progress, Is.EqualTo(0f), "objective must fail once the nest is gone");
        });

        await pair.CleanReturnAsync();
    }
}
