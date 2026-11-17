// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Lavaland.Aggression;
using Content.Shared._Lavaland.Anger.Components;
using Content.Shared._Lavaland.Megafauna;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Conditions;
using Content.Shared._Lavaland.Megafauna.Events;
using Content.Shared._Lavaland.Megafauna.Selectors;
using Content.Shared._Lavaland.Megafauna.Systems;
using Content.Shared._Lavaland.MobPhases;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

[TestFixture]
[TestOf(typeof(MegafaunaAiComponent))]
public sealed class ColossusAiTest
{
    private const string FinaleAction = "ActionColossusFinale";
    private const string DirShotsAction = "ActionColossusBoltsDirShots";

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

    [Test]
    public async Task ColossusHealthTableIsConsistent()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid colossus = default;

        await server.WaitPost(() => colossus = entMan.SpawnAtPosition("LavalandBossColossus", testMap.GridCoords));

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            // Anger overwrites the death threshold from totalHp, so the two must not drift apart.
            var anger = entMan.GetComponent<AngerComponent>(colossus);
            var thresholds = entMan.GetComponent<MobThresholdsComponent>(colossus).Thresholds;
            var dead = thresholds.Last(entry => entry.Value == MobState.Dead).Key;
            Assert.That(anger.TotalHp, Is.EqualTo(dead), "Anger totalHp has to match the death threshold");

            // Phase gates are damage numbers: below 33% and below 10% of the health pool.
            var phases = entMan.GetComponent<MobPhasesComponent>(colossus);
            var gates = phases.PhaseThresholds.Keys.OrderBy(key => key.Float()).Select(key => key.Float()).ToArray();
            Assert.That(gates.Length, Is.EqualTo(3), "the colossus has a start gate and two attack phases");
            Assert.That(gates[1], Is.EqualTo(dead.Float() * 0.66f).Within(1f),
                "the counter-spiral phase must stay at 33% HP");
            Assert.That(gates[2], Is.EqualTo(dead.Float() * 0.9f).Within(1f),
                "the finale phase must stay at 10% HP");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ColossusFinaleBarrageIsWired()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var damageable = entMan.System<DamageableSystem>();

        EntityUid colossus = default;
        EntityUid attacker = default;

        await server.WaitPost(() => colossus = entMan.SpawnAtPosition("LavalandBossColossus", testMap.GridCoords));

        await server.WaitRunTicks(2);

        var args = new MegafaunaCalculationBaseArgs(
            colossus,
            entMan,
            protoMan,
            server.ResolveDependency<ILogManager>().GetSawmill("colossus-finale-test"),
            server.ResolveDependency<IRobustRandom>());

        var finale = protoMan.Index<MegafaunaSelectorPrototype>(FinaleAction).Selector;
        var dirShots = protoMan.Index<MegafaunaSelectorPrototype>(DirShotsAction).Selector;

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
                Assert.That(granted, Does.Contain(FinaleAction), "the colossus must be granted the finale");
                Assert.That(granted, Does.Contain(DirShotsAction), "the colossus must be granted the alternating volley");
                Assert.That(attacks, Does.Contain(DirShotsAction), "the alternating volley must be in the attack selector");
                Assert.That(finale.Conditions.OfType<PhaseMegafaunaCondition>().Single().RequiredPhases,
                    Is.EqualTo(new[] { 3 }), "the finale must stay a phase 3 finisher");
                Assert.That(dirShots.Conditions.OfType<PhaseMegafaunaCondition>(), Is.Empty,
                    "the alternating volley is a basic attack, TG has no phase gate on it");
                Assert.That(finale.CheckConditions(args), Is.False, "the finale must stay locked in phase 1");
                Assert.That(dirShots.CheckConditions(args), Is.True,
                    "the alternating volley must be available from the start of the fight");
            });
        });

        // TG dir_shots/alternating: four waves of four bolts, diagonals then cardinals.
        await server.WaitPost(() => dirShots.Invoke(args));

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
            Assert.That(CountProjectiles(entMan), Is.EqualTo(4),
                "the first alternating wave must fire four bolts along the diagonals"));

        await server.WaitRunTicks(100);

        await server.WaitAssertion(() =>
            Assert.That(CountProjectiles(entMan), Is.EqualTo(16),
                "four alternating waves must fire four bolts each, diagonals and cardinals"));

        // The colossus is invulnerable to damage without an actor origin, so a dummy hits it once.
        // Resistances are off, so all 3000 lands: past the 2835 phase 3 gate, below the 3150 death one.
        await server.WaitPost(() =>
        {
            attacker = entMan.SpawnAtPosition("Wrench", testMap.GridCoords);
            entMan.EnsureComponent<ActorComponent>(attacker);
            damageable.TryChangeDamage(colossus,
                new DamageSpecifier { DamageDict = { { "Blunt", 3000f } } }, true, origin: attacker);
        });

        await server.WaitRunTicks(2);

        // The dummy is gone, so the AI stops scheduling its own attacks and the bolt counts stay exact.
        await server.WaitPost(() => entMan.DeleteEntity(attacker));

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var phases = entMan.GetComponent<MobPhasesComponent>(colossus);
            Assert.That(phases.CurrentPhase, Is.EqualTo(3), "the hit must bring the colossus into the finale phase");
            Assert.That(finale.CheckConditions(args), Is.True, "the finale must unlock in phase 3");
        });

        // TG colossus_final: 24 waves 1.5 s apart, the first six are shotgun blasts of six bolts.
        await server.WaitPost(() => finale.Invoke(args));

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var volley = entMan.GetComponent<MegafaunaVolleyComponent>(colossus);
            Assert.Multiple(() =>
            {
                Assert.That(volley.Volleys, Is.EqualTo(24), "the finale must fire 24 waves");
                Assert.That(volley.Interval, Is.EqualTo(TimeSpan.FromSeconds(1.5)),
                    "the finale waves must be 1.5 s apart");
                Assert.That(volley.CountPerAngle, Is.EqualTo(10), "every random finale wave must fire ten bolts");
                Assert.That(volley.Patterns.Select(pattern => pattern.Count).ToArray(),
                    Is.EqualTo(new[] { 6, 6, 6, 6, 6, 6, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 32, 32, 32, 8, 8, 8 }),
                    "the finale order is shotgun waves, random waves, three rings, then three eight-way waves");
                Assert.That(CountProjectiles(entMan), Is.EqualTo(22),
                    "the first finale wave is a shotgun blast of six bolts on top of the sixteen volley bolts");
            });
        });

        // Another volley action picked in the middle of the barrage must not cut the finale short.
        var barrageIndex = 0;
        await server.WaitAssertion(() =>
        {
            var volley = entMan.GetComponent<MegafaunaVolleyComponent>(colossus);
            barrageIndex = volley.VolleyIndex;
            Assert.That(barrageIndex, Is.EqualTo(1), "the finale has fired exactly one wave so far");
        });

        await server.WaitPost(() => dirShots.Invoke(args));

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var volley = entMan.GetComponent<MegafaunaVolleyComponent>(colossus);
            Assert.Multiple(() =>
            {
                Assert.That(volley.Volleys, Is.EqualTo(24), "a second volley action must not replace the finale");
                Assert.That(volley.VolleyIndex, Is.EqualTo(barrageIndex),
                    "the finale wave counter must keep counting instead of restarting");
                Assert.That(CountProjectiles(entMan), Is.EqualTo(22),
                    "the alternating volley must not fire over the finale");
            });
        });

        await server.WaitRunTicks(48);

        await server.WaitAssertion(() =>
        {
            Assert.That(CountProjectiles(entMan), Is.EqualTo(28), "a second shotgun wave must fire after 1.5 s");
            Assert.That(finale.CheckConditions(args), Is.False, "the finale is once per fight");
        });

        await server.WaitRunTicks(45);

        await server.WaitAssertion(() =>
            Assert.That(CountProjectiles(entMan), Is.EqualTo(34), "a third shotgun wave must fire after another 1.5 s"));

        await pair.CleanReturnAsync();

        static int CountProjectiles(IEntityManager entMan)
        {
            var count = 0;
            var query = entMan.EntityQueryEnumerator<ProjectileComponent>();
            while (query.MoveNext(out _, out _))
                count++;

            return count;
        }
    }
}
