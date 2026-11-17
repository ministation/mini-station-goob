// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Collections.Generic;
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
using Content.Shared.Chat;
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

        // TG Titan's Finale: 26 scripted steps, the first wave comes 1.1 s after the action starts.
        var before = new HashSet<EntityUid>();
        await server.WaitAssertion(() => before = SnapshotProjectiles(entMan));

        await server.WaitPost(() => finale.Invoke(args));

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var volley = entMan.GetComponent<MegafaunaVolleyComponent>(colossus);
            Assert.Multiple(() =>
            {
                Assert.That(volley.Steps.Count, Is.EqualTo(26), "the finale must play TG's 20 + 3 + 3 waves");
                Assert.That(volley.Steps[0].Interval, Is.EqualTo(1.1f).Within(0.001f),
                    "the first wave waits 1.1 s, like TG's opening sleep");
                Assert.That(volley.Steps[5].Interval, Is.EqualTo(0.6f).Within(0.001f),
                    "the shotgun waves speed up from 1.1 s to 0.6 s");
                Assert.That(volley.Steps[8].Interval, Is.EqualTo(0.3f).Within(0.001f),
                    "the counter runs down to 0.3 s");
                Assert.That(volley.Steps[9].Interval, Is.EqualTo(0.2f).Within(0.001f),
                    "the slow main loop waves are 0.2 s apart");
                Assert.That(volley.Steps.Take(6).Select(step => step.Pattern.Count), Is.All.EqualTo(6),
                    "the first six waves are the six-bolt shotgun blast");
                Assert.That(volley.Steps.Skip(6).Take(3).Select(step => step.ScatterCount), Is.All.EqualTo(12),
                    "the next three waves only scatter bolts over the arena");
                Assert.That(volley.Steps.Skip(9).Take(11).Select(step => step.ScatterCount), Is.All.EqualTo(3),
                    "the rest of the main loop is sparse scatter");
                Assert.That(volley.Steps.Skip(20).Take(3).Select(step => step.RandomCount), Is.All.EqualTo(32),
                    "random_shots is three waves of 32 bolts");
                Assert.That(volley.Steps.Skip(23).Select(step => step.Pattern.Count), Is.All.EqualTo(8),
                    "dir_shots closes the barrage with three eight-way waves");
                Assert.That(CountProjectiles(entMan), Is.EqualTo(16),
                    "the first wave must wait for its interval instead of firing instantly");
            });
        });

        await server.WaitRunTicks(34);

        // The first wave is six aimed shotgun bolts plus twelve bolts aimed at random tiles.
        await server.WaitAssertion(() =>
        {
            var fresh = SnapshotProjectiles(entMan).Except(before).ToArray();
            var angles = fresh.Select(uid => Math.Round(entMan.GetComponent<TransformComponent>(uid).LocalRotation.Degrees, 2))
                .Distinct()
                .Count();
            Assert.Multiple(() =>
            {
                Assert.That(fresh.Length, Is.EqualTo(18), "the first wave is six shotgun bolts plus twelve scatter bolts");
                Assert.That(angles, Is.GreaterThan(6),
                    "scatter bolts fly at random tiles, not only along the six shotgun angles");
                Assert.That(entMan.GetComponent<MegafaunaVolleyComponent>(colossus).StepIndex, Is.EqualTo(1),
                    "exactly one wave must have fired");
            });
        });

        // Another volley action picked in the middle of the barrage must not cut the finale short.
        var stepIndex = 0;
        await server.WaitAssertion(() =>
        {
            stepIndex = entMan.GetComponent<MegafaunaVolleyComponent>(colossus).StepIndex;
            Assert.That(stepIndex, Is.EqualTo(1), "the finale has fired exactly one wave so far");
        });

        await server.WaitPost(() => dirShots.Invoke(args));

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var volley = entMan.GetComponent<MegafaunaVolleyComponent>(colossus);
            Assert.Multiple(() =>
            {
                Assert.That(volley.Steps.Count, Is.EqualTo(26), "a second volley action must not replace the finale");
                Assert.That(volley.StepIndex, Is.EqualTo(stepIndex),
                    "the finale step counter must keep counting instead of restarting");
                Assert.That(CountProjectiles(entMan), Is.EqualTo(34),
                    "the alternating volley must not fire over the finale");
            });
        });

        await server.WaitRunTicks(30);

        await server.WaitAssertion(() =>
        {
            Assert.That(CountProjectiles(entMan), Is.EqualTo(52), "a second shotgun wave must fire after 1 s");
            Assert.That(finale.CheckConditions(args), Is.False, "the finale is once per fight");
        });

        await pair.CleanReturnAsync();

        static int CountProjectiles(IEntityManager entMan)
        {
            var count = 0;
            var query = entMan.EntityQueryEnumerator<ProjectileComponent>();
            while (query.MoveNext(out _, out _))
                count++;

            return count;
        }

        static HashSet<EntityUid> SnapshotProjectiles(IEntityManager entMan)
        {
            var set = new HashSet<EntityUid>();
            var query = entMan.EntityQueryEnumerator<ProjectileComponent>();
            while (query.MoveNext(out var uid, out _))
                set.Add(uid);

            return set;
        }
    }

    [Test]
    public async Task ColossusFinalePausesAiAndAnnounces()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var damageable = entMan.System<DamageableSystem>();

        SpeechSpySystem spy = default!;
        Assert.That(server.ResolveDependency<IEntitySystemManager>().TryGetEntitySystem(out spy));

        EntityUid colossus = default;
        EntityUid attacker = default;

        await server.WaitPost(() =>
        {
            colossus = entMan.SpawnAtPosition("LavalandBossColossus", testMap.GridCoords);
            attacker = entMan.SpawnAtPosition("Wrench", testMap.GridCoords);
            entMan.EnsureComponent<ActorComponent>(attacker);
        });

        await server.WaitRunTicks(2);

        // Start the AI for real and bring the boss into phase 3, keeping the aggressor alive.
        await server.WaitPost(() => damageable.TryChangeDamage(colossus,
            new DamageSpecifier { DamageDict = { { "Blunt", 3000f } } }, true, origin: attacker));

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            var ai = entMan.GetComponent<MegafaunaAiComponent>(colossus);
            Assert.That(ai.Active, Is.True, "the angry colossus must have its AI running");
            Assert.That(ai.Schedule, Is.Not.Empty, "the AI must have attacks scheduled");
            Assert.That(entMan.GetComponent<MobPhasesComponent>(colossus).CurrentPhase, Is.EqualTo(3));
        });

        var args = new MegafaunaCalculationBaseArgs(
            colossus,
            entMan,
            protoMan,
            server.ResolveDependency<ILogManager>().GetSawmill("colossus-finale-test"),
            server.ResolveDependency<IRobustRandom>());
        var finale = protoMan.Index<MegafaunaSelectorPrototype>(FinaleAction).Selector;

        spy.LastMessage = null;

        await server.WaitPost(() => finale.Invoke(args));

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var ai = entMan.GetComponent<MegafaunaAiComponent>(colossus);
            Assert.Multiple(() =>
            {
                Assert.That(ai.Active, Is.False, "the finale must freeze the colossus AI while the barrage plays");
                Assert.That(entMan.GetComponent<MegafaunaVolleyComponent>(colossus).Steps.Count, Is.EqualTo(26),
                    "the finale barrage must have started");
                Assert.That(spy.LastMessage, Is.EqualTo("Perish."), "the finale must announce Perish. in chat");
            });
        });

        // The barrage ends early here, but the AI must come back as soon as the barrage is gone.
        await server.WaitPost(() => entMan.RemoveComponent<MegafaunaVolleyComponent>(colossus));

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
            Assert.That(entMan.GetComponent<MegafaunaAiComponent>(colossus).Active, Is.True,
                "the colossus AI must resume after the barrage"));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Captures entity speech so the test can check the finale announce line.
    /// </summary>
    public sealed partial class SpeechSpySystem : EntitySystem
    {
        public string? LastMessage;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<EntitySpokeEvent>((ev) => LastMessage = ev.Message);
        }
    }
}
