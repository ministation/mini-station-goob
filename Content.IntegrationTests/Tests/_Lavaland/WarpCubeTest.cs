// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server._Lavaland.Loot;
using Content.Shared._Lavaland.Loot;
using Content.Shared.Interaction.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Lavaland;

/// <summary>
/// A warp cube teleports its user to the linked twin cube. When the twin is on the user
/// (spawned in the same hand or carried in a pocket) the destination coordinates are parented
/// to the user, and the engine used to fail to parent the user to itself, delete it and log
/// an exception in DispatchEntityNetworkMessage.
/// </summary>
[TestFixture]
[TestOf(typeof(WarpCubeSystem))]
public sealed class WarpCubeTest
{
    [Test]
    public async Task TwinOnUserDoesNotDeleteUser()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var xform = server.System<SharedTransformSystem>();

        var user = EntityUid.Invalid;
        var cube = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var map);

            user = entMan.SpawnEntity(null, new MapCoordinates(0, 0, map));
            // Spawning the cube in the user's hand makes MapInit spawn the twin at the same
            // coordinates, i.e. parented to the user.
            cube = entMan.SpawnEntity("LavalandWarpCubeRed", new EntityCoordinates(user, Vector2.Zero));
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.TryGetComponent(cube, out WarpCubeComponent? warp), Is.True);
            Assert.That(warp!.Partner, Is.Not.Null);

            var twin = warp.Partner.Value;
            Assert.That(xform.GetParentUid(cube), Is.EqualTo(user));
            Assert.That(xform.GetParentUid(twin), Is.EqualTo(user));

            entMan.EventBus.RaiseLocalEvent(cube, new UseInHandEvent(user));

            Assert.That(entMan.Deleted(user), Is.False,
                "teleporting to coordinates parented to the user must not delete the user");

            // The user still stands where they were and keeps both cubes.
            Assert.That(xform.GetParentUid(twin), Is.EqualTo(user));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TwinInContainerOnUserDoesNotDeleteUser()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var xform = server.System<SharedTransformSystem>();

        var user = EntityUid.Invalid;
        var cube = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var map);

            user = entMan.SpawnEntity(null, new MapCoordinates(0, 0, map));
            cube = entMan.SpawnEntity("LavalandWarpCubeRed", new MapCoordinates(1, 0, map));
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.TryGetComponent(cube, out WarpCubeComponent? warp), Is.True);
            Assert.That(warp!.Partner, Is.Not.Null);

            var twin = warp.Partner.Value;

            // The twin lies in a container (backpack or pocket) carried by the user.
            var container = entMan.SpawnEntity(null, new EntityCoordinates(user, Vector2.Zero));
            xform.SetCoordinates(twin, new EntityCoordinates(container, Vector2.Zero));

            entMan.EventBus.RaiseLocalEvent(cube, new UseInHandEvent(user));

            Assert.That(entMan.Deleted(user), Is.False,
                "teleporting to coordinates parented to the user's container must not delete the user");
        });

        await pair.CleanReturnAsync();
    }
}
