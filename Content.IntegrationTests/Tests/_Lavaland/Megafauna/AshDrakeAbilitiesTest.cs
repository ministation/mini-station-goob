// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Goobstation.Common.Gibbing;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Lavaland.Megafauna;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Conditions;
using Content.Shared._Lavaland.Megafauna.Events;
using Content.Shared._Lavaland.Megafauna.Selectors;
using Content.Shared._Lavaland.MobPhases;
using Content.Shared.Actions.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

/// <summary>
/// Guards the ash drake kit: every action its selector tree can roll is granted to the mob,
/// and the phase two firestorm is wired to a real action and actually reachable.
/// </summary>
[TestFixture]
public sealed class AshDrakeAbilitiesTest
{
    private const string DrakeProto = "LavalandBossAshDrake";
    private const string ActionGroup = "AshDrakeActions";
    private const string FirestormAction = "ActionDrakeFirestorm";

    [Test]
    public async Task DrakeGrantsEveryActionItsSelectorRolls()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        EntityUid drake = default;
        await server.WaitPost(() =>
        {
            drake = entMan.SpawnEntity(DrakeProto, testMap.GridCoords);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.TryGetComponent<ActionsComponent>(drake, out var actions), Is.True,
                "the ash drake is missing Actions");

            var granted = new List<string>();
            foreach (var action in actions!.Actions)
            {
                if (entMan.Deleted(action))
                    continue;

                var actionMeta = entMan.GetComponent<MetaDataComponent>(action);
                granted.Add(actionMeta.EntityPrototype?.ID ?? action.ToString());
            }

            var group = protoMan.Index<MegafaunaSelectorPrototype>(ActionGroup).Selector;
            Assert.That(group, Is.InstanceOf<GroupMegafaunaSelector>(),
                ActionGroup + " should be a group selector");

            var children = new List<string>();
            foreach (var child in ((GroupMegafaunaSelector) group).Children)
            {
                switch (child)
                {
                    case ProtoIdMegafaunaSelector protoId:
                        children.Add(protoId.Id.Id);
                        break;
                    case NestedMegafaunaSelector nested:
                        children.Add(nested.Id.Id);
                        break;
                }
            }

            Assert.That(children, Does.Contain(FirestormAction),
                ActionGroup + " does not roll the firestorm: " + string.Join(", ", children));

            // Every rolled selector must point at an action prototype the drake was actually granted.
            foreach (var actionId in children)
            {
                Assert.That(protoMan.HasIndex<EntityPrototype>(actionId), Is.True,
                    actionId + " has no entity prototype");

                Assert.That(protoMan.HasIndex<MegafaunaSelectorPrototype>(actionId), Is.True,
                    actionId + " has no selector prototype");

                var selector = protoMan.Index<MegafaunaSelectorPrototype>(actionId).Selector;
                Assert.That(selector, Is.InstanceOf<PerformActionSelector>(),
                    actionId + " should be performed by the AI");

                var perform = (PerformActionSelector) selector;
                Assert.That(perform.ActionId.Id, Is.EqualTo(actionId),
                    actionId + " wires a different action: " + perform.ActionId.Id);

                Assert.That(granted, Does.Contain(actionId),
                    "the drake was not granted " + actionId + ", granted: " + string.Join(", ", granted));
            }

            // The firestorm is the phase two attack: without MobPhases the phase condition never passes.
            Assert.That(entMan.TryGetComponent<MobPhasesComponent>(drake, out var phases), Is.True,
                "the drake needs MobPhases or the firestorm selector never fires");
            Assert.That(phases!.CurrentPhase, Is.EqualTo(1), "the fight starts in the first phase");

            var stage = entMan.GetComponent<MegafaunaStageComponent>(drake);
            Assert.That(stage.MaxStage, Is.GreaterThanOrEqualTo(2), "the firestorm phase needs a HUD stage");

            var firestorm = protoMan.Index<MegafaunaSelectorPrototype>(FirestormAction).Selector;
            PhaseMegafaunaCondition gate = null;
            foreach (var condition in firestorm.Conditions)
            {
                if (condition is PhaseMegafaunaCondition phaseCondition)
                    gate = phaseCondition;
            }

            Assert.That(gate, Is.Not.Null, FirestormAction + " should be gated by a phase condition");
            Assert.That(Array.IndexOf(gate.RequiredPhases, 2), Is.GreaterThanOrEqualTo(0),
                FirestormAction + " should be unlocked in the second phase");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The swoop is TG's lava swoop, not a teleport: the drake is untouchable while it flies to
    /// the target and the landing tile is slammed. Damage has to be cancelled mid air and go
    /// through again once the drake has landed.
    /// </summary>
    [Test]
    public async Task SwoopIsInvulnerableInFlightAndSlamsOnLanding()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var xformSys = server.System<SharedTransformSystem>();
        var damageableSys = server.System<DamageableSystem>();
        var thresholdsSys = server.System<MobThresholdSystem>();
        var testMap = await pair.CreateTestMap();

        EntityUid drake = default;
        EntityUid victim = default;
        EntityCoordinates landing = default;
        Vector2 takeoff = default;
        Vector2 landingPos = default;

        await server.WaitPost(() =>
        {
            // The default test map is a single tile, the swoop needs room to fly over.
            var tile = testMap.Tile.Tile;
            for (var x = -6; x <= 6; x++)
            {
                for (var y = -6; y <= 6; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
            }

            drake = entMan.SpawnEntity(DrakeProto, testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            landing = testMap.GridCoords.Offset(new Vector2(4.5f, 0.5f));
            victim = entMan.SpawnEntity("MobCow", landing.Offset(new Vector2(0f, 1f)));

            // Keep the victim alive through the slam: a dead dummy would prove nothing.
            var thresholds = entMan.GetComponent<MobThresholdsComponent>(victim);
            thresholdsSys.SetMobStateThreshold(victim, FixedPoint2.Zero, MobState.Alive, thresholds);
            thresholdsSys.SetMobStateThreshold(victim, FixedPoint2.New(100000), MobState.Dead, thresholds);
            entMan.EnsureComponent<DamageGibImmuneComponent>(victim);

            takeoff = xformSys.GetWorldPosition(entMan.GetComponent<TransformComponent>(drake));
            landingPos = xformSys.ToMapCoordinates(landing).Position;
        });

        await server.WaitRunTicks(5);

        // ActionDrakeSlam raises exactly this event, so the test drives the same code path.
        await server.WaitPost(() =>
        {
            entMan.EventBus.RaiseLocalEvent(drake, new MegafaunaBlinkActionEvent
            {
                Performer = drake,
                Target = landing,
            });
        });

        // Halfway through the 1.5 s flight. The server ticks at 30 Hz, so 22 ticks is ~0.73 s.
        await server.WaitRunTicks(22);

        float victimDamageInFlight = 0f;
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<GodmodeComponent>(drake), Is.True,
                "the swooping drake has to be invulnerable while it flies");

            var mid = xformSys.GetWorldPosition(entMan.GetComponent<TransformComponent>(drake));
            Assert.That(mid.X, Is.GreaterThan(takeoff.X + 0.1f),
                "the drake has to fly to the target instead of teleporting there");
            Assert.That(mid.X, Is.LessThan(landingPos.X - 0.1f),
                "the drake should still be in the air halfway through the swoop");

            damageableSys.TryChangeDamage(drake, new DamageSpecifier { DamageDict = { ["Heat"] = 10 } }, ignoreResistances: true);
            Assert.That(entMan.GetComponent<DamageableComponent>(drake).TotalDamage.Float(), Is.EqualTo(0f),
                "damage during the swoop has to be cancelled");

            victimDamageInFlight = entMan.GetComponent<DamageableComponent>(victim).TotalDamage.Float();
        });

        // Finish the flight and let the landing slam resolve its damage delay.
        await server.WaitRunTicks(90);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<GodmodeComponent>(drake), Is.False,
                "the drake has to be vulnerable again after it lands");

            var landed = xformSys.GetWorldPosition(entMan.GetComponent<TransformComponent>(drake));
            Assert.That(landed.X, Is.EqualTo(landingPos.X).Within(0.6f), "the drake did not land on the target tile");
            Assert.That(landed.Y, Is.EqualTo(landingPos.Y).Within(0.6f), "the drake did not land on the target tile");

            var victimDamage = entMan.GetComponent<DamageableComponent>(victim).TotalDamage.Float();
            Assert.That(victimDamage, Is.GreaterThan(victimDamageInFlight),
                "the landing slam has to damage the mobs around the landing tile");

            // TG's slam skips the drake itself, the landing must not hurt it.
            Assert.That(entMan.GetComponent<DamageableComponent>(drake).TotalDamage.Float(), Is.EqualTo(0f),
                "the drake hurt itself with its own landing slam");

            damageableSys.TryChangeDamage(drake, new DamageSpecifier { DamageDict = { ["Heat"] = 10 } }, ignoreResistances: true);
            Assert.That(entMan.GetComponent<DamageableComponent>(drake).TotalDamage.Float(), Is.GreaterThan(0f),
                "the drake has to take damage after the swoop ends");
        });

        await pair.CleanReturnAsync();
    }
}
