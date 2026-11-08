// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Lavaland.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

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

        EntityUid victim = default;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var map);
            entMan.SpawnEntity("MobLavalandLobstrosity", new MapCoordinates(0, 0, map));
            victim = entMan.SpawnEntity("MobMonkey", new MapCoordinates(5, 0, map));
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
            Assert.That(entMan.HasComponent<KnockedDownComponent>(victim), Is.True,
                "the lobster rush must knock the victim down"));

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

        await server.WaitRunTicks(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(ore), Is.True, "the goldgrub must eat the ore next to it");
            Assert.That(entMan.GetComponent<LavalandOreEaterComponent>(grub).Eaten, Is.Not.Empty,
                "the goldgrub must remember what it ate");
        });

        await pair.CleanReturnAsync();
    }
}
