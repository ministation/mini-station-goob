// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

#nullable enable annotations

using Content.Goobstation.Maths.FixedPoint;
using Content.Server._Lavaland.Mobs;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Mobs;

/// <summary>
/// End-to-end check of the basilisk overheat: heat must heal it, swap its projectile and speed it up,
/// cold must revert all of that.
/// </summary>
[TestFixture]
public sealed class BasiliskOverheatTest
{
    [Test]
    public async Task HeatOverheatsAndColdCoolsDown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var proto = server.ResolveDependency<IPrototypeManager>();
        var damage = server.System<DamageableSystem>();

        var testMap = await pair.CreateTestMap();
        EntityUid basilisk = default;

        await server.WaitPost(() =>
        {
            basilisk = entMan.SpawnEntity("MobLavalandBasilisk", testMap.GridCoords);
        });
        await server.WaitRunTicks(2);

        await server.WaitPost(() =>
        {
            var heat = new DamageSpecifier(proto.Index<DamageTypePrototype>("Heat"), 10);
            damage.TryChangeDamage(basilisk, heat, ignoreResistances: true);
        });

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<BasiliskOverheatComponent>(basilisk).EndsAt,
                    Is.Not.EqualTo(TimeSpan.Zero), "heat damage did not overheat the basilisk");
                Assert.That(entMan.GetComponent<DamageableComponent>(basilisk).TotalDamage,
                    Is.EqualTo(FixedPoint2.Zero), "overheat did not fully heal the basilisk");
                Assert.That(entMan.GetComponent<BatteryAmmoProviderComponent>(basilisk).Prototype.Id,
                    Is.EqualTo("BasiliskOverheatBolt"), "overheat did not swap the projectile");
                Assert.That(entMan.GetComponent<MovementSpeedModifierComponent>(basilisk).BaseWalkSpeed,
                    Is.GreaterThan(1.65f), "overheat did not speed the basilisk up");
            });
        });

        await server.WaitPost(() =>
        {
            var cold = new DamageSpecifier(proto.Index<DamageTypePrototype>("Cold"), 5);
            damage.TryChangeDamage(basilisk, cold, ignoreResistances: true);
        });

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<BasiliskOverheatComponent>(basilisk).EndsAt,
                    Is.EqualTo(TimeSpan.Zero), "cold damage did not cool the basilisk down");
                Assert.That(entMan.GetComponent<BatteryAmmoProviderComponent>(basilisk).Prototype.Id,
                    Is.EqualTo("BasiliskGaze"), "cooling did not restore the projectile");
                Assert.That(entMan.GetComponent<MovementSpeedModifierComponent>(basilisk).BaseWalkSpeed,
                    Is.EqualTo(1.65f).Within(0.001f), "cooling did not restore the speed");
            });
        });

        await pair.CleanReturnAsync();
    }
}
