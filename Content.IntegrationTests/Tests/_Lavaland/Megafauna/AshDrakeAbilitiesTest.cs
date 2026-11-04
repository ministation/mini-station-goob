// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using Content.Shared._Lavaland.Megafauna;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Conditions;
using Content.Shared._Lavaland.Megafauna.Selectors;
using Content.Shared._Lavaland.MobPhases;
using Content.Shared.Actions.Components;
using Robust.Shared.GameObjects;
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
}
