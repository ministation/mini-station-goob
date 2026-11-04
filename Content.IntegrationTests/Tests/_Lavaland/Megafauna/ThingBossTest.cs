// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Server._Lavaland.BossMechanics;
using Content.Server.DoAfter;
using Content.Shared._Lavaland.BossMechanics;
using Content.Shared._Lavaland.Megafauna;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Conditions;
using Content.Shared._Lavaland.Megafauna.Selectors;
using Content.Shared._Lavaland.MobPhases;
using Content.Shared.Actions.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

/// <summary>
/// Guards the Outpost 31 boss: its arena and accelerators, the phase budget, the attack wiring
/// and the loot it leaves behind.
/// </summary>
[TestFixture]
[TestOf(typeof(ThingBossComponent))]
public sealed class ThingBossTest
{
    private const string BossProto = "LavalandBossThing";
    private const string ArenaSpawnerProto = "LavalandThingArenaSpawner";

    private static readonly string[] Actions =
    {
        "ActionThingDecimate",
        "ActionThingCharge",
        "ActionThingShriek",
        "ActionThingCardinalTendrils",
        "ActionThingBigTendrils",
        "ActionThingAcidSpit",
    };

    [Test]
    public async Task ThingArenaOverloadsPhasesAndDropsLoot()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var appearance = entMan.System<SharedAppearanceSystem>();
        var damageable = entMan.System<DamageableSystem>();

        EntityUid boss = default;
        EntityUid user = default;
        var accelerators = new List<Entity<ThingAcceleratorComponent>>();

        await server.WaitPost(() =>
        {
            entMan.SpawnEntity(ArenaSpawnerProto, testMap.GridCoords);
            user = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
        });
        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var query = entMan.EntityQueryEnumerator<ThingBossComponent>();
            Assert.That(query.MoveNext(out var bossUid, out _), Is.True, "the arena must spawn the boss");
            boss = bossUid;
            Assert.That(query.MoveNext(out _, out _), Is.False, "there must be exactly one Thing");

            var accelQuery = entMan.EntityQueryEnumerator<ThingAcceleratorComponent>();
            while (accelQuery.MoveNext(out var uid, out var accel))
                accelerators.Add((uid, accel));

            Assert.That(accelerators, Has.Count.EqualTo(2), "the arena has two molecular accelerators");
            foreach (var accel in accelerators)
                Assert.That(accel.Comp.Boss, Is.EqualTo(boss), "both accelerators must be wired to the boss");

            var thing = entMan.GetComponent<ThingBossComponent>(boss);
            Assert.That(thing.Phase, Is.EqualTo(1), "the fight starts in the first phase");
            Assert.That(thing.Invulnerable, Is.False, "the boss must be damageable at the start");

            var phases = entMan.GetComponent<MobPhasesComponent>(boss);
            Assert.That(phases.CurrentPhase, Is.EqualTo(1), "the attack selector starts in the first phase");

            var stage = entMan.GetComponent<MegafaunaStageComponent>(boss);
            Assert.That(stage.MaxStage, Is.EqualTo(3), "the Thing has one HUD stage per phase");

            var granted = entMan.GetComponent<ActionsComponent>(boss).Actions
                .Where(action => !entMan.Deleted(action))
                .Select(action => entMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID)
                .ToArray();

            foreach (var action in Actions)
                Assert.That(granted, Does.Contain(action), $"the Thing was never granted {action}");
        });

        // 700 damage is past the 600 point phase budget, the excess has to be eaten.
        await server.WaitPost(() => damageable.TryChangeDamage(boss,
            new DamageSpecifier { DamageDict = { { "Blunt", 700f } } }, true));
        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            var thing = entMan.GetComponent<ThingBossComponent>(boss);
            Assert.That(thing.Invulnerable, Is.True, "running out of phase health must make the boss invulnerable");

            var health = entMan.GetComponent<DamageableComponent>(boss);
            Assert.That(health.TotalDamage.Float(), Is.EqualTo(600f).Within(1f),
                "damage must be capped at the phase budget");

            foreach (var accel in accelerators)
                Assert.That(accel.Comp.Deployed, Is.True, "accelerators pop out of the floor while the boss is down");

            var phases = entMan.GetComponent<MobPhasesComponent>(boss);
            Assert.That(phases.CurrentPhase, Is.EqualTo(2), "the attack selector follows the damage budget");
        });

        // Overloading an accelerator is what pushes the boss forward, the healing alone never does.
        // Go through a real do-after, raising the event by hand would have no DoAfter attached.
        var doAfterSystem = entMan.System<DoAfterSystem>();
        await server.WaitPost(() =>
        {
            var overload = new DoAfterArgs(entMan, user, TimeSpan.FromSeconds(1),
                new ThingAcceleratorOverloadDoAfterEvent(), accelerators[0].Owner);
            Assert.That(doAfterSystem.TryStartDoAfter(overload), Is.True, "the overload do-after must start");
        });
        await server.WaitRunTicks(60);

        await server.WaitAssertion(() =>
        {
            var thing = entMan.GetComponent<ThingBossComponent>(boss);
            Assert.That(thing.Phase, Is.EqualTo(2), "overloading an accelerator must push the boss to the next phase");
            Assert.That(thing.Invulnerable, Is.False, "overloading the accelerator must end the downed window");

            Assert.That(accelerators[0].Comp.Functional, Is.False, "an accelerator is single use");
            Assert.That(accelerators[1].Comp.Functional, Is.True, "the second accelerator is saved for the next phase");
            Assert.That(accelerators[1].Comp.Deployed, Is.False, "the accelerator goes back into the floor");

            Assert.That(appearance.TryGetData(boss, ThingVisuals.State, out var state), Is.True,
                "the boss must have a phase sprite");
            Assert.That(state, Is.EqualTo("p2"), "the sprite follows the phase");
        });

        // Second phase: another 600 damage, another accelerator.
        await server.WaitPost(() => damageable.TryChangeDamage(boss,
            new DamageSpecifier { DamageDict = { { "Blunt", 700f } } }, true));
        await server.WaitRunTicks(3);

        await server.WaitPost(() =>
        {
            var overload = new DoAfterArgs(entMan, user, TimeSpan.FromSeconds(1),
                new ThingAcceleratorOverloadDoAfterEvent(), accelerators[1].Owner);
            Assert.That(doAfterSystem.TryStartDoAfter(overload), Is.True, "the second overload must start");
        });
        await server.WaitRunTicks(60);

        await server.WaitAssertion(() =>
        {
            var thing = entMan.GetComponent<ThingBossComponent>(boss);
            Assert.That(thing.Phase, Is.EqualTo(3), "the second accelerator must reach the last phase");
            Assert.That(thing.Invulnerable, Is.False, "the last phase has no downed window");
        });

        // The last phase has no phase budget left: the boss just dies.
        await server.WaitPost(() => damageable.TryChangeDamage(boss,
            new DamageSpecifier { DamageDict = { { "Blunt", 700f } } }, true));
        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            var spawned = new List<EntityUid>();
            var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out var uid, out var meta))
            {
                if (meta.EntityPrototype?.ID is "LavalandCrateThing" or "LavalandThingKeycard")
                    spawned.Add(uid);
            }

            Assert.That(spawned, Has.Count.EqualTo(2), "the boss must drop its crate and the storage room keycard");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThingAttacksAreGatedAndSpawnSpikes()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        EntityUid boss = default;
        await server.WaitPost(() => boss = entMan.SpawnEntity(BossProto, testMap.GridCoords));
        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            var group = (GroupMegafaunaSelector) protoMan.Index<MegafaunaSelectorPrototype>("ThingActions").Selector;

            var attacks = new Dictionary<string, PerformActionSelector>();
            foreach (var child in group.Children)
            {
                Assert.That(child, Is.InstanceOf<ProtoIdMegafaunaSelector>(), "attacks are selected by prototype id");
                var id = ((ProtoIdMegafaunaSelector) child).Id.Id;
                var selector = protoMan.Index<MegafaunaSelectorPrototype>(id).Selector;
                Assert.That(selector, Is.InstanceOf<PerformActionSelector>(), $"{id} must be performed by the AI");
                attacks[id] = (PerformActionSelector) selector;
            }

            var selectorActions = attacks.Values.Select(selector => selector.ActionId.Id).ToArray();
            Assert.That(selectorActions, Is.EquivalentTo(Actions), "every granted action must be in the selector");

            // Phase gates: the close range attack and the acid shower only exist later.
            var shriek = attacks["ThingShriek"];
            Assert.That(shriek.Conditions.OfType<PhaseMegafaunaCondition>().Single().RequiredPhases,
                Is.EquivalentTo(new[] { 2, 3 }), "the shriek is a second phase attack");

            var acid = attacks["ThingAcidSpit"];
            Assert.That(acid.Conditions.OfType<PhaseMegafaunaCondition>().Single().RequiredPhases,
                Is.EquivalentTo(new[] { 3 }), "acid only comes out in the last phase");

            var decimate = attacks["ThingDecimate"];
            Assert.That(decimate.Conditions.OfType<PhaseMegafaunaCondition>(), Is.Empty,
                "decimate is available in every phase");
        });

        // Decimate telegraphs a 5x5 box and only then spawns the spikes.
        await server.WaitPost(() => entMan.EventBus.RaiseLocalEvent(boss, new ThingAttackActionEvent
        {
            Attack = ThingAttackKind.Decimate,
            Target = new EntityCoordinates(testMap.Grid, 0, 0),
        }));
        await server.WaitRunTicks(30);

        await server.WaitAssertion(() =>
            Assert.That(CountEntities<ThingSpikeComponent>(entMan), Is.Zero, "spikes must be telegraphed first"));

        await server.WaitRunTicks(90);

        await server.WaitAssertion(() =>
        {
            Assert.That(CountEntities<ThingSpikeComponent>(entMan), Is.GreaterThan(0),
                "decimate must spawn its spike field");
        });

        await pair.CleanReturnAsync();
    }

    private static int CountEntities<T>(IEntityManager entMan) where T : IComponent
    {
        var count = 0;
        var query = entMan.EntityQueryEnumerator<T>();
        while (query.MoveNext(out _, out _))
            count++;

        return count;
    }
}
