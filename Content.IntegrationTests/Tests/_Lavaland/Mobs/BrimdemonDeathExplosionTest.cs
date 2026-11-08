// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

#nullable enable annotations

using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Mobs;

/// <summary>
/// End-to-end check of the brimdemon death burst: dying must hurt a mob standing next to it.
/// </summary>
[TestFixture]
public sealed class BrimdemonDeathExplosionTest
{
    [Test]
    public async Task DyingBrimdemonBurnsNeighbour()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var proto = server.ResolveDependency<IPrototypeManager>();
        var damage = server.System<DamageableSystem>();

        var testMap = await pair.CreateTestMap();
        EntityUid brimdemon = default;
        EntityUid victim = default;

        await server.WaitPost(() =>
        {
            brimdemon = entMan.SpawnEntity("LavalandMobBrimdemon", testMap.GridCoords);
            victim = entMan.SpawnEntity("MobCow", testMap.GridCoords.Offset(new Vector2(0.8f, 0f)));

            // Kill it instantly so its ranged AI does not get a turn and the burst is the only damage.
            var lethal = new DamageSpecifier(proto.Index<DamageTypePrototype>("Blunt"), 400);
            damage.TryChangeDamage(brimdemon, lethal, ignoreResistances: true);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.TryGetComponent<DamageableComponent>(victim, out var damageable), Is.True,
                "the brimdemon burst victim has no DamageableComponent");
            Assert.That(damageable!.Damage.DamageDict.TryGetValue("Heat", out var heat), Is.True,
                "the dying brimdemon did not apply heat damage to its neighbour");
            Assert.That(heat, Is.GreaterThan(FixedPoint2.Zero),
                "the dying brimdemon applied no heat damage to its neighbour");
        });

        await pair.CleanReturnAsync();
    }
}
