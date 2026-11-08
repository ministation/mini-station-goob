// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using System.Collections.Generic;
using Content.Server._Lavaland.Weapons;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Stealth.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Lavaland.Weapons;

/// <summary>
/// TG cursed katana parity: swing sequences trigger combo moves — the dark
/// cloak hides the wielder, and the shatter finisher replaces the damage with
/// a heavy mining-fauna hit before the blade vanishes.
/// </summary>
[TestFixture]
public sealed class CursedKatanaTest
{
    [Test]
    public async Task DarkCloakSequenceGrantsStealth()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();

        EntityUid katana = default;
        EntityUid user = default;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var map);
            var coords = new MapCoordinates(0, 0, map);
            katana = entMan.SpawnEntity("LavalandCursedKatana", coords);
            user = entMan.SpawnEntity("MobHuman", coords);
        });

        await server.WaitRunTicks(2);

        // LRLR: whiff, hit, whiff, hit — the fourth swing completes the cloak.
        await server.WaitPost(() =>
        {
            RaiseSwing(entMan, katana, user, hit: false, applyDamage: false);
            RaiseSwing(entMan, katana, user, hit: true, victims: [user], applyDamage: false);
            RaiseSwing(entMan, katana, user, hit: false, applyDamage: false);

            Assert.That(entMan.HasComponent<StealthComponent>(user), Is.False,
                "the cloak must not trigger before the sequence completes");

            RaiseSwing(entMan, katana, user, hit: true, victims: [user], applyDamage: false);
        });

        await server.WaitRunTicks(2);

        Assert.That(entMan.HasComponent<StealthComponent>(user), Is.True,
            "finishing LRLR must hide the wielder");

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ShatterHitsFaunaHardAndVanishesBlade()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var damageable = server.System<DamageableSystem>();

        EntityUid katana = default;
        EntityUid user = default;
        EntityUid fauna = default;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var map);
            var coords = new MapCoordinates(0, 0, map);
            katana = entMan.SpawnEntity("LavalandCursedKatana", coords);
            user = entMan.SpawnEntity("MobHuman", coords);
            fauna = entMan.SpawnEntity("MobLavalandGutlunch", coords.Offset(new Vector2(0, 1)));
        });

        await server.WaitRunTicks(2);

        // RLRL: the fourth swing is the shatter finisher on the fauna (its last
        // symbol is a whiff, and that whiff carries the replaced damage).
        await server.WaitPost(() =>
        {
            RaiseSwing(entMan, katana, user, hit: true, victims: [fauna], applyDamage: true);
            RaiseSwing(entMan, katana, user, hit: false, applyDamage: false);
            RaiseSwing(entMan, katana, user, hit: true, victims: [fauna], applyDamage: true);
            RaiseSwing(entMan, katana, user, hit: false, applyDamage: true);
        });

        await server.WaitRunTicks(2);

        var faunaDamage = entMan.GetComponent<DamageableComponent>(fauna).Damage.DamageDict.TryGetValue("Slash", out var s) ? (float) s : 0f;
        Assert.That(faunaDamage, Is.EqualTo(75f), "the shatter must replace the swing with a 75 mining-fauna hit");
        Assert.That(entMan.EntityExists(katana), Is.False, "the blade should vanish after a shatter");

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Raises a melee hit on the katana like the melee system would; optionally
    /// applies the resulting damage to the victims like MeleeWeaponSystem does.
    /// </summary>
    private static void RaiseSwing(
        IEntityManager entMan,
        EntityUid katana,
        EntityUid user,
        bool hit,
        List<EntityUid>? victims = null,
        bool applyDamage = false)
    {
        var targets = hit ? (victims ?? [user]) : new List<EntityUid>();
        var ev = new MeleeHitEvent(targets, user, katana, new DamageSpecifier(), null, entMan.GetComponent<TransformComponent>(user).Coordinates);
        entMan.EventBus.RaiseLocalEvent(katana, ev);

        if (applyDamage && ev.BonusDamage.AnyPositive())
        {
            foreach (var victim in targets)
            {
                if (entMan.HasComponent<DamageableComponent>(victim))
                    entMan.System<DamageableSystem>().TryChangeDamage(victim, ev.BonusDamage, origin: user);
            }
        }
    }
}
