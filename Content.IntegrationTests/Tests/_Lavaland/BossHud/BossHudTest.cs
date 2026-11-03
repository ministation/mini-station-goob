// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Lavaland.BossHud;
using Content.Server._Lavaland.Mobs;
using Content.Shared._Lavaland.Aggression;
using Content.Shared._Lavaland.BossHud;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Lavaland.BossHud;

[TestFixture]
[TestOf(typeof(BossHudSystem))]
public sealed class BossHudTest
{
    [Test]
    public async Task BossHudTracksFightState()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await pair.CreateTestMap();

        EntityUid boss = default;
        EntityUid attacker = default;

        await server.WaitPost(() =>
        {
            boss = entMan.SpawnEntity("LavalandBossAshDrake", testMap.GridCoords);
            // Any entity works as an aggressor; an item cannot die or wander off mid test.
            attacker = entMan.SpawnEntity("Wrench", testMap.GridCoords);
            entMan.EnsureComponent<BossHudComponent>(boss);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var hud = entMan.GetComponent<BossHudComponent>(boss);
            Assert.Multiple(() =>
            {
                Assert.That(hud.Active, Is.False, "boss HUD must stay hidden without aggressors");
                Assert.That(hud.MaxHealth, Is.GreaterThan(0f), "boss HUD must know the boss max health");
                Assert.That(hud.Health, Is.EqualTo(hud.MaxHealth), "undamaged boss must start at full health");
            });
        });

        await server.WaitPost(() =>
        {
            var aggressive = entMan.EnsureComponent<AggressiveComponent>(boss);
            entMan.System<AggressorsSystem>().AddAggressor((boss, aggressive), attacker);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var hud = entMan.GetComponent<BossHudComponent>(boss);
            Assert.That(hud.Active, Is.True, "boss HUD must show while aggressors are present");
        });

        await server.WaitPost(() =>
        {
            entMan.System<DamageableSystem>().TryChangeDamage(
                boss,
                new DamageSpecifier { DamageDict = { { "Blunt", 500f } } },
                true);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var hud = entMan.GetComponent<BossHudComponent>(boss);
            Assert.Multiple(() =>
            {
                Assert.That(hud.Health, Is.LessThan(hud.MaxHealth), "boss health must drop after damage");
                Assert.That(hud.Active, Is.True, "boss HUD must stay visible during the fight");
            });
        });

        await server.WaitPost(() =>
        {
            // Keep the corpse around so the hidden state after death can be asserted.
            entMan.RemoveComponent<SpawnLootOnDeathComponent>(boss);
            entMan.System<DamageableSystem>().TryChangeDamage(
                boss,
                new DamageSpecifier { DamageDict = { { "Blunt", 10000f } } },
                true);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            // Bosses with SpawnLootOnDeath are queued for deletion on the death tick,
            // so the corpse may already be gone by the time the assertion runs.
            if (!entMan.Deleted(boss))
            {
                Assert.That(entMan.GetComponent<MobStateComponent>(boss).CurrentState, Is.EqualTo(MobState.Dead),
                    "boss should be dead");
            }

            if (entMan.TryGetComponent<BossHudComponent>(boss, out var hud))
                Assert.That(hud.Active, Is.False, "boss HUD must hide after death");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BloodDrunkMinerGetsBossHud()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await pair.CreateTestMap();

        EntityUid boss = default;
        await server.WaitPost(() => boss = entMan.SpawnEntity("LavalandBossBloodDrunk", testMap.GridCoords));

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.TryGetComponent<MegafaunaStageComponent>(boss, out var stage), Is.True,
                "the blood drunk miner must declare a stage for the boss HUD");
            if (stage != null)
            {
                // The second stage is the low health enrage at 35 percent HP.
                Assert.That(stage.MaxStage, Is.EqualTo(2), "the enrage must be a second HUD stage");
                Assert.That(stage.Stage, Is.EqualTo(1), "the miner starts in the calm stage");
            }

            Assert.That(entMan.HasComponent<BossHudComponent>(boss), Is.True,
                "the boss HUD system must attach to the blood drunk miner");
        });

        await pair.CleanReturnAsync();
    }
}
