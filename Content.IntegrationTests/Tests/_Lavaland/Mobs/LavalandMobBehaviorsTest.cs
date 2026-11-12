// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using Content.Server._Lavaland.Mobs;
using Content.Server.Gravity;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Gravity;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Lavaland.Mobs;

/// <summary>
/// Ported TG fauna behaviours must actually trigger in game.
/// </summary>
[TestFixture]
public sealed class LavalandMobBehaviorsTest
{
    [Test]
    public async Task LobstrosityRushKnocksTheVictimDown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var testMap = await pair.CreateTestMap();

        EntityUid victim = default;

        await server.WaitPost(() =>
        {
            // A single-tile test grid would drop the victim into space, where knockdown is ignored.
            for (var x = 1; x <= 5; x++)
                mapSys.SetTile(testMap.Grid, new Vector2i(x, 0), testMap.Tile.Tile);

            entMan.SpawnEntity("MobLavalandLobstrosity", testMap.GridCoords);
            victim = entMan.SpawnEntity("MobMonkey", testMap.GridCoords.Offset(new Vector2(5, 0)));

            var gravity = entMan.EnsureComponent<GravityComponent>(testMap.Grid);
            server.System<GravitySystem>().EnableGravity(testMap.Grid, gravity);
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
            Assert.That(entMan.HasComponent<KnockedDownComponent>(victim), Is.False,
                "the lobster must dash to the victim instead of teleporting onto it"));

        await server.WaitRunTicks(20);

        await server.WaitAssertion(() =>
            Assert.That(entMan.HasComponent<KnockedDownComponent>(victim), Is.True,
                "the lobster dash must knock the victim down"));

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
}
