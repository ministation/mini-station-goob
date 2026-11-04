// SPDX-FileCopyrightText: 2026 Mini Station
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.NPC.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.NPC;

/// <summary>
/// Mini: an NPC that uses an action on its blackboard target must not crash the entity system
/// tick when that target has been deleted.
/// </summary>
[TestFixture, TestOf(typeof(NPCUseActionOnTargetSystem))]
public sealed class NPCUseActionOnTargetTest
{
    [Test]
    public async Task DeletedTargetIsSkipped()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var system = server.System<NPCUseActionOnTargetSystem>();

        EntityUid npc = default;
        EntityUid deleted = default;

        await server.WaitAssertion(() =>
        {
            npc = entMan.SpawnEntity("MobGoliath", testMap.GridCoords);
            deleted = entMan.SpawnEntity(null, testMap.GridCoords);
            entMan.DeleteEntity(deleted);
        });

        await server.WaitAssertion(() =>
        {
            // Regression: the stale target used to throw in SharedActionsSystem.SetEventTarget
            // because a deleted entity has no TransformComponent.
            Assert.That(system.TryUseTentacleAttack(npc, deleted), Is.False);

            // Sanity check that the goliath does have a usable action for a live target.
            var live = entMan.SpawnEntity(null, testMap.GridCoords);
            Assert.That(system.TryUseTentacleAttack(npc, live), Is.True);

            entMan.DeleteEntity(live);
            entMan.DeleteEntity(npc);
        });

        await pair.CleanReturnAsync();
    }
}
