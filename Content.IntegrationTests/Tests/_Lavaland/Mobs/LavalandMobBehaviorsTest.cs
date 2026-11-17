// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using Content.Server._Lavaland.Mobs;
using Content.Server.Gravity;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks.Operators;
using Content.Shared._Mini.MiniCCVars;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Gravity;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stealth.Components;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Lavaland.Mobs;

/// <summary>
/// Ported TG fauna behaviours must actually trigger in game.
/// </summary>
[TestFixture]
public sealed class LavalandMobBehaviorsTest
{
    [Test]
    public async Task LobstrosityRushTelegraphsBeforeItHits()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var xformSys = server.System<SharedTransformSystem>();
        var testMap = await pair.CreateTestMap();

        EntityUid mob = default;
        EntityUid victim = default;
        var start = Vector2.Zero;

        await server.WaitPost(() =>
        {
            // The default test map is a single tile, the rush needs room to charge over.
            for (var x = 0; x <= 8; x++)
                mapSys.SetTile(testMap.Grid, new Vector2i(x, 0), testMap.Tile.Tile);

            mob = entMan.SpawnEntity("MobLavalandLobstrosity", testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            victim = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(5.5f, 0.5f)));

            // A tileless victim would be in space, where knockdown is ignored.
            var gravity = entMan.EnsureComponent<GravityComponent>(testMap.Grid);
            server.System<GravitySystem>().EnableGravity(testMap.Grid, gravity);

            start = xformSys.GetWorldPosition(mob);
        });

        // TG charge_delay is 1.5 seconds: the mob stands still and shakes before it charges.
        await server.WaitRunTicks(pair.SecondsToTicks(1f));

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<KnockedDownComponent>(victim), Is.False,
                "the rush must telegraph before it can hit");
            Assert.That((xformSys.GetWorldPosition(mob) - start).Length(), Is.LessThan(0.5f),
                "the lobster must not close the distance while it winds up");
        });

        // 1.5s windup plus 5 tiles at the TG charge_speed of 2 tiles per second.
        await server.WaitRunTicks(pair.SecondsToTicks(3.5f));

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<KnockedDownComponent>(victim), Is.True,
                "the lobster rush must knock the victim down");
            Assert.That((xformSys.GetWorldPosition(mob) - xformSys.GetWorldPosition(victim)).Length(), Is.LessThan(1.5f),
                "the lobster must charge into the victim instead of stopping short");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LobstrosityRushIgnoresTargetsOutsideChargeDistance()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var testMap = await pair.CreateTestMap();

        EntityUid mob = default;
        EntityUid victim = default;

        await server.WaitPost(() =>
        {
            for (var x = 0; x <= 11; x++)
                mapSys.SetTile(testMap.Grid, new Vector2i(x, 0), testMap.Tile.Tile);

            mob = entMan.SpawnEntity("MobLavalandLobstrosity", testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            victim = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(10.5f, 0.5f)));

            // Without its AI the lobster cannot walk into range by itself.
            entMan.RemoveComponent<HTNComponent>(mob);
        });

        await server.WaitRunTicks(pair.SecondsToTicks(3f));

        await server.WaitAssertion(() =>
        {
            var rush = entMan.GetComponent<LavalandRushComponent>(mob);
            Assert.That(rush.NextRush, Is.EqualTo(TimeSpan.Zero),
                "TG charge_distance is 8 tiles, the rush must not start from 10 tiles away");
            Assert.That(rush.DashTarget, Is.Null, "no rush may be in progress");
            Assert.That(entMan.HasComponent<KnockedDownComponent>(victim), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LobstrosityRushDoesNotOneShotAFullHealthVictim()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var testMap = await pair.CreateTestMap();

        EntityUid mob = default;
        EntityUid victim = default;

        await server.WaitPost(() =>
        {
            for (var x = 0; x <= 6; x++)
                mapSys.SetTile(testMap.Grid, new Vector2i(x, 0), testMap.Tile.Tile);

            mob = entMan.SpawnEntity("MobLavalandLobstrosity", testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            victim = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(4.5f, 0.5f)));

            // Knockdown is ignored in space.
            var gravity = entMan.EnsureComponent<GravityComponent>(testMap.Grid);
            server.System<GravitySystem>().EnableGravity(testMap.Grid, gravity);

            // No AI: the victim only has to survive the rush itself, not the follow up melee.
            entMan.RemoveComponent<HTNComponent>(mob);
        });

        // 1.5s windup plus 4 tiles at 2 tiles per second.
        await server.WaitRunTicks(pair.SecondsToTicks(4f));

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<KnockedDownComponent>(victim), Is.True,
                "the rush must have hit the victim");
            var damage = entMan.GetComponent<DamageableComponent>(victim).TotalDamage.Float();
            Assert.That(damage, Is.GreaterThan(0f), "the rush must damage the victim");
            Assert.That(server.System<MobStateSystem>().IsAlive(victim), Is.True,
                "one lobster rush must not kill a full health player");
            Assert.That(damage, Is.LessThan(100f), "the rush must stay below the 100 hp of a player");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LobstrosityRushGoesOnCooldownAndLeavesTheMobTired()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        var status = server.System<StatusEffectsSystem>();
        var testMap = await pair.CreateTestMap();

        EntityUid mob = default;

        await server.WaitPost(() =>
        {
            for (var x = 0; x <= 6; x++)
                mapSys.SetTile(testMap.Grid, new Vector2i(x, 0), testMap.Tile.Tile);

            mob = entMan.SpawnEntity("MobLavalandLobstrosity", testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(4.5f, 0.5f)));
            entMan.RemoveComponent<HTNComponent>(mob);
        });

        await server.WaitRunTicks(pair.SecondsToTicks(0.5f));

        await server.WaitAssertion(() =>
        {
            var rush = entMan.GetComponent<LavalandRushComponent>(mob);
            Assert.That(rush.DashTarget, Is.Not.Null, "the rush must have started");
            Assert.That((rush.NextRush - timing.CurTime).TotalSeconds, Is.InRange(5.3, 6.0),
                "TG cooldown_time is 6 seconds and it is counted from the start of the windup");
        });

        // 1.5s windup plus 4 tiles at 2 tiles per second, then the mob pays the tired cost.
        await server.WaitRunTicks(pair.SecondsToTicks(3.5f));

        await server.WaitAssertion(() =>
        {
            var rush = entMan.GetComponent<LavalandRushComponent>(mob);
            Assert.That(rush.DashTarget, Is.Null, "the rush must be over");
            Assert.That(status.HasStatusEffect(mob, "LavalandStatusEffectTired"), Is.True,
                "TG tired_post_charge must slow the lobster after the rush");
            Assert.That(timing.CurTime, Is.LessThan(rush.NextRush), "the cooldown must block the next rush");
        });

        // TG tired_post_charge lasts a second, the cooldown blocks the rush for six.
        await server.WaitRunTicks(pair.SecondsToTicks(1.2f));

        await server.WaitAssertion(() =>
        {
            var rush = entMan.GetComponent<LavalandRushComponent>(mob);
            Assert.That(status.HasStatusEffect(mob, "LavalandStatusEffectTired"), Is.False,
                "TG tired_post_charge lasts one second");
            Assert.That(rush.DashTarget, Is.Null, "a second rush has to wait for the cooldown");
            Assert.That(timing.CurTime, Is.LessThan(rush.NextRush), "the cooldown still has to be running");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LobstrosityRushStopsAtWalls()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var xformSys = server.System<SharedTransformSystem>();
        var testMap = await pair.CreateTestMap();

        EntityUid mob = default;
        EntityUid victim = default;

        await server.WaitPost(() =>
        {
            for (var x = 0; x <= 8; x++)
                mapSys.SetTile(testMap.Grid, new Vector2i(x, 0), testMap.Tile.Tile);

            mob = entMan.SpawnEntity("MobLavalandLobstrosity", testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            victim = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(5.5f, 0.5f)));

            // Knockdown is ignored in space, this test needs it to be a real hit.
            var gravity = entMan.EnsureComponent<GravityComponent>(testMap.Grid);
            server.System<GravitySystem>().EnableGravity(testMap.Grid, gravity);

            entMan.RemoveComponent<HTNComponent>(mob);
        });

        // Let the windup end and the dash start, then raise a wall in its path.
        await server.WaitRunTicks(pair.SecondsToTicks(1.6f));
        await server.WaitPost(() => entMan.SpawnEntity("WallSolid", testMap.GridCoords.Offset(new Vector2(2.5f, 0.5f))));

        await server.WaitRunTicks(pair.SecondsToTicks(3f));

        await server.WaitAssertion(() =>
        {
            var rush = entMan.GetComponent<LavalandRushComponent>(mob);
            Assert.That(entMan.HasComponent<KnockedDownComponent>(victim), Is.False,
                "the rush must stop at a wall");
            Assert.That(rush.DashTarget, Is.Null, "the wall must abort the rush");
            Assert.That(xformSys.GetWorldPosition(mob).X, Is.LessThan(2.4f),
                "the lobster must stay on its own side of the wall");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TamedGoldgrubAcceptsRider()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var hands = server.System<SharedHandsSystem>();
        var buckle = server.System<SharedBuckleSystem>();
        var testMap = await pair.CreateTestMap();

        EntityUid grub = default;
        EntityUid tamer = default;

        await server.WaitPost(() =>
        {
            tamer = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            grub = entMan.SpawnEntity("LavalandMobGoldgrub", testMap.GridCoords.Offset(new Vector2(1.5f, 0)));

            var tame = entMan.GetComponent<TameableMobComponent>(grub);
            tame.CurrentChance = 100f;

            var ore = entMan.SpawnEntity("GoldOre1", testMap.GridCoords);
            Assert.That(hands.TryPickupAnyHand(tamer, ore), Is.True);
            entMan.EventBus.RaiseLocalEvent(grub, new InteractUsingEvent(tamer, ore, grub, testMap.GridCoords));
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<TamedMobComponent>(grub), Is.True, "ore must tame the goldgrub");
            Assert.That(entMan.GetComponent<MountableMobComponent>(grub).Saddled, Is.True,
                "a tamed goldgrub must become rideable without a saddle");
            Assert.That(entMan.HasComponent<StrapComponent>(grub), Is.True,
                "a tamed goldgrub must have a strap for the rider");

            var rider = entMan.GetComponent<BuckleComponent>(tamer);
            Assert.That(buckle.TryBuckle(tamer, tamer, grub, rider), Is.True,
                "a tamed goldgrub must accept a rider");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GuntherLeavesAshwalkersAlone()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var factions = server.System<NpcFactionSystem>();
        var testMap = await pair.CreateTestMap();

        EntityUid gunther = default;
        EntityUid ashwalker = default;
        EntityUid human = default;

        await server.WaitPost(() =>
        {
            gunther = entMan.SpawnEntity("MobLavalandGutlunchWarrior", testMap.GridCoords);
            ashwalker = entMan.SpawnEntity("MobAshwalkerPlayable", testMap.GridCoords.Offset(new Vector2(1f, 0)));
            human = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(-1f, 0)));
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(factions.IsEntityFriendly(gunther, ashwalker), Is.True,
                "gunther must treat ashwalkers as friendly");
            Assert.That(factions.IsEntityFriendly(gunther, human), Is.False,
                "gunther must stay hostile to everyone else");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BilewormResurfacesNearItsTarget()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var xformSys = server.System<SharedTransformSystem>();

        EntityUid bileworm = default;
        var start = MapCoordinates.Nullspace;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var map);
            bileworm = entMan.SpawnEntity("LavalandMobBileworm", new MapCoordinates(0, 0, map));
            entMan.SpawnEntity("MobMonkey", new MapCoordinates(10, 0, map));
            start = xformSys.GetMapCoordinates(bileworm);
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            var now = xformSys.GetMapCoordinates(bileworm);
            Assert.That((now.Position - start.Position).Length(), Is.GreaterThan(1f),
                "the bileworm must burrow towards its target");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GoldgrubEatsOre()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();

        EntityUid grub = default;
        EntityUid ore = default;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var map);
            grub = entMan.SpawnEntity("LavalandMobGoldgrub", new MapCoordinates(0, 0, map));
            ore = entMan.SpawnEntity("GoldOre1", new MapCoordinates(0.5f, 0, map));
        });

        // The grub eats once a second, which is 30 ticks in the debug test preset.
        await server.WaitRunTicks(70);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(ore), Is.True, "the goldgrub must eat the ore next to it");
            Assert.That(entMan.GetComponent<LavalandOreEaterComponent>(grub).Eaten, Is.Not.Empty,
                "the goldgrub must remember what it ate");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GoldgrubWalksToOreOnTheGround()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var testMap = await pair.CreateTestMap();

        EntityUid grub = default;
        EntityUid ore = default;

        server.CfgMan.SetCVar(MiniCCVars.NPCDisableWithoutPlayers, false);

        await server.WaitPost(() =>
        {
            for (var x = -1; x <= 8; x++)
            {
                for (var y = -1; y <= 1; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), testMap.Tile.Tile);
            }

            grub = entMan.SpawnEntity("LavalandMobGoldgrub", testMap.GridCoords);
            ore = entMan.SpawnEntity("GoldOre1", testMap.GridCoords.Offset(new Vector2(5, 0)));

            // Two sleep systems gate NPC AI in this fork: one parks NPCs unless a player session is
            // nearby (off below, there is no session in a test), the other wants ActorComponent.
            var dummy = entMan.SpawnEntity("Wrench", testMap.GridCoords.Offset(new Vector2(0, -1)));
            entMan.EnsureComponent<ActorComponent>(dummy);

            var gravity = entMan.EnsureComponent<GravityComponent>(testMap.Grid);
            server.System<GravitySystem>().EnableGravity(testMap.Grid, gravity);
        });

        // The grub plans every 0.45 seconds; even 5 tiles away the loose ore has to win the query.
        await server.WaitRunTicks(30);

        await server.WaitAssertion(() =>
        {
            var htn = entMan.GetComponent<HTNComponent>(grub);
            var plan = htn.Plan;
            Assert.That(plan, Is.Not.Null, "the goldgrub must plan to go after loose ore");
            Assert.That(plan!.CurrentOperator, Is.InstanceOf<MoveToOperator>(),
                "the goldgrub must walk to the ore instead of idling");

            var target = htn.Blackboard.GetValueOrDefault<EntityCoordinates>("TargetCoordinates", entMan);
            Assert.That(target.EntityId, Is.EqualTo(ore), "the goldgrub must pick the loose ore on the floor");
        });

        // Integration tests run with pathfinding turned off (PoolManager.Cvars), so beeline to the ore
        // instead of waiting for a path. The walk/eat loop below is the part a player actually sees.
        await server.WaitPost(() =>
            entMan.GetComponent<NPCSteeringComponent>(grub).DirectMove = true);

        await server.WaitRunTicks(150);

        await server.WaitAssertion(() =>
            Assert.That(entMan.Deleted(ore), Is.True, "the goldgrub must walk to loose ore and eat it"));

        server.CfgMan.SetCVar(MiniCCVars.NPCDisableWithoutPlayers, true);
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GoldgrubBurrowsUnderTheGroundWhenHurt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var testMap = await pair.CreateTestMap();

        EntityUid grub = default;

        await server.WaitPost(() =>
        {
            for (var x = -1; x <= 8; x++)
            {
                for (var y = -1; y <= 1; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), testMap.Tile.Tile);
            }

            grub = entMan.SpawnEntity("LavalandMobGoldgrub", testMap.GridCoords);
            var slash = protoMan.Index<DamageTypePrototype>("Slash");
            entMan.System<DamageableSystem>()
                .TryChangeDamage(grub, new DamageSpecifier(slash, 10), origin: grub, canMiss: false);
        });

        // TG digs down for 2.5 seconds, so it is underground after ~3 seconds and back up shortly after.
        await server.WaitRunTicks(90);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<LavalandBurrowEscapeComponent>(grub).Underground, Is.True,
                "the hurt goldgrub must dig into the ground");
            Assert.That(entMan.HasComponent<StealthComponent>(grub), Is.True,
                "the goldgrub must be hidden while it is underground");
        });

        await server.WaitRunTicks(40);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<LavalandBurrowEscapeComponent>(grub).Underground, Is.False,
                "the goldgrub must come back up");
            Assert.That(entMan.HasComponent<StealthComponent>(grub), Is.False,
                "the goldgrub must be visible again after resurfacing");
        });

        await pair.CleanReturnAsync();
    }
}
