// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Numerics;
using Content.Server._Lavaland.Weapons.Resonator;
using Content.Shared._Lavaland.ItemUpgrades.Components;
using Content.Shared._Lavaland.Weapons.Resonator;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Projectiles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Lavaland.Items;

[TestFixture]
[TestOf(typeof(ResonatorComponent))]
public sealed class ResonatorTest
{
    private static readonly string[] Modkits =
    {
        "PKAUpgradeCooldown",
        "PKAUpgradeMiningExplosion",
        "PKAUpgradeOffensiveExplosion",
        "PKAUpgradeOffensiveMiningExplosion",
        "PKAUpgradeRepeater",
        "PKAUpgradeInfernalRepeater",
        "PKAUpgradeLifesteal",
        "PKAUpgradeResonatorBlast",
        "PKAUpgradeDeathSyphon",
        "PKAUpgradeHumanPassthrough",
        "PKAUpgradeSuperChassis",
        "PKAUpgradeHyperChassis",
        "PKAUpgradeTracer",
    };

    [Test]
    public async Task ResonanceFieldBurstsAndMines()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var coords = testMap.GridCoords;

        EntityUid rock = default;
        EntityUid mob = default;

        await server.WaitPost(() =>
        {
            rock = entMan.SpawnEntity("AsteroidRockCoal", coords);
            mob = entMan.SpawnEntity("MobHuman", coords.Offset(new Vector2(0.2f, 0)));
            entMan.System<ResonatorSystem>().CreateField(coords, null, null, ResonatorMode.Auto, 1f, 100);
        });

        // Auto fields burst after two seconds.
        await server.WaitRunTicks(160);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(rock), Is.True, "the field should crush the rock on its tile");
            var damageable = entMan.GetComponent<DamageableComponent>(mob);
            Assert.That(damageable.TotalDamage.Value, Is.GreaterThan(0), "the field should damage living creatures on its tile");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ExplosionModifierDrillsAndDamages()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var coords = testMap.GridCoords;

        EntityUid rock = default;
        EntityUid mob = default;

        await server.WaitPost(() =>
        {
            rock = entMan.SpawnEntity("AsteroidRockCoal", coords.Offset(new Vector2(0.8f, 0)));
            mob = entMan.SpawnEntity("MobHuman", coords.Offset(new Vector2(-0.8f, 0)));
            var target = entMan.SpawnEntity("MobHuman", coords);

            var projectile = entMan.SpawnEntity("BulletKinetic", coords);
            var modifiers = entMan.AddComponent<PkaProjectileExplosionComponent>(projectile);
            modifiers.Turfs = true;
            modifiers.MobDamageModifier = 1f;

            var ev = new ProjectileHitEvent(new DamageSpecifier { DamageDict = { { "Blunt", 10f } } }, target);
            entMan.EventBus.RaiseLocalEvent(projectile, ref ev);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(rock), Is.True, "the explosion should drill nearby rock");
            var damageable = entMan.GetComponent<DamageableComponent>(mob);
            Assert.That(damageable.TotalDamage.Value, Is.GreaterThan(0), "the explosion should damage nearby creatures");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ModkitsInstallAndEject()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var coords = testMap.GridCoords;

        var problems = new List<string>();

        await server.WaitPost(() =>
        {
            var gun = entMan.SpawnEntity("WeaponProtoKineticAccelerator", coords);
            var slots = entMan.System<ItemSlotsSystem>();

            foreach (var proto in Modkits)
            {
                var kit = entMan.SpawnEntity(proto, coords);
                if (!entMan.HasComponent<ItemUpgradeComponent>(kit))
                    problems.Add($"{proto} is not an item upgrade");

                if (!slots.TryInsert(gun, "upgrade_slot_1", kit, null))
                    problems.Add($"{proto} did not fit in an accelerator upgrade slot");

                if (proto == "PKAUpgradeDeathSyphon"
                    && !entMan.HasComponent<PkaDeathSyphonComponent>(gun))
                    problems.Add("the death syphon modkit did not add its component to the accelerator");

                if (!slots.TryEject(gun, "upgrade_slot_1", null, out var ejected, doAfter: false))
                    problems.Add($"{proto} did not eject from the accelerator");
                else if (ejected != kit)
                    problems.Add($"{proto} ejected as a different item");

                if (entMan.HasComponent<PkaDeathSyphonComponent>(gun))
                    problems.Add($"ejecting {proto} did not remove the death syphon component");

                entMan.DeleteEntity(kit);
            }

            entMan.DeleteEntity(gun);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(problems, Is.Empty, string.Join("; ", problems));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MiningCacheSpawnsModkit()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var coords = testMap.GridCoords;

        await server.WaitPost(() =>
        {
            entMan.SpawnEntity("LavalandMiningCacheSpawner", coords);
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            var modkits = 0;
            var query = entMan.EntityQueryEnumerator<ItemUpgradeComponent>();
            while (query.MoveNext(out var uid, out _))
            {
                if (!entMan.Deleted(uid))
                    modkits++;
            }

            Assert.That(modkits, Is.GreaterThan(0), "the mining cache should spawn one random modkit");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ModeCycling()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var coords = testMap.GridCoords;

        var basicModes = new List<ResonatorMode>();
        var upgradedModes = new List<ResonatorMode>();

        await server.WaitPost(() =>
        {
            var basic = entMan.SpawnEntity("LavalandResonator", coords);
            var basicComp = entMan.GetComponent<ResonatorComponent>(basic);
            basicModes.Add(basicComp.Mode);
            entMan.EventBus.RaiseLocalEvent(basic, new UseInHandEvent(basic));
            basicModes.Add(basicComp.Mode);
            entMan.EventBus.RaiseLocalEvent(basic, new UseInHandEvent(basic));
            basicModes.Add(basicComp.Mode);

            var upgraded = entMan.SpawnEntity("LavalandResonatorUpgraded", coords);
            var upgradedComp = entMan.GetComponent<ResonatorComponent>(upgraded);
            upgradedModes.Add(upgradedComp.Mode);
            entMan.EventBus.RaiseLocalEvent(upgraded, new UseInHandEvent(upgraded));
            upgradedModes.Add(upgradedComp.Mode);
            entMan.EventBus.RaiseLocalEvent(upgraded, new UseInHandEvent(upgraded));
            upgradedModes.Add(upgradedComp.Mode);
            entMan.EventBus.RaiseLocalEvent(upgraded, new UseInHandEvent(upgraded));
            upgradedModes.Add(upgradedComp.Mode);

            entMan.DeleteEntity(basic);
            entMan.DeleteEntity(upgraded);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(basicModes, Is.EqualTo(new List<ResonatorMode>
            {
                ResonatorMode.Auto,
                ResonatorMode.Manual,
                ResonatorMode.Auto,
            }), "a resonator without matrix mode should cycle between auto and manual");

            Assert.That(upgradedModes, Is.EqualTo(new List<ResonatorMode>
            {
                ResonatorMode.Auto,
                ResonatorMode.Manual,
                ResonatorMode.Matrix,
                ResonatorMode.Auto,
            }), "the upgraded resonator should cycle through all three modes");
        });

        await pair.CleanReturnAsync();
    }
}
