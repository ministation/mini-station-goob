// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server._Lavaland.BossMechanics;
using Content.Shared._Lavaland.Megafauna;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Conditions;
using Content.Shared._Lavaland.Megafauna.Selectors;
using Content.Shared._Lavaland.MobPhases;
using Content.Shared.Actions.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

/// <summary>
/// Guards the wendigo kit: every attack its selector tree can roll is granted to the mob,
/// the enraged attacks are gated on the second phase, and the loot drops on a crusher kill.
/// </summary>
[TestFixture]
[TestOf(typeof(WendigoBossComponent))]
public sealed class WendigoBossTest
{
    private const string WendigoProto = "LavalandBossWendigo";

    // Every attack plan the wendigo AI can roll, with the phase it belongs to.
    private static readonly string[] PhaseOnePlans =
    {
        "WendigoSlamPlan",
        "WendigoTeleportPlan",
        "WendigoCirclePlan",
        "WendigoSpiralPlan",
    };

    private static readonly string[] PhaseTwoPlans =
    {
        "WendigoCircleEnragedPlan",
        "WendigoSpiralEnragedPlan",
        "WendigoWavePlan",
    };

    [Test]
    public async Task WendigoGrantsEveryActionItsSelectorRolls()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        EntityUid wendigo = default;
        await server.WaitPost(() => wendigo = entMan.SpawnAtPosition(WendigoProto, testMap.GridCoords));

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.TryGetComponent<ActionsComponent>(wendigo, out var actions), Is.True,
                "the wendigo is missing Actions");

            var granted = new List<string>();
            foreach (var action in actions!.Actions)
            {
                if (entMan.Deleted(action))
                    continue;

                var meta = entMan.GetComponent<MetaDataComponent>(action);
                granted.Add(meta.EntityPrototype?.ID ?? action.ToString());
            }

            Assert.That(protoMan.HasIndex<MegafaunaSelectorPrototype>("Wendigo"), Is.True,
                "the AI root selector must exist");

            foreach (var plan in PhaseOnePlans.Concat(PhaseTwoPlans))
            {
                Assert.That(protoMan.HasIndex<MegafaunaSelectorPrototype>(plan), Is.True,
                    plan + " has no selector prototype");

                var selector = protoMan.Index<MegafaunaSelectorPrototype>(plan).Selector;
                Assert.That(selector, Is.InstanceOf<PerformActionSelector>(),
                    plan + " should be performed by the AI");

                var actionId = ((PerformActionSelector) selector).ActionId.Id;
                Assert.That(granted, Does.Contain(actionId),
                    "the wendigo was not granted " + actionId + ", granted: " + string.Join(", ", granted));
            }

            // The enraged forms are phase two only, otherwise a full health wendigo fires them.
            foreach (var plan in PhaseTwoPlans)
            {
                var phaseGate = protoMan.Index<MegafaunaSelectorPrototype>(plan).Selector.Conditions
                    .OfType<PhaseMegafaunaCondition>()
                    .FirstOrDefault();

                Assert.That(phaseGate, Is.Not.Null, plan + " should be gated by a phase condition");
                Assert.That(Array.IndexOf(phaseGate!.RequiredPhases, 2), Is.GreaterThanOrEqualTo(0),
                    plan + " should be unlocked in the second phase");
            }

            Assert.That(entMan.TryGetComponent<MobPhasesComponent>(wendigo, out var phases), Is.True,
                "the wendigo needs MobPhases or the enraged attacks never fire");
            Assert.That(phases!.CurrentPhase, Is.EqualTo(1), "the fight starts in the first phase");

            var stage = entMan.GetComponent<MegafaunaStageComponent>(wendigo);
            Assert.That(stage.MaxStage, Is.GreaterThanOrEqualTo(2), "the enraged phase needs a HUD stage");

            Assert.That(entMan.TryGetComponent<BossEnrageComponent>(wendigo, out var enrage), Is.True,
                "the wendigo needs BossEnrage to speed up when enraged");
            Assert.That(enrage!.HealthFraction, Is.EqualTo(0.5f).Within(0.001f),
                "TG enrages the wendigo at half health");

            Assert.That(entMan.TryGetComponent<WendigoBossComponent>(wendigo, out _), Is.True,
                "the wendigo needs its boss component for the stomp and shockwaves");
            Assert.That(protoMan.HasIndex<EntityPrototype>("LavalandWendigoSlamBurst"), Is.True,
                "the slam action spawns an entity with the slam component");
            Assert.That(protoMan.HasIndex<EntityPrototype>("LavalandWendigoStompBurst"), Is.True,
                "the movement stomp needs its burst prototype");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WendigoDropsChestAndTrophyOnCrusherKill()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid wendigo = default;
        EntityUid crusher = default;

        await server.WaitPost(() =>
        {
            wendigo = entMan.SpawnAtPosition(WendigoProto, testMap.GridCoords);
            crusher = entMan.SpawnAtPosition("WeaponCrusher", testMap.GridCoords);
        });

        await server.WaitRunTicks(5);

        await server.WaitPost(() =>
        {
            // Killing blow from a crusher, so both the chest and the horn trophy drop.
            entMan.System<DamageableSystem>().TryChangeDamage(
                wendigo,
                new DamageSpecifier { DamageDict = { { "Blunt", 10000f } } },
                ignoreResistances: true,
                origin: crusher);
        });

        await server.WaitRunTicks(10);

        await server.WaitAssertion(() =>
        {
            var counts = CountPrototypes(entMan);
            Assert.Multiple(() =>
            {
                Assert.That(counts.GetValueOrDefault("LavalandCrateWendigo"), Is.GreaterThanOrEqualTo(1),
                    "the wendigo should drop its chest");
                Assert.That(counts.GetValueOrDefault("LavalandWendigoHorn"), Is.GreaterThanOrEqualTo(1),
                    "a crusher kill should drop the horn trophy");
            });

            EntityUid? crate = null;
            foreach (var uid in entMan.GetEntities())
            {
                if (entMan.TryGetComponent<MetaDataComponent>(uid, out var meta)
                    && meta.EntityPrototype?.ID == "LavalandCrateWendigo")
                {
                    crate = uid;
                    break;
                }
            }

            Assert.That(crate, Is.Not.Null);
            Assert.That(entMan.TryGetComponent<ContainerManagerComponent>(crate.Value, out var containers), Is.True,
                "the wendigo chest must have a container");

            var contained = containers!.Containers.Values
                .SelectMany(container => container.ContainedEntities)
                .Count();

            Assert.That(contained, Is.GreaterThan(0), "the wendigo chest must come filled");
        });

        await pair.CleanReturnAsync();
    }

    private static Dictionary<string, int> CountPrototypes(IEntityManager entMan)
    {
        var counts = new Dictionary<string, int>();
        foreach (var uid in entMan.GetEntities())
        {
            if (!entMan.TryGetComponent<MetaDataComponent>(uid, out var meta))
                continue;

            if (meta.EntityPrototype is not { } proto)
                continue;

            counts[proto.ID] = counts.GetValueOrDefault(proto.ID) + 1;
        }

        return counts;
    }
}
