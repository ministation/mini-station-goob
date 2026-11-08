// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable annotations

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Mobs;

/// <summary>
/// End-to-end check of the tendril regenerator: the tendril must heal its brute damage back,
/// but only after it has gone thirty seconds without damage.
/// </summary>
[TestFixture]
public sealed class TendrilRegenerationTest
{
    private const int EarlyTicks = 150; // 5 s, the tendril must not have healed yet
    private const int DelayTicks = 930; // 31 s, the regeneration delay
    private const int HealTicks = 700; // more than the time needed to heal the damage

    [Test]
    public async Task TendrilHealsBruteDamageAfterDelay()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var proto = server.ResolveDependency<IPrototypeManager>();
        var damage = server.System<DamageableSystem>();

        var testMap = await pair.CreateTestMap();
        EntityUid tendril = default;
        var brute = new DamageSpecifier(proto.Index<DamageTypePrototype>("Blunt"), 100);

        await server.WaitPost(() =>
        {
            tendril = entMan.SpawnEntity("LavalandTendrilGoliath", testMap.GridCoords);
            damage.TryChangeDamage(tendril, brute, ignoreResistances: true);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<DamageableComponent>(tendril).TotalDamage,
                Is.EqualTo(FixedPoint2.New(100)), "the test damage did not land on the tendril");
        });

        // Within the regeneration delay nothing may heal.
        await server.WaitRunTicks(EarlyTicks);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<DamageableComponent>(tendril).TotalDamage,
                Is.EqualTo(FixedPoint2.New(100)), "the tendril healed before the regeneration delay");
        });

        await server.WaitRunTicks(DelayTicks + HealTicks);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<DamageableComponent>(tendril).TotalDamage,
                Is.EqualTo(FixedPoint2.Zero), "the tendril did not regenerate its damage back");
        });

        await pair.CleanReturnAsync();
    }
}
