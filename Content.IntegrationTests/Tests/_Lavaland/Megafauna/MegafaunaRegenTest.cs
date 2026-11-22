// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Lavaland.Aggression;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

/// <summary>
/// A boss left alone must slowly regenerate, so running away between hits cannot win a fight.
/// </summary>
[TestFixture]
public sealed class MegafaunaRegenTest
{
    [Test]
    public async Task EscapedBossSlowlyHeals()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var damageable = entMan.System<DamageableSystem>();
        var aggressors = entMan.System<AggressorsSystem>();
        var testMap = await pair.CreateTestMap();

        EntityUid boss = default;
        EntityUid dummy = default;
        var damaged = 0f;

        await server.WaitPost(() =>
        {
            boss = entMan.SpawnEntity("LavalandBossAshDrake", testMap.GridCoords);
            dummy = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(5f, 0f)));
            // The boss must keep the aggressor for the whole check, so the dummy cannot be killed.
            entMan.EnsureComponent<GodmodeComponent>(dummy);

            damageable.TryChangeDamage(boss, new DamageSpecifier { DamageDict = { { "Blunt", 500f } } }, true);
            var aggressive = entMan.GetComponent<AggressiveComponent>(boss);
            aggressors.RemoveAllAggressors((boss, aggressive));
            aggressors.AddAggressor((boss, aggressive), dummy);
            damaged = entMan.GetComponent<DamageableComponent>(boss).TotalDamage.Float();
        });

        Assert.That(damaged, Is.GreaterThan(0f), "the boss must take damage before the check");

        // While the boss still has an aggressor it must not heal.
        await server.WaitRunTicks(60);
        await server.WaitAssertion(() =>
        {
            var total = entMan.GetComponent<DamageableComponent>(boss).TotalDamage.Float();
            Assert.That(total, Is.EqualTo(damaged).Within(0.01f), "a boss with an aggressor must not heal");
        });

        // Once the target is gone it starts healing slowly.
        await server.WaitPost(() =>
        {
            var aggressive = entMan.GetComponent<AggressiveComponent>(boss);
            aggressors.RemoveAggressor((boss, aggressive), dummy);
        });

        await server.WaitRunTicks(300);
        await server.WaitAssertion(() =>
        {
            var total = entMan.GetComponent<DamageableComponent>(boss).TotalDamage.Float();
            Assert.That(total, Is.LessThan(damaged - 20f), "an escaped boss must slowly heal");
        });

        await pair.CleanReturnAsync();
    }
}
