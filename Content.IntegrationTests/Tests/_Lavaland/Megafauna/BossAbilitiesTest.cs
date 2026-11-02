// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Goobstation.Shared.Projectiles;
using Content.Server._Lavaland.Bubblegum;
using Content.Server.Fluids.EntitySystems;
using Content.Shared._Lavaland.Aggression;
using Content.Shared._Lavaland.Megafauna;
using Content.Shared._Lavaland.Megafauna.Selectors;
using Content.Shared.Actions.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Movement.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

/// <summary>
/// Guards the TG parity bits of the boss kits: Bubblegum enrages after a blood warp,
/// and the ash drake has the meteor shower and mass fire abilities wired into its AI.
/// </summary>
[TestFixture]
public sealed class BossAbilitiesTest
{
    [Test]
    public async Task BubblegumEnragesAfterBloodWarp()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();

        // Mobs need a floor to stand and spill blood on, and the test map has a single tile.
        await server.WaitPost(() =>
        {
            for (var x = -4; x <= 4; x++)
            {
                for (var y = -4; y <= 4; y++)
                    mapSys.SetTile(testMap.Grid, new EntityCoordinates(testMap.Grid, x, y), testMap.Tile.Tile);
            }
        });

        EntityUid boss = default;
        EntityUid target = default;
        await server.WaitPost(() =>
        {
            boss = entMan.SpawnEntity("LavalandBossBubblegum", testMap.GridCoords);

            // The warp refuses to teleport right next to the target, so keep it three tiles away.
            target = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(3f, 0f)));
            entMan.EnsureComponent<GodmodeComponent>(target);
        });

        await server.WaitRunTicks(5);

        var speedBefore = 0f;
        var warped = false;
        await server.WaitPost(() =>
        {
            var gridUid = entMan.GetComponent<TransformComponent>(boss).GridUid!.Value;
            var grid = entMan.GetComponent<MapGridComponent>(gridUid);
            var puddles = entMan.System<PuddleSystem>();

            // Blood next to the boss and next to the target: this is what the warp picks its destination from.
            foreach (var tile in new[] { new Vector2i(0, 1), new Vector2i(1, 0), new Vector2i(2, 1) })
            {
                var tileRef = mapSys.GetTileRef(gridUid, grid, tile);
                puddles.TrySpillAt(tileRef, new Solution("Blood", FixedPoint2.New(25)), out _, sound: false);
            }

            var aggressive = entMan.GetComponent<AggressiveComponent>(boss);
            entMan.System<AggressorsSystem>().AddAggressor((boss, aggressive), target);

            // Keep the boss's own AI from warping again while the test measures the seven second window.
            var bubblegum = entMan.GetComponent<BubblegumComponent>(boss);
            bubblegum.NextAttack = TimeSpan.FromDays(1);
            bubblegum.NextBloodReaction = TimeSpan.FromDays(1);
            bubblegum.NextSummon = TimeSpan.FromDays(1);

            var speedComp = entMan.GetComponent<MovementSpeedModifierComponent>(boss);
            speedBefore = speedComp.SprintSpeedModifier * speedComp.BaseSprintSpeed;

            warped = entMan.System<BubblegumSystem>().TryBloodWarp(boss, bubblegum, gridUid, grid, target);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(warped, Is.True, "blood warp should succeed with blood pools around the boss and the target");

            var bubblegum = entMan.GetComponent<BubblegumComponent>(boss);
            Assert.That(bubblegum.Enraged, Is.True, "a successful blood warp should enrage Bubblegum");
            Assert.That(entMan.HasComponent<ProjectileImmunityComponent>(boss), Is.True,
                "an enraged Bubblegum should be immune to projectiles");

            var speed = entMan.GetComponent<MovementSpeedModifierComponent>(boss);
            var speedNow = speed.SprintSpeedModifier * speed.BaseSprintSpeed;
            Assert.That(speedNow, Is.GreaterThan(speedBefore * 1.2f),
                "an enraged Bubblegum should move faster: was " + speedBefore.ToString("0.00")
                + ", now " + speedNow.ToString("0.00"));

            var clouds = 0;
            var query = entMan.EntityQueryEnumerator<BubblegumEnrageCloudComponent>();
            while (query.MoveNext(out _, out _))
                clouds++;

            Assert.That(clouds, Is.GreaterThanOrEqualTo(1), "the warp should leave a blood cloud behind");
        });

        // Integration instances run at their own tick rate, so count seconds through the timing of the test server.
        var tickRate = server.ResolveDependency<IGameTiming>().TickRate;

        // The enrage lasts seven seconds, so it must still be active two seconds in.
        await server.WaitRunTicks(tickRate * 2);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<BubblegumComponent>(boss).Enraged, Is.True,
                "the enrage should still be active two seconds in");
        });

        await server.WaitRunTicks(tickRate * 8);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<BubblegumComponent>(boss).Enraged, Is.False,
                "the enrage should end after seven seconds");
            Assert.That(entMan.HasComponent<ProjectileImmunityComponent>(boss), Is.False,
                "the projectile immunity should end together with the enrage");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DrakeHasMeteorsAndMassFire()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        EntityUid drake = default;
        await server.WaitPost(() =>
        {
            drake = entMan.SpawnEntity("LavalandBossAshDrake", testMap.GridCoords);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.TryGetComponent<ActionsComponent>(drake, out var actions), Is.True,
                "the ash drake is missing Actions");

            var granted = new List<string>();
            foreach (var action in actions!.Actions)
            {
                if (entMan.Deleted(action))
                    continue;

                var actionMeta = entMan.GetComponent<MetaDataComponent>(action);
                granted.Add(actionMeta.EntityPrototype?.ID ?? action.ToString());
            }

            Assert.That(granted, Does.Contain("ActionDrakeMeteors"), "the drake was not granted ActionDrakeMeteors: " + string.Join(", ", granted));
            Assert.That(granted, Does.Contain("ActionDrakeMassFire"), "the drake was not granted ActionDrakeMassFire: " + string.Join(", ", granted));

            var group = protoMan.Index<MegafaunaSelectorPrototype>("AshDrakeActions").Selector;
            Assert.That(group, Is.InstanceOf<GroupMegafaunaSelector>(), "AshDrakeActions should be a group selector");

            var children = new List<string>();
            foreach (var child in ((GroupMegafaunaSelector) group).Children)
            {
                switch (child)
                {
                    case ProtoIdMegafaunaSelector protoId:
                        children.Add(protoId.Id.Id);
                        break;
                    case NestedMegafaunaSelector nested:
                        children.Add(nested.Id.Id);
                        break;
                }
            }

            Assert.That(children, Does.Contain("ActionDrakeMeteors"), "AshDrakeActions does not roll the meteor selector: " + string.Join(", ", children));
            Assert.That(children, Does.Contain("ActionDrakeMassFire"), "AshDrakeActions does not roll the mass fire selector: " + string.Join(", ", children));

            // Each child id must lead to a selector that actually performs the matching action.
            foreach (var actionId in new[] { "ActionDrakeMeteors", "ActionDrakeMassFire" })
            {
                var selectorPrototype = protoMan.Index<MegafaunaSelectorPrototype>(actionId).Selector;
                Assert.That(selectorPrototype, Is.InstanceOf<PerformActionSelector>(), actionId + " should be performed by the AI");

                if (selectorPrototype is PerformActionSelector perform)
                {
                    Assert.That(perform.ActionId.Id, Is.EqualTo(actionId),
                        actionId + " wires a different action: " + perform.ActionId.Id);
                }
            }

            Assert.That(protoMan.HasIndex<EntityPrototype>("LavalandDrakeMeteorBurst"), Is.True,
                "the meteor action spawner prototype is missing");
            Assert.That(protoMan.HasIndex<EntityPrototype>("LavalandDrakeMassFireBurst"), Is.True,
                "the mass fire action spawner prototype is missing");
        });

        // Spawn both effect chains directly: this catches broken prototype components as well.
        await server.WaitPost(() =>
        {
            entMan.SpawnEntity("LavalandDrakeMeteorBurst", testMap.GridCoords);
            entMan.SpawnEntity("LavalandDrakeMassFireBurst", testMap.GridCoords.Offset(new Vector2(4f, 0f)));
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(CountPrototype(entMan, "LavalandDrakeMeteor"), Is.GreaterThanOrEqualTo(9),
                "the meteor shower should call down a 3x3 volley of meteors");
            Assert.That(CountPrototype(entMan, "LavalandLavaPool"), Is.GreaterThanOrEqualTo(25),
                "mass fire should cover a 5x5 area with fire");
        });

        // The meteors land and turn into slam damage, so the falling visual must clean itself up.
        await server.WaitRunTicks(40);
        await server.WaitAssertion(() =>
        {
            Assert.That(CountPrototype(entMan, "LavalandDrakeMeteor"), Is.EqualTo(0),
                "meteors should despawn after landing");
        });

        await pair.CleanReturnAsync();
    }

    private static int CountPrototype(IEntityManager entMan, string prototype)
    {
        var count = 0;
        var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var uid, out var meta))
        {
            if (!entMan.Deleted(uid) && meta.EntityPrototype?.ID == prototype)
                count++;
        }

        return count;
    }
}
