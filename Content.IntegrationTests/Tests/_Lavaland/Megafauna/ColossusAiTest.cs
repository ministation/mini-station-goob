// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Lavaland.Aggression;
using Content.Shared._Lavaland.Megafauna;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Selectors;
using Content.Shared._Lavaland.Megafauna.Systems;
using Content.Shared.Actions.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

[TestFixture]
[TestOf(typeof(MegafaunaAiComponent))]
public sealed class ColossusAiTest
{
    [Test]
    public async Task ColossusAggrosThroughArmor()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid colossus = default;
        EntityUid attacker = default;

        await server.WaitPost(() =>
        {
            colossus = entMan.SpawnAtPosition("LavalandBossColossus", testMap.GridCoords);
            attacker = entMan.SpawnAtPosition("Wrench", testMap.GridCoords);
            entMan.EnsureComponent<ActorComponent>(attacker);
        });

        await server.WaitRunTicks(2);

        await server.WaitPost(() =>
        {
            // The armor pool eats the whole hit, so no DamageChangedEvent is raised. The boss still
            // has to notice the attacker, otherwise it stands inert until the armor is gone.
            entMan.System<DamageableSystem>().TryChangeDamage(
                colossus,
                new DamageSpecifier { DamageDict = { { "Piercing", 50f } } },
                origin: attacker);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var armor = entMan.GetComponent<MegafaunaArmorComponent>(colossus);
            Assert.That(armor.Armor, Is.LessThan(armor.MaxArmor), "the hit should have drained armor");

            var damageable = entMan.GetComponent<DamageableComponent>(colossus);
            Assert.That(damageable.TotalDamage, Is.EqualTo(FixedPoint2.Zero),
                "the armor has to absorb all health damage, otherwise this test proves nothing");

            var aggressive = entMan.GetComponent<AggressiveComponent>(colossus);
            Assert.That(aggressive.Aggressors, Does.Contain(attacker),
                "a hit absorbed by armor must still add the attacker as an aggressor");

            var ai = entMan.GetComponent<MegafaunaAiComponent>(colossus);
            Assert.Multiple(() =>
            {
                Assert.That(ai.Active, Is.True, "absorbed damage must still start the boss AI");
                Assert.That(ai.Schedule, Is.Not.Empty, "the fight must be scheduled");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ColossusNonBoltAttacksAreWired()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        EntityUid colossus = default;

        await server.WaitPost(() => colossus = entMan.SpawnAtPosition("LavalandBossColossus", testMap.GridCoords));

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var granted = entMan.GetComponent<ActionsComponent>(colossus).Actions
                .Where(action => !entMan.Deleted(action))
                .Select(action => entMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID)
                .ToArray();

            var group = (GroupMegafaunaSelector) protoMan.Index<MegafaunaSelectorPrototype>("ColossusActions").Selector;
            var attacks = group.Children.OfType<ProtoIdMegafaunaSelector>().Select(child => child.Id.Id).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(granted, Does.Contain("ActionColossusBoltRain"), "colossus must be granted the bolt rain");
                Assert.That(granted, Does.Contain("ActionColossusBoulderToss"), "colossus must be granted the boulder toss");
                Assert.That(attacks, Does.Contain("ActionColossusBoltRain"), "bolt rain must be in the attack selector");
                Assert.That(attacks, Does.Contain("ActionColossusBoulderToss"), "boulder toss must be in the attack selector");
                Assert.That(protoMan.HasIndex<EntityPrototype>("LavalandColossusBoulder"), Is.True,
                    "the boulder projectile prototype must exist");
            });
        });

        await pair.CleanReturnAsync();
    }
}
