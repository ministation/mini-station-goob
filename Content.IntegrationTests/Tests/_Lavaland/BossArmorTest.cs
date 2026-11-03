// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Lavaland.BossHud;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Lavaland;

/// <summary>
/// Guards the megafauna armor pool: damage goes into armor before health and a broken
/// pool never regenerates. Also keeps the boss HUD snapshot in sync with the pool.
/// </summary>
[TestFixture]
public sealed class BossArmorTest
{
    [Test]
    public async Task ArmorAbsorbsDamageAndBreaks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await pair.CreateTestMap();

        EntityUid boss = default;
        await server.WaitPost(() =>
        {
            boss = entMan.SpawnEntity("LavalandBossColossus", testMap.GridCoords);
            // Godmode cancels damage without a player origin, remove it so the test can hit the boss.
            entMan.RemoveComponent<MegafaunaGodmodeComponent>(boss);
            // Raw numbers keep the armor math checkable.
            entMan.System<DamageableSystem>().SetDamageModifierSetId(boss, null);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var armor = entMan.GetComponent<MegafaunaArmorComponent>(boss);
            var hud = entMan.GetComponent<BossHudComponent>(boss);
            Assert.Multiple(() =>
            {
                Assert.That(armor.MaxArmor, Is.EqualTo(600f), "colossus prototype declares the armor pool");
                Assert.That(armor.Armor, Is.EqualTo(armor.MaxArmor), "armor starts full");
                Assert.That(armor.Broken, Is.False, "armor starts intact");
                Assert.That(hud.MaxArmor, Is.EqualTo(600f), "boss HUD must know the armor pool");
                Assert.That(hud.Armor, Is.EqualTo(600f), "boss HUD must start with full armor");
            });
        });

        await server.WaitPost(() =>
        {
            var armor = entMan.GetComponent<MegafaunaArmorComponent>(boss);
            // Shorter timings keep the test quick, the prototype uses eight seconds and forty per second.
            armor.RegenDelay = 0.5f;
            armor.RegenRate = 1000f;
        });

        await server.WaitPost(() =>
        {
            entMan.System<DamageableSystem>().TryChangeDamage(
                boss,
                new DamageSpecifier { DamageDict = { { "Blunt", 200f } } });
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var armor = entMan.GetComponent<MegafaunaArmorComponent>(boss);
            var hud = entMan.GetComponent<BossHudComponent>(boss);
            var damageable = entMan.GetComponent<DamageableComponent>(boss);
            Assert.Multiple(() =>
            {
                Assert.That(armor.Armor, Is.EqualTo(400f).Within(0.5f), "armor must absorb the hit");
                Assert.That(damageable.TotalDamage.Float(), Is.EqualTo(0f), "absorbed damage must not reach health");
                Assert.That(hud.Armor, Is.EqualTo(armor.Armor).Within(0.5f), "boss HUD armor must follow the pool");
                Assert.That(hud.Health, Is.EqualTo(hud.MaxHealth), "health stays full while armor absorbs");
            });
        });

        // Past the regen delay the pool refills.
        await server.WaitRunTicks(40);

        await server.WaitAssertion(() =>
        {
            var armor = entMan.GetComponent<MegafaunaArmorComponent>(boss);
            Assert.That(armor.Armor, Is.EqualTo(600f).Within(0.5f), "armor must regenerate back to max");
        });

        // Damage past the remaining pool breaks it and the leftover reaches health.
        await server.WaitPost(() =>
        {
            entMan.System<DamageableSystem>().TryChangeDamage(
                boss,
                new DamageSpecifier { DamageDict = { { "Blunt", 1000f } } });
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var armor = entMan.GetComponent<MegafaunaArmorComponent>(boss);
            var hud = entMan.GetComponent<BossHudComponent>(boss);
            var damageable = entMan.GetComponent<DamageableComponent>(boss);
            Assert.Multiple(() =>
            {
                Assert.That(armor.Armor, Is.EqualTo(0f), "a hit past the pool zeroes it out");
                Assert.That(armor.Broken, Is.True, "a depleted pool must break");
                Assert.That(damageable.TotalDamage.Float(), Is.EqualTo(400f).Within(0.5f),
                    "only the damage past the armor must reach health");
                Assert.That(hud.Armor, Is.EqualTo(0f), "boss HUD must show the depleted pool");
                Assert.That(hud.ArmorBroken, Is.True, "boss HUD must show the broken pool");
                Assert.That(hud.Health, Is.EqualTo(hud.MaxHealth - 400f).Within(1f), "boss HUD health must drop");
            });
        });

        // A broken pool never comes back.
        await server.WaitRunTicks(40);

        await server.WaitAssertion(() =>
        {
            var armor = entMan.GetComponent<MegafaunaArmorComponent>(boss);
            var hud = entMan.GetComponent<BossHudComponent>(boss);
            Assert.Multiple(() =>
            {
                Assert.That(armor.Armor, Is.EqualTo(0f), "broken armor must not regenerate");
                Assert.That(armor.Broken, Is.True, "broken armor must stay broken");
                Assert.That(hud.Armor, Is.EqualTo(0f), "boss HUD must keep the broken armor at zero");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BossWithoutArmorKeepsHud()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await pair.CreateTestMap();

        EntityUid boss = default;
        await server.WaitPost(() => boss = entMan.SpawnEntity("LavalandBossAshDrake", testMap.GridCoords));

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<MegafaunaArmorComponent>(boss), Is.False,
                "the ash drake has no armor pool");
            var hud = entMan.GetComponent<BossHudComponent>(boss);
            Assert.Multiple(() =>
            {
                Assert.That(hud.MaxArmor, Is.EqualTo(0f), "a boss without armor must report no armor");
                Assert.That(hud.Armor, Is.EqualTo(0f), "a boss without armor must report empty armor");
                Assert.That(hud.MaxHealth, Is.GreaterThan(0f), "the health bar must still work");
            });
        });

        await pair.CleanReturnAsync();
    }
}
