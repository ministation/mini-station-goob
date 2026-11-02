// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Goobstation.Shared.GPS.Components;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._Lavaland.Necropolis;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Lavaland.Mobs;

[TestFixture]
[TestOf(typeof(EliteTumorComponent))]
public sealed class EliteTumorTest
{
    private static EntityUid SpawnTumor(IEntityManager entMan, EntityCoordinates coords)
    {
        var tumor = entMan.SpawnEntity("LavalandEliteTumor", coords);
        var comp = entMan.GetComponent<EliteTumorComponent>(tumor);
        comp.SpawnDelay = TimeSpan.FromMilliseconds(100);
        comp.ArenaRadius = 2;
        return tumor;
    }

    [Test]
    public async Task ActivationSpawnsEliteAndVictoryMakesItPassive()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid tumor = default;
        await server.WaitPost(() => tumor = SpawnTumor(entMan, testMap.GridCoords));
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<DamageableComponent>(tumor), Is.False,
                "the tumor must be indestructible");
            Assert.That(entMan.GetComponent<EliteTumorComponent>(tumor).State,
                Is.EqualTo(EliteTumorState.Inactive));
        });

        EntityUid user = default;
        await server.WaitPost(() => user = entMan.SpawnEntity("MobHuman",
            entMan.GetComponent<TransformComponent>(tumor).Coordinates));
        await server.WaitRunTicks(1);

        await server.WaitPost(() =>
            entMan.EventBus.RaiseLocalEvent(tumor, new InteractHandEvent(user, tumor)));
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<EliteTumorComponent>(tumor);
            Assert.That(comp.State, Is.EqualTo(EliteTumorState.Active),
                "hand activation must start the fight");
            Assert.That(comp.Walls, Is.Not.Empty, "the arena must be sealed on activation");
        });

        // The elite spawns after the configured delay.
        await server.WaitRunTicks(20);

        EntityUid elite = default;
        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<EliteTumorComponent>(tumor);
            Assert.That(comp.SpawnedElite, Is.Not.Null, "the elite must spawn after the delay");
            elite = comp.SpawnedElite!.Value;
            Assert.That(entMan.Deleted(elite), Is.False);
            Assert.That(entMan.HasComponent<MobStateComponent>(elite), Is.True);
        });

        // Dying to the elite makes the tumor passive, the elite heals up for a rematch.
        await server.WaitPost(() =>
            entMan.System<MobStateSystem>().ChangeMobState(user, MobState.Dead));
        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<EliteTumorComponent>(tumor);
            Assert.That(comp.State, Is.EqualTo(EliteTumorState.Passive),
                "dying to the elite must turn the tumor passive");
            Assert.That(comp.Walls, Is.Empty, "the arena must open when the fight is lost");
            Assert.That(entMan.Deleted(elite), Is.False, "the elite stays for a rematch");
            Assert.That(entMan.GetComponent<DamageableComponent>(elite).Damage.GetTotal(),
                Is.EqualTo(FixedPoint2.Zero), "the elite must be healed after its victory");
            Assert.That(comp.TimesWon, Is.EqualTo(0),
                "an unboosted fight must not count as a boosted win");

            Assert.That(entMan.System<MobThresholdSystem>()
                .TryGetDeadThreshold(elite, out var threshold), Is.True);
            Assert.That(threshold!.Value, Is.EqualTo(FixedPoint2.New(1000)),
                "only a boosted victory weakens the elite for the rematch");
        });

        // Rematch, this time the elite dies and the tumor pops with a loot crate.
        await server.WaitPost(() =>
            entMan.EventBus.RaiseLocalEvent(tumor, new InteractHandEvent(user, tumor)));
        await server.WaitRunTicks(5);

        await server.WaitPost(() =>
            entMan.System<DamageableSystem>().TryChangeDamage(elite,
                new DamageSpecifier { DamageDict = { { "Blunt", 2000f } } }, true));
        await server.WaitRunTicks(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(tumor), Is.True, "the tumor must pop when the elite dies");
            Assert.That(CountPrototypes(entMan, "LavalandCrateNecropolis"), Is.EqualTo(1),
                "killing the elite must leave a loot crate");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BoostedTumorGivesGhostRoleAndLoot()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid tumor = default;
        await server.WaitPost(() => tumor = SpawnTumor(entMan, testMap.GridCoords));
        await server.WaitRunTicks(2);

        EntityUid user = default;
        EntityUid boost = default;
        await server.WaitPost(() =>
        {
            user = entMan.SpawnEntity("MobHuman",
                entMan.GetComponent<TransformComponent>(tumor).Coordinates);
            boost = entMan.SpawnEntity("MaterialLegionSkull1",
                entMan.GetComponent<TransformComponent>(tumor).Coordinates);
        });
        await server.WaitRunTicks(1);

        await server.WaitPost(() =>
            entMan.EventBus.RaiseLocalEvent(tumor,
                new InteractUsingEvent(user, boost, tumor,
                    entMan.GetComponent<TransformComponent>(tumor).Coordinates)));
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
            Assert.That(entMan.GetComponent<EliteTumorComponent>(tumor).Boosted, Is.True,
                "using a legion skull on the tumor must boost it"));

        await server.WaitPost(() =>
            entMan.EventBus.RaiseLocalEvent(tumor, new InteractHandEvent(user, tumor)));
        await server.WaitRunTicks(20);

        EntityUid elite = default;
        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<EliteTumorComponent>(tumor);
            Assert.That(comp.SpawnedElite, Is.Not.Null);
            elite = comp.SpawnedElite!.Value;
            Assert.That(entMan.HasComponent<GhostRoleComponent>(elite), Is.True,
                "a boosted elite must be offered to ghosts");
            Assert.That(entMan.HasComponent<GhostTakeoverAvailableComponent>(elite), Is.True);
        });

        // A boosted victory: the elite keeps the arena and comes back weaker next time.
        await server.WaitPost(() =>
            entMan.System<MobStateSystem>().ChangeMobState(user, MobState.Dead));
        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<EliteTumorComponent>(tumor);
            Assert.That(comp.State, Is.EqualTo(EliteTumorState.Passive));
            Assert.That(comp.TimesWon, Is.EqualTo(1));

            Assert.That(entMan.System<MobThresholdSystem>()
                .TryGetDeadThreshold(elite, out var threshold), Is.True);
            Assert.That(threshold!.Value, Is.EqualTo(FixedPoint2.New(400)),
                "a boosted victory must apply the post battle health multiplier");
        });

        // Rematch restores the elite health pool before the next fight.
        await server.WaitPost(() =>
            entMan.EventBus.RaiseLocalEvent(tumor, new InteractHandEvent(user, tumor)));
        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.System<MobThresholdSystem>()
                .TryGetDeadThreshold(elite, out var threshold), Is.True);
            Assert.That(threshold!.Value, Is.EqualTo(FixedPoint2.New(1000)),
                "a rematch must restore the elite health threshold");
        });

        await server.WaitPost(() =>
            entMan.System<DamageableSystem>().TryChangeDamage(elite,
                new DamageSpecifier { DamageDict = { { "Blunt", 2000f } } }, true));
        await server.WaitRunTicks(10);

        await server.WaitAssertion(() =>
        {
            EntityUid crate = default;
            var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out var uid, out var meta))
            {
                if (!entMan.Deleted(uid) && meta.EntityPrototype?.ID == "LavalandCrateNecropolis")
                {
                    crate = uid;
                    break;
                }
            }

            Assert.That(entMan.EntityExists(crate), Is.True, "a loot crate must spawn");
            var containers = entMan.System<SharedContainerSystem>();
            Assert.That(containers.TryGetContainer(crate, "entity_storage", out var container), Is.True);
            Assert.That(container!.ContainedEntities, Is.Not.Empty,
                "the boosted crate must contain a trophy or a tumor shard");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ElitesHaveGpsSignals()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        var expected = new (string Proto, string Name)[]
        {
            ("LavalandEliteLegionnaire", "Crumbling Signal"),
            ("LavalandElitePandora", "Blinking Signal"),
            ("LavalandEliteHerald", "Radiant Signal"),
            ("LavalandEliteGoliathBroodmother", "Unknown Signal"),
        };

        foreach (var (proto, name) in expected)
        {
            EntityUid elite = default;
            await server.WaitPost(() => elite = entMan.SpawnEntity(proto, testMap.GridCoords));
            await server.WaitRunTicks(2);

            await server.WaitAssertion(() =>
            {
                Assert.That(entMan.TryGetComponent<GPSComponent>(elite, out var gps), Is.True,
                    $"{proto} has no GPS component");
                Assert.That(gps!.GpsName, Is.EqualTo(name), $"{proto} GPS name mismatch");
                Assert.That(gps.Enabled, Is.True, $"{proto} GPS is disabled");
            });

            await server.WaitPost(() => entMan.DeleteEntity(elite));
        }

        await pair.CleanReturnAsync();
    }

    private static int CountPrototypes(IEntityManager entMan, string protoId)
    {
        var count = 0;
        var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var uid, out var meta))
        {
            if (!entMan.Deleted(uid) && meta.EntityPrototype?.ID == protoId)
                count++;
        }
        return count;
    }
}
