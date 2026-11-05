// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable annotations

using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Lavaland.Aggression;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

/// <summary>
/// End-to-end damage check for every lavaland boss: the boss must actually hurt an adjacent target.
/// This catches attacks that are wired but silently do nothing (bad target, dropped event, zero damage),
/// which a prototype-only audit cannot see.
/// </summary>
[TestFixture]
public sealed class BossAttackDamageTest
{
    private const int Ticks = 600;
    private const int DummyHealth = 100000;

    private static readonly string[] Bosses =
    {
        "LavalandBossBubblegum",
        "LavalandBossColossus",
        "LavalandBossAshDrake",
        "MobHierophant",
        "LavalandBossWendigo",
        "LavalandBossDemonicFrostMiner",
        "LavalandBossThing",
        "LavalandBossBloodDrunk",
        "LavalandBossMegaLegion",
    };

    [Test]
    public async Task EveryBossDamagesAnAdjacentDummy()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var thresholdsSys = server.System<MobThresholdSystem>();

        foreach (var bossProto in Bosses)
        {
            var testMap = await pair.CreateTestMap();
            EntityUid boss = default;
            EntityUid dummy = default;

            await server.WaitPost(() =>
            {
                // The default test map is a single tile; give the boss room to move and attack.
                var tile = testMap.Tile.Tile;
                for (var x = -12; x <= 12; x++)
                {
                    for (var y = -12; y <= 12; y++)
                        mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
                }

                boss = entMan.SpawnEntity(bossProto, testMap.GridCoords);
                dummy = entMan.SpawnEntity("MobCow", testMap.GridCoords.Offset(new Vector2(1.5f, 0f)));

                // Keep the dummy alive for the whole fight: a dead dummy would prove nothing.
                // MobThresholdsComponent is write-protected outside its own system, so go through the system API.
                var thresholds = entMan.GetComponent<MobThresholdsComponent>(dummy);
                thresholdsSys.SetMobStateThreshold(dummy, FixedPoint2.Zero, MobState.Alive, thresholds);
                thresholdsSys.SetMobStateThreshold(dummy, FixedPoint2.New(DummyHealth), MobState.Dead, thresholds);

                var aggressive = entMan.GetComponent<AggressiveComponent>(boss);
                entMan.System<AggressorsSystem>().AddAggressor((boss, aggressive), dummy);
            });

            await server.WaitRunTicks(Ticks);

            await server.WaitAssertion(() =>
            {
                Assert.That(entMan.TryGetComponent<DamageableComponent>(dummy, out var damageable), Is.True,
                    bossProto + " removed the dummy's DamageableComponent");

                Assert.That(entMan.GetComponent<MobStateComponent>(dummy).CurrentState, Is.Not.EqualTo(MobState.Dead),
                    bossProto + " killed the test dummy, the health boost did not apply");

                Assert.That(damageable!.TotalDamage.Float(), Is.GreaterThan(0f),
                    bossProto + " did not damage an adjacent dummy in " + Ticks + " ticks, its attacks are not landing");

                TestContext.Out.WriteLine("RUNTIME " + bossProto + " -> " + damageable.TotalDamage.Float() + " damage in " + Ticks + " ticks");
            });

            await server.WaitPost(() =>
            {
                entMan.DeleteEntity(boss);
                entMan.DeleteEntity(dummy);
            });
        }

        await pair.CleanReturnAsync();
    }
}
