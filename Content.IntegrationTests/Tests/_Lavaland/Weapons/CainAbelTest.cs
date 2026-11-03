// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Lavaland.Weapons.CainAbel;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Projectiles;
using Content.Shared.StatusEffectNew;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Lavaland.Weapons;

/// <summary>
/// Guards the Cain and Abel port: prototype wiring, the large mob combo and its damage bonus,
/// the stored blood wisps and their release, both throw modes, the crystal trap and the swing
/// ability. Nothing here needs a client.
/// </summary>
[TestFixture]
public sealed class CainAbelTest
{
    private const string Dagger = "LavalandCainDagger";
    private const string LargeMob = "MobLavalandGoliath";

    [Test]
    public async Task DaggerPrototypeCarriesEveryMechanic()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var dagger = entMan.SpawnEntity(Dagger, testMap.GridCoords);

            var melee = entMan.GetComponent<MeleeWeaponComponent>(dagger);
            Assert.That((float) melee.Damage.GetTotal(), Is.EqualTo(15f).Within(0.01f), "base damage");
            Assert.That(melee.AttackRate, Is.EqualTo(1.6667f).Within(0.001f), "large mob attack rate");
            Assert.That(melee.Range, Is.EqualTo(2f), "reach");

            var cain = entMan.GetComponent<CainAbelComponent>(dagger);
            Assert.That(cain.MaxCombo, Is.EqualTo(6));
            Assert.That(cain.Crystals, Is.True, "crystal throw mode is the default");

            var granted = entMan.GetComponent<ActionGrantComponent>(dagger).Actions.Select(a => a.Id).ToList();
            Assert.That(granted, Does.Contain("ActionCainAbelSwing"));
            Assert.That(granted, Does.Contain("ActionCainAbelReleaseWisps"));

            var itemGranted = entMan.GetComponent<ItemActionGrantComponent>(dagger).Actions.Select(a => a.Id).ToList();
            Assert.That(itemGranted, Does.Contain("ActionCainAbelSwing"));

            var abel = entMan.SpawnEntity("LavalandAbelDagger", testMap.GridCoords);
            Assert.That(entMan.HasComponent<CainAbelComponent>(abel), Is.True, "both twins share the mechanics");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ComboGrowsOnLargeHitsAndResets()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var hands = entMan.System<SharedHandsSystem>();
        var centre = testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f));

        EntityUid human = default;
        EntityUid mob = default;
        EntityUid small = default;
        EntityUid dagger = default;

        await server.WaitPost(() =>
        {
            FillFloor(server.System<SharedMapSystem>(), testMap);
            human = entMan.SpawnEntity("MobHuman", centre);
            mob = entMan.SpawnEntity(LargeMob, centre.Offset(new Vector2(6, 0)));
            small = entMan.SpawnEntity("MobHuman", centre.Offset(new Vector2(-6, 0)));
            dagger = entMan.SpawnEntity(Dagger, centre);
        });

        await server.WaitRunTicks(3);

        await server.WaitPost(() => Assert.That(hands.TryPickupAnyHand(human, dagger), Is.True, "test setup failed to hold the dagger"));
        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            var cain = entMan.GetComponent<CainAbelComponent>(dagger);
            var weapon = entMan.GetComponent<MeleeWeaponComponent>(dagger);

            var first = RaiseMeleeHit(entMan, dagger, human, mob, weapon.Damage);
            Assert.That(cain.Combo, Is.EqualTo(1), "the first large hit should build combo");
            Assert.That((float) first.BonusDamage.GetTotal(), Is.EqualTo(2.25f).Within(0.01f), "combo 0 damage boost");

            RaiseMeleeHit(entMan, dagger, human, mob, weapon.Damage);
            Assert.That(cain.Combo, Is.EqualTo(2));

            var third = RaiseMeleeHit(entMan, dagger, human, mob, weapon.Damage);
            Assert.That(cain.Combo, Is.EqualTo(3));
            Assert.That((float) third.BonusDamage.GetTotal(), Is.EqualTo(19.5f).Within(0.05f), "combo 2 damage boost is 15 * (2 * 1.15 - 1)");

            var rate = new GetMeleeAttackRateEvent(dagger, 0f, 1f, human);
            entMan.EventBus.RaiseLocalEvent(dagger, ref rate);
            Assert.That(rate.Rate, Is.EqualTo(1.6667f).Within(0.001f), "large mobs use the fast attack rate");

            // A small target takes the early return: no boost and no combo.
            RaiseMeleeHit(entMan, dagger, human, small, weapon.Damage);
            Assert.That(cain.Combo, Is.EqualTo(0), "hitting a small mob breaks the combo");

            rate = new GetMeleeAttackRateEvent(dagger, 0f, 1f, human);
            entMan.EventBus.RaiseLocalEvent(dagger, ref rate);
            Assert.That(rate.Rate, Is.EqualTo(1.25f).Within(0.001f), "small mobs use the slow attack rate");

            // Being attacked also resets the combo.
            RaiseMeleeHit(entMan, dagger, human, mob, weapon.Damage);
            RaiseMeleeHit(entMan, dagger, human, mob, weapon.Damage);
            Assert.That(cain.Combo, Is.EqualTo(2));

            var attacked = new AttackedEvent(dagger, small, centre);
            entMan.EventBus.RaiseLocalEvent(human, attacked);
            Assert.That(cain.Combo, Is.EqualTo(0), "an attacked holder loses the combo");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WispsFollowComboAndCanBeReleased()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var hands = entMan.System<SharedHandsSystem>();
        var centre = testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f));

        EntityUid human = default;
        EntityUid mob = default;
        EntityUid dagger = default;

        await server.WaitPost(() =>
        {
            FillFloor(server.System<SharedMapSystem>(), testMap);
            human = entMan.SpawnEntity("MobHuman", centre);
            mob = entMan.SpawnEntity(LargeMob, centre.Offset(new Vector2(6, 0)));
            dagger = entMan.SpawnEntity(Dagger, centre);
        });

        await server.WaitRunTicks(3);

        await server.WaitPost(() => Assert.That(hands.TryPickupAnyHand(human, dagger), Is.True));
        await server.WaitRunTicks(3);
        var timing = server.ResolveDependency<IGameTiming>();

        await server.WaitAssertion(() =>
        {
            var cain = entMan.GetComponent<CainAbelComponent>(dagger);
            var weapon = entMan.GetComponent<MeleeWeaponComponent>(dagger);
            for (var i = 0; i < 3; i++)
                RaiseMeleeHit(entMan, dagger, human, mob, weapon.Damage);

            Assert.That(cain.Combo, Is.EqualTo(3));
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var cain = entMan.GetComponent<CainAbelComponent>(dagger);
            Assert.That(cain.Wisps.Count, Is.EqualTo(3), "one wisp per combo point");
            Assert.That(CountPrototypes(entMan, "LavalandCainAbelWisp"), Is.EqualTo(3));
            foreach (var wisp in cain.Wisps)
                Assert.That(entMan.GetComponent<TransformComponent>(wisp).ParentUid, Is.EqualTo(human), "wisps follow the holder");

            var ev = new CainAbelReleaseWispsActionEvent
            {
                Performer = human,
                Target = entMan.GetComponent<TransformComponent>(mob).Coordinates,
            };
            // Action events are broadcast subscriptions, a plain directed raise never reaches them.
            entMan.EventBus.RaiseLocalEvent(human, (object) ev, broadcast: true);
            Assert.That(ev.Handled, Is.True, "wisps should be released at the target");
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            var cain = entMan.GetComponent<CainAbelComponent>(dagger);
            Assert.That(cain.Combo, Is.LessThan(3), "each fired wisp spends a combo point");
            Assert.That(CountPrototypes(entMan, "LavalandCainAbelWispProjectile"), Is.GreaterThan(0), "a wisp projectile should fly");
        });

        await server.WaitRunTicks((int) Math.Ceiling(0.5f * timing.TickRate) + 2);

        await server.WaitAssertion(() =>
        {
            var cain = entMan.GetComponent<CainAbelComponent>(dagger);
            Assert.That(cain.Combo, Is.EqualTo(0));
            Assert.That(cain.Wisps.Count, Is.EqualTo(0));
            Assert.That(CountPrototypes(entMan, "LavalandCainAbelWisp"), Is.EqualTo(0));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThrowModesFireProjectilesAndLeaveEngravedDagger()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var hands = entMan.System<SharedHandsSystem>();
        var centre = testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f));

        EntityUid human = default;
        EntityUid mob = default;
        EntityUid dagger = default;

        await server.WaitPost(() =>
        {
            FillFloor(server.System<SharedMapSystem>(), testMap);
            human = entMan.SpawnEntity("MobHuman", centre);
            mob = entMan.SpawnEntity(LargeMob, centre.Offset(new Vector2(6, 0)));
            dagger = entMan.SpawnEntity(Dagger, centre);
        });

        await server.WaitRunTicks(3);

        await server.WaitPost(() => Assert.That(hands.TryPickupAnyHand(human, dagger), Is.True));
        await server.WaitRunTicks(3);
        var timing = server.ResolveDependency<IGameTiming>();

        await server.WaitAssertion(() =>
        {
            var cain = entMan.GetComponent<CainAbelComponent>(dagger);
            Assert.That(cain.Crystals, Is.True);

            entMan.EventBus.RaiseLocalEvent(dagger, new UseInHandEvent(human));
            Assert.That(cain.Crystals, Is.False, "use in hand switches the throw mode");

            var launchBefore = CountPrototypes(entMan, "LavalandCainAbelThrowProjectileLaunch");
            var throwEv = new BeforeThrowEvent(dagger, new Vector2(4f, 0f), 10f, human);
            entMan.EventBus.RaiseLocalEvent(dagger, ref throwEv);

            Assert.That(throwEv.Cancelled, Is.True, "the dagger itself is never thrown");
            Assert.That(hands.IsHolding(human, dagger), Is.True, "the dagger stays in the hand");
            Assert.That(CountPrototypes(entMan, "LavalandCainAbelThrowProjectileLaunch"), Is.EqualTo(launchBefore + 1));
            Assert.That(cain.NextThrow, Is.Not.EqualTo(default(TimeSpan)), "the launch throw starts its cooldown");

            // Still on cooldown: a second throw does nothing.
            var again = new BeforeThrowEvent(dagger, new Vector2(4f, 0f), 10f, human);
            entMan.EventBus.RaiseLocalEvent(dagger, ref again);
            Assert.That(CountPrototypes(entMan, "LavalandCainAbelThrowProjectileLaunch"), Is.EqualTo(launchBefore + 1));
        });

        await server.WaitAssertion(() =>
        {
            var projectile = entMan.SpawnEntity("LavalandCainAbelThrowProjectileCrystal", centre);
            var hit = new ProjectileHitEvent(new DamageSpecifier(), mob, human);
            entMan.EventBus.RaiseLocalEvent(projectile, ref hit);

            Assert.That(CountPrototypes(entMan, "LavalandCainAbelEngraved"), Is.GreaterThan(0), "impact leaves an engraved dagger");
            Assert.That(CountPrototypes(entMan, "LavalandCainAbelChainBeam"), Is.GreaterThan(0), "impact draws the chain beam");
        });

        await server.WaitRunTicks((int) Math.Ceiling(1.2f * timing.TickRate) + 2);

        await server.WaitAssertion(() =>
        {
            Assert.That(CountPrototypes(entMan, "LavalandCainAbelCrystal"), Is.GreaterThanOrEqualTo(5), "the first two crystal rings should be placed");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CrystalTrapDamagesAndStuns()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var status = entMan.System<StatusEffectsSystem>();
        var centre = testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f));

        EntityUid mob = default;
        FixedPoint2 before = default;

        await server.WaitPost(() =>
        {
            FillFloor(server.System<SharedMapSystem>(), testMap);
            mob = entMan.SpawnEntity(LargeMob, centre);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            before = entMan.GetComponent<DamageableComponent>(mob).TotalDamage;
            entMan.SpawnEntity("LavalandCainAbelCrystal", entMan.GetComponent<TransformComponent>(mob).Coordinates);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var after = entMan.GetComponent<DamageableComponent>(mob).TotalDamage;
            Assert.That((float) after, Is.GreaterThan((float) before), "a crystal trap damages a mob standing on it");
            Assert.That(status.TryGetStatusEffect(mob, "LavalandStatusEffectDaggerStun", out _), Is.True, "a crystal trap stuns");
            Assert.That(CountPrototypes(entMan, "LavalandCainAbelStunVisual"), Is.GreaterThan(0), "the stun overlay is drawn");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SwingDamagesAroundUserAndParriesProjectiles()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var hands = entMan.System<SharedHandsSystem>();
        var status = entMan.System<StatusEffectsSystem>();
        var centre = testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f));

        EntityUid human = default;
        EntityUid mob = default;
        EntityUid shooter = default;
        EntityUid dagger = default;

        await server.WaitPost(() =>
        {
            FillFloor(server.System<SharedMapSystem>(), testMap);
            human = entMan.SpawnEntity("MobHuman", centre);
            mob = entMan.SpawnEntity(LargeMob, centre.Offset(new Vector2(1, 0)));
            shooter = entMan.SpawnEntity("MobHuman", centre.Offset(new Vector2(-3, 0)));
            dagger = entMan.SpawnEntity(Dagger, centre);
        });

        await server.WaitRunTicks(3);

        await server.WaitPost(() => Assert.That(hands.TryPickupAnyHand(human, dagger), Is.True));
        await server.WaitRunTicks(3);
        var timing = server.ResolveDependency<IGameTiming>();

        FixedPoint2 beforeSwing = default;

        await server.WaitAssertion(() =>
        {
            beforeSwing = entMan.GetComponent<DamageableComponent>(mob).TotalDamage;
            entMan.EventBus.RaiseLocalEvent(human, (object) new CainAbelSwingActionEvent { Performer = human }, broadcast: true);
            Assert.That(status.TryGetStatusEffect(human, "LavalandStatusEffectDaggerSwinging", out _), Is.True, "the swing applies its status effect");
            Assert.That(CountPrototypes(entMan, "LavalandCainAbelSlash"), Is.GreaterThan(0), "the slash overlay is drawn");
        });

        await server.WaitRunTicks((int) Math.Ceiling(0.6f * timing.TickRate) + 2);

        await server.WaitAssertion(() =>
        {
            var after = entMan.GetComponent<DamageableComponent>(mob).TotalDamage;
            Assert.That((float) after, Is.GreaterThan((float) beforeSwing), "the swing damages nearby living mobs");

            var projectile = entMan.SpawnEntity("LavalandCainAbelWispProjectile", centre);
            entMan.System<SharedPhysicsSystem>().SetLinearVelocity(projectile, new Vector2(5f, 0f));

            var damage = new DamageSpecifier();
            damage.DamageDict["Slash"] = 10;
            var hit = new ProjectileHitEvent(damage, human, shooter);
            entMan.EventBus.RaiseLocalEvent(projectile, ref hit);

            Assert.That(hit.Damage.Empty, Is.True, "a parried projectile deals no damage");
            Assert.That(entMan.GetComponent<ProjectileComponent>(projectile).DeleteOnCollide, Is.False, "the parried projectile keeps flying");
            Assert.That(entMan.GetComponent<PhysicsComponent>(projectile).LinearVelocity.X, Is.LessThan(0f), "the parried projectile is turned around");
        });

        await pair.CleanReturnAsync();
    }

    private static MeleeHitEvent RaiseMeleeHit(IEntityManager entMan, EntityUid dagger, EntityUid user, EntityUid target, DamageSpecifier damage)
    {
        var ev = new MeleeHitEvent(new List<EntityUid> { target }, user, dagger, damage, null, entMan.GetComponent<TransformComponent>(target).Coordinates);
        entMan.EventBus.RaiseLocalEvent(dagger, ev);
        return ev;
    }

    private static void FillFloor(SharedMapSystem mapSys, TestMapData testMap)
    {
        for (var x = -4; x <= 8; x++)
        {
            for (var y = -4; y <= 4; y++)
                mapSys.SetTile(testMap.Grid, new EntityCoordinates(testMap.Grid, x, y), testMap.Tile.Tile);
        }
    }

    private static int CountPrototypes(IEntityManager entMan, string prototypeId)
    {
        var count = 0;
        var query = entMan.AllEntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var meta))
        {
            if (meta.EntityPrototype?.ID == prototypeId)
                count++;
        }

        return count;
    }
}
