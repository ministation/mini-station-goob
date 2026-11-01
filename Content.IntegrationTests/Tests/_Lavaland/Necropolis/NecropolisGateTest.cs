// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Shared._Lavaland.Necropolis;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.Tests._Lavaland.Necropolis;

[TestFixture]
[TestOf(typeof(NecropolisGateComponent))]
[TestOf(typeof(NecropolisGateSpawnerComponent))]
public sealed class NecropolisGateTest
{
    [Test]
    public async Task GateStructureBuildsAndOpensOnKnocks()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid spawner = default;
        await server.WaitPost(() =>
        {
            spawner = entMan.SpawnEntity("LavalandNecropolisGateSpawner", testMap.GridCoords);
        });

        await server.WaitRunTicks(5);

        // Structure built: gate exists, spawner keeps existing (no self-delete, the
        // prototype sweep requires every entity to stay deletable).
        EntityUid gate = default;
        EntityCoordinates gateCoords = default;
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(spawner), Is.False, "spawner should keep existing after building");

            var gates = new List<Entity<NecropolisGateComponent>>();
            var query = entMan.EntityQueryEnumerator<NecropolisGateComponent>();
            while (query.MoveNext(out var uid, out var comp))
                gates.Add((uid, comp));

            Assert.That(gates, Has.Count.EqualTo(1), "gate should be built by the spawner");
            gate = gates[0].Owner;
            gateCoords = entMan.GetComponent<TransformComponent>(gate).Coordinates;

            var arches = 0;
            var archQuery = entMan.EntityQueryEnumerator<MetaDataComponent>();
            while (archQuery.MoveNext(out var uid, out var meta))
            {
                if (!entMan.Deleted(uid) && meta.EntityPrototype?.ID == "LavalandNecropolisArch")
                    arches++;
            }
            Assert.That(arches, Is.EqualTo(1), "arch should be built by the spawner");
        });

        // Knocker next to the gate.
        EntityUid user = default;
        await server.WaitPost(() =>
        {
            user = entMan.SpawnEntity("MobHuman", gateCoords);
        });

        await server.WaitRunTicks(1);

        // First knock does not open the gate yet.
        await server.WaitPost(() =>
        {
            entMan.EventBus.RaiseLocalEvent(gate, new InteractHandEvent(user, gate));
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<NecropolisGateComponent>(gate).Open, Is.False);
        });

        // Wait out the knock cooldown (2s) before knocking again.
        await server.WaitRunTicks(150);

        // Second knock opens the gate; the door animates for ~3 seconds.
        await server.WaitPost(() =>
        {
            entMan.EventBus.RaiseLocalEvent(gate, new InteractHandEvent(user, gate));
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<NecropolisGateComponent>(gate);
            Assert.That(comp.Open, Is.True);

            var physics = entMan.GetComponent<PhysicsComponent>(gate);
            Assert.That(physics.CanCollide, Is.True, "gate should stay solid while the door animation plays");
        });

        // Enough ticks for the 3.1s open animation.
        await server.WaitRunTicks(250);

        EntityUid legion = default;
        await server.WaitAssertion(() =>
        {
            var physics = entMan.GetComponent<PhysicsComponent>(gate);
            Assert.That(physics.CanCollide, Is.False, "gate should become passable once opened");

            var legionQuery = entMan.EntityQueryEnumerator<MetaDataComponent>();
            while (legionQuery.MoveNext(out var uid, out var meta))
            {
                if (!entMan.Deleted(uid) && meta.EntityPrototype?.ID == "LavalandBossMegaLegion")
                {
                    legion = uid;
                    break;
                }
            }

            Assert.That(entMan.EntityExists(legion), Is.True, "opening the gate should spawn Legion");
        });

        await pair.CleanReturnAsync();
    }
}
