// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using Content.Server._Lavaland.BossMechanics;
using Content.Shared._Lavaland.Megafauna;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Selectors;
using Content.Shared._Lavaland.MobPhases;
using Content.Shared.Actions.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

/// <summary>
/// Guards the demonic frost miner port: every attack the AI selector can roll is granted to
/// the boss, the enrage heals it back to full below a quarter health and a crusher kill
/// drops the chest and the ice block talisman trophy.
/// </summary>
[TestFixture]
public sealed class FrostMinerBossTest
{
    private const string BossProto = "LavalandBossDemonicFrostMiner";
    private const string ActionsSelector = "FrostMinerActions";
    private const string EnragedAction = "ActionFrostMinerSnowballGunEnraged";
    private const string CrusherProto = "WeaponCrusher";

    [Test]
    public async Task FrostMinerGrantsEveryActionItsSelectorRolls()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        EntityUid boss = default;
        await server.WaitPost(() =>
        {
            boss = entMan.SpawnEntity(BossProto, testMap.GridCoords);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.TryGetComponent<ActionsComponent>(boss, out var actions), Is.True,
                "the frost miner must have an action container");

            var granted = new List<string>();
            if (actions != null)
            {
                foreach (var action in actions.Actions)
                {
                    if (entMan.Deleted(action))
                        continue;

                    if (entMan.GetComponent<MetaDataComponent>(action).EntityPrototype is { } proto)
                        granted.Add(proto.ID);
                }
            }

            var group = protoMan.Index<MegafaunaSelectorPrototype>(ActionsSelector).Selector;
            Assert.That(group, Is.InstanceOf<GroupMegafaunaSelector>(),
                "the frost miner action selector must be a weighted group");

            var selectorIds = new List<string>();
            if (group is GroupMegafaunaSelector groupSelector)
            {
                foreach (var child in groupSelector.Children)
                {
                    if (child is ProtoIdMegafaunaSelector protoId)
                        selectorIds.Add(protoId.Id.Id);
                }
            }

            // Both MobPhases gates must be covered, including the fast attacks of the enraged phase.
            Assert.That(selectorIds, Does.Contain(EnragedAction),
                "the second phase must be able to roll the enraged attacks");

            foreach (var selectorId in selectorIds)
            {
                var selector = protoMan.Index<MegafaunaSelectorPrototype>(selectorId).Selector;
                Assert.That(selector, Is.InstanceOf<PerformActionSelector>(),
                    $"{selectorId} must be performed by the AI");

                if (selector is not PerformActionSelector perform)
                    continue;

                var actionId = perform.ActionId.Id;
                Assert.That(protoMan.HasIndex<EntityPrototype>(actionId), Is.True,
                    $"{actionId} has no entity prototype");
                Assert.That(granted, Does.Contain(actionId),
                    $"the frost miner was never granted {actionId}");
            }

            var phases = entMan.GetComponent<MobPhasesComponent>(boss);
            Assert.That(phases.CurrentPhase, Is.EqualTo(1), "the frost miner starts in the first phase");

            var stage = entMan.GetComponent<MegafaunaStageComponent>(boss);
            Assert.That(stage.MaxStage, Is.EqualTo(2), "the frost miner has two HUD stages");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FrostMinerEnragesAndDropsLoot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid boss = default;
        EntityUid crusher = default;
        await server.WaitPost(() =>
        {
            boss = entMan.SpawnEntity(BossProto, testMap.GridCoords);
            crusher = entMan.SpawnEntity(CrusherProto, testMap.GridCoords);
        });

        await server.WaitRunTicks(5);

        // 1200 of 1500 health is past the 25% enrage threshold.
        await server.WaitPost(() =>
        {
            entMan.System<DamageableSystem>().TryChangeDamage(
                boss,
                new DamageSpecifier { DamageDict = { { "Blunt", 1200f } } },
                true);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.TryGetComponent<FrostMinerEnrageComponent>(boss, out var enrage), Is.True,
                "the frost miner must carry the enrage component");
            Assert.That(enrage?.Enraged, Is.True, "the frost miner must enrage below a quarter health");

            var damageable = entMan.GetComponent<DamageableComponent>(boss);
            Assert.That(damageable.TotalDamage.Float(), Is.EqualTo(0f),
                "enraging must heal the frost miner back to full");
        });

        // Kill it with the crusher so the crusher-only trophy is part of the loot.
        await server.WaitPost(() =>
        {
            entMan.System<DamageableSystem>().TryChangeDamage(
                boss,
                new DamageSpecifier { DamageDict = { { "Blunt", 2000f } } },
                true,
                true,
                crusher);
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            var spawned = new List<string>();
            var query = entMan.AllEntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out _, out var meta))
            {
                if (meta.EntityPrototype is { } proto)
                    spawned.Add(proto.ID);
            }

            Assert.Multiple(() =>
            {
                Assert.That(spawned, Does.Contain("LavalandCrateFrostMiner"),
                    "the frost miner must drop its chest");
                Assert.That(spawned, Does.Contain("LavalandIceBlockTalisman"),
                    "a crusher kill must drop the ice block talisman trophy");
            });
        });

        await pair.CleanReturnAsync();
    }
}
