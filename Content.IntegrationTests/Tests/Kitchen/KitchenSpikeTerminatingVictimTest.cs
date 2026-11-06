// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Kitchen;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Kitchen;

/// <summary>
/// The kitchen spike damages a victim when it leaves the spike. Deleting a victim that is still in crit first
/// flags it as terminating and only then detaches it from the spike, so that removal damage used to kill an
/// already terminating mob. The resulting mob state change made MobStateActionsSystem delete the crit action
/// entities that the deletion had already flagged for removal, which the engine reports as
/// "entity: Called Delete on an entity already being deleted".
/// </summary>
[TestFixture]
[TestOf(typeof(SharedKitchenSpikeSystem))]
public sealed class KitchenSpikeTerminatingVictimTest
{
    private const string VictimProtoId = "KitchenSpikeTerminatingVictimTestMob";

    [TestPrototypes]
    private const string Prototypes = $@"
- type: entity
  id: {VictimProtoId}
  components:
  - type: MobState
  - type: MobThresholds
    triggersAlerts: false
    thresholds:
      0: Alive
      5: Critical
      15: Dead
  - type: MobStateActions
    actions:
      Critical:
      - ActionCritSuccumb
      - ActionCritFakeDeath
      - ActionCritLastWords
  - type: Actions
  - type: Damageable
    damageContainer: Biological
";

    [Test]
    public async Task DeletingCriticalVictimOnSpikeDoesNotDeleteActionsTwice()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var containerSys = server.System<SharedContainerSystem>();
        var map = await pair.CreateTestMap();

        EntityUid spike = default;
        EntityUid victim = default;

        await server.WaitAssertion(() =>
        {
            spike = entMan.SpawnEntity("KitchenSpike", map.GridCoords);
            victim = entMan.SpawnEntity(VictimProtoId, map.GridCoords);

            Assert.That(containerSys.TryGetContainer(spike, "body", out var container), Is.True);

            // Hooking deals 10 piercing, which is enough to put the victim into crit and grant the crit actions.
            Assert.That(containerSys.Insert(victim, container!, force: true), Is.True);

            var mobState = entMan.GetComponent<MobStateComponent>(victim);
            Assert.That(mobState.CurrentState, Is.EqualTo(MobState.Critical));
            Assert.That(entMan.GetComponent<MobStateActionsComponent>(victim).GrantedActions, Has.Count.EqualTo(3));
        });

        // Deleting the victim detaches it from the spike before the crit actions are removed. Without the guard
        // the removal damage kills the terminating victim and MobStateActionsSystem deletes the crit actions a
        // second time, which the engine reports as an error (or throws in a debug build).
        await server.WaitPost(() => entMan.DeleteEntity(victim));

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(victim), Is.True);
            Assert.That(containerSys.TryGetContainer(spike, "body", out var container), Is.True);
            Assert.That(container!.ContainedEntities, Is.Empty);
        });

        await pair.CleanReturnAsync();
    }
}
