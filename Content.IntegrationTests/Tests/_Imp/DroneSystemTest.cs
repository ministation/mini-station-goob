// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Imp.Drone;
using Content.Shared.ActionBlocker;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Imp;

/// <summary>
/// A drone using a blacklisted item with no crew nearby used to format the still-default
/// NearestEnt (EntityUid.Invalid) into the "drone-cant-use-nearby" popup. The loc formatter
/// then logged "Entity 0 does not have a component of type MetaDataComponent".
/// </summary>
[TestFixture]
[TestOf(typeof(DroneSystem))]
public sealed class DroneSystemTest
{
    [Test]
    public async Task BlacklistedUseWithoutNearbyBeingDoesNotLogError()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid drone = default;
        EntityUid syringe = default;
        await server.WaitPost(() =>
        {
            drone = server.EntMan.SpawnEntity("Drone", map.GridCoords);
            syringe = server.EntMan.SpawnEntity("Syringe", map.GridCoords);
        });

        await server.WaitAssertion(() =>
        {
            // Syringes are in the drone blacklist and not in its whitelist, so this is exactly the path
            // that used to warn about a nearby being while nobody was in range.
            Assert.That(server.System<ActionBlockerSystem>().CanUseHeldEntity(drone, syringe), Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
