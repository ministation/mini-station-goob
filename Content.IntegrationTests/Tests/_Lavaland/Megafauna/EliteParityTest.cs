// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server._Lavaland.Mobs;
using Content.Shared._Lavaland.Elites.Components;
using Content.Shared._Lavaland.Elites.Events;
using Content.Shared.Actions;
using Content.Shared.Armor;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Projectiles;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

[TestFixture]
public sealed class EliteParityTest
{
    private static readonly string[] Elites =
    {
        "LavalandEliteLegionnaire",
        "LavalandElitePandora",
        "LavalandEliteHerald",
        "LavalandEliteGoliathBroodmother",
    };

    [Test]
    public async Task EliteTrophiesAndActionsAreWired()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var actions = server.System<SharedActionsSystem>();

        var elites = new EntityUid[Elites.Length];
        EntityUid cloak = default;
        EntityUid hope = default;
        EntityUid shard = default;

        await server.WaitPost(() =>
        {
            cloak = entMan.SpawnEntity("LavalandHeraldCloak", testMap.GridCoords);
            hope = entMan.SpawnEntity("LavalandPandoraHope", testMap.GridCoords);
            shard = entMan.SpawnEntity("LavalandTumorShard", testMap.GridCoords);

            for (var i = 0; i < Elites.Length; i++)
                elites[i] = entMan.SpawnEntity(Elites[i], testMap.GridCoords);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<HeraldCloakComponent>(cloak).Count, Is.EqualTo(8),
                "the herald cloak should answer with eight bolts");

            var clothing = entMan.GetComponent<ClothingComponent>(hope);
            Assert.That(clothing.Slots.HasFlag(SlotFlags.NECK), Is.True,
                "pandora's hope should be wearable on the neck");
            Assert.That(entMan.HasComponent<ArmorComponent>(hope), Is.True,
                "pandora's hope should give a light armour buff");

            Assert.That(entMan.HasComponent<TumorShardComponent>(shard), Is.True,
                "the tumor shard needs its revive component");

            for (var i = 0; i < Elites.Length; i++)
            {
                Assert.That(entMan.HasComponent<LavalandEliteComponent>(elites[i]), Is.True,
                    $"{Elites[i]} should be revivable by the tumor shard");
            }

            Assert.That(actions.TryGetActionById(elites[0], "ActionEliteLegionnaireTurret", out _), Is.True,
                "the legionnaire should be granted the bone turret action");
            Assert.That(actions.TryGetActionById(elites[2], "ActionEliteHeraldTeleshot", out _), Is.True,
                "the herald should be granted the teleshot action");
            Assert.That(actions.TryGetActionById(elites[3], "ActionEliteBroodTentacle", out _), Is.True,
                "the broodmother should be granted the tentacle patch action");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HeraldCloakAnswersProjectileHits()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var inventory = server.System<InventorySystem>();

        EntityUid wearer = default;
        EntityUid cloak = default;
        EntityUid bolt = default;

        await server.WaitPost(() =>
        {
            wearer = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            cloak = entMan.SpawnEntity("LavalandHeraldCloak", testMap.GridCoords);
            entMan.GetComponent<HeraldCloakComponent>(cloak).Chance = 1f;
            bolt = entMan.SpawnEntity("LavalandHeraldBolt", testMap.GridCoords.Offset(new Vector2(0, 2)));
            inventory.TryEquip(wearer, cloak, "neck", force: true);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(inventory.TryGetSlotEntity(wearer, "neck", out var equipped) && equipped == cloak, Is.True,
                "the cloak has to be worn for the counter fire to work");
        });

        await server.WaitAssertion(() =>
        {
            var projectile = entMan.GetComponent<ProjectileComponent>(bolt);
            var ev = new ProjectileReflectAttemptEvent(bolt, projectile, false);
            entMan.EventBus.RaiseLocalEvent(wearer, ref ev);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var spawned = 0;
            var query = entMan.AllEntityQueryEnumerator<ProjectileComponent>();
            while (query.MoveNext(out var uid, out _))
            {
                if (uid != bolt)
                    spawned++;
            }

            Assert.That(spawned, Is.GreaterThanOrEqualTo(8),
                "the herald cloak should answer a projectile hit with a ring of bolts");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TumorShardRevivesDeadElite()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var mobState = server.System<MobStateSystem>();
        var faction = server.System<NpcFactionSystem>();

        EntityUid elite = default;
        EntityUid user = default;
        EntityUid shard = default;

        await server.WaitPost(() =>
        {
            elite = entMan.SpawnEntity("LavalandEliteLegionnaire", testMap.GridCoords);
            user = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(1, 0)));
            shard = entMan.SpawnEntity("LavalandTumorShard", testMap.GridCoords.Offset(new Vector2(0, 1)));
        });

        await server.WaitRunTicks(2);
        await server.WaitPost(() => mobState.ChangeMobState(elite, MobState.Dead));
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(mobState.IsDead(elite), Is.True, "test setup: the elite has to be dead");
        });

        await server.WaitPost(() =>
        {
            var coords = entMan.GetComponent<TransformComponent>(elite).Coordinates;
            var ev = new InteractUsingEvent(user, shard, elite, coords);
            entMan.EventBus.RaiseLocalEvent(elite, ev);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(mobState.IsAlive(elite), Is.True, "the tumor shard should revive the elite");
            Assert.That(entMan.Deleted(shard), Is.True, "the tumor shard should be consumed");
            Assert.That(faction.IsEntityFriendly(elite, user), Is.True,
                "a revived elite should be friendly to the crew");
            Assert.That(entMan.HasComponent<SpawnLootOnDeathComponent>(elite), Is.False,
                "a revived elite must not drop loot a second time");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BroodmotherSpawnsLimitedChildren()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid broodmother = default;

        await server.WaitPost(() => broodmother = entMan.SpawnEntity("LavalandEliteGoliathBroodmother", testMap.GridCoords));
        await server.WaitRunTicks(2);

        await server.WaitPost(() =>
        {
            var coords = entMan.GetComponent<TransformComponent>(broodmother).Coordinates;
            for (var i = 0; i < 5; i++)
            {
                var ev = new SpawnBroodActionEvent
                {
                    Performer = broodmother,
                    Target = coords,
                    Child = "MobLavalandGoliathChild",
                    Count = 2,
                };
                entMan.EventBus.RaiseLocalEvent(broodmother, ev);
            }
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var children = 0;
            var query = entMan.AllEntityQueryEnumerator<EliteChildComponent, MobStateComponent>();
            while (query.MoveNext(out var uid, out var child, out _))
            {
                if (child.Parent != broodmother)
                    continue;

                children++;
                Assert.That(entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID,
                    Is.EqualTo("MobLavalandGoliathChild"));
            }

            Assert.That(children, Is.EqualTo(8), "the broodmother should cap her brood at eight children");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LegionnaireTurretsAreLimited()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid legionnaire = default;

        await server.WaitPost(() =>
        {
            legionnaire = entMan.SpawnEntity("LavalandEliteLegionnaire", testMap.GridCoords);
            var coords = entMan.GetComponent<TransformComponent>(legionnaire).Coordinates;
            for (var i = 0; i < 3; i++)
            {
                var ev = new SpawnTurretActionEvent
                {
                    Performer = legionnaire,
                    Target = coords.Offset(new Vector2(i + 1, 0)),
                    Turret = "LavalandLegionBoneTurret",
                };
                entMan.EventBus.RaiseLocalEvent(legionnaire, ev);
            }
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var turrets = 0;
            var query = entMan.AllEntityQueryEnumerator<LegionTurretComponent>();
            while (query.MoveNext(out _, out _))
                turrets++;

            Assert.That(turrets, Is.EqualTo(2), "only two bone turrets should stand at once");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BoneTurretFiresAtHostiles()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid victim = default;

        await server.WaitPost(() =>
        {
            entMan.SpawnEntity("LavalandLegionBoneTurret", testMap.GridCoords);
            victim = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(3, 0)));
        });

        await server.WaitRunTicks(90);

        await server.WaitAssertion(() =>
        {
            var damage = entMan.GetComponent<DamageableComponent>(victim).TotalDamage.Value;
            Assert.That(damage, Is.GreaterThan(0), "the bone turret should shoot nearby hostiles");
        });

        await pair.CleanReturnAsync();
    }
}
