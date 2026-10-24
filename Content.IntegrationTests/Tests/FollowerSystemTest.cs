// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Reflection;
using Content.Server.GameTicking;
using Content.Shared.Follower;
using Content.Shared.Follower.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests;

[TestFixture, TestOf(typeof(FollowerSystem))]
public sealed class FollowerSystemTest
{
    /// <summary>
    ///     This test ensures that deleting a map while an entity follows another doesn't throw any exceptions.
    /// </summary>
    [Test]
    public async Task FollowerMapDeleteTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var sysMan = server.ResolveDependency<IEntitySystemManager>();
        var logMan = server.ResolveDependency<ILogManager>();
        var mapSys = server.System<SharedMapSystem>();
        var logger = logMan.RootSawmill;

        await server.WaitPost(() =>
        {
            var followerSystem = sysMan.GetEntitySystem<FollowerSystem>();

            // Create a map to spawn the observers on.
            mapSys.CreateMap(out var map);

            // Spawn an observer to be followed.
            var followed = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, new MapCoordinates(0, 0, map));
            logger.Info($"Spawned followed observer: {entMan.ToPrettyString(followed)}");

            // Spawn an observer to follow another observer.
            var follower = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, new MapCoordinates(0, 0, map));
            logger.Info($"Spawned follower observer: {entMan.ToPrettyString(follower)}");

            followerSystem.StartFollowingEntity(follower, followed);

            entMan.DeleteEntity(mapSys.GetMap(map));
        });
        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Deleting the bottom of a follower chain must unwind the links iteratively.
    ///     The middle entity stops following, while the link between the top and the
    ///     middle stays intact.
    /// </summary>
    [Test]
    public async Task FollowerChainDeleteTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var sysMan = server.ResolveDependency<IEntitySystemManager>();
        var mapSys = server.System<SharedMapSystem>();

        await server.WaitAssertion(() =>
        {
            var followerSystem = sysMan.GetEntitySystem<FollowerSystem>();

            mapSys.CreateMap(out var map);

            var bottom = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, new MapCoordinates(0, 0, map));
            var middle = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, new MapCoordinates(0, 0, map));
            var top = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, new MapCoordinates(0, 0, map));

            followerSystem.StartFollowingEntity(middle, bottom);
            followerSystem.StartFollowingEntity(top, middle);

            Assert.That(entMan.GetComponent<FollowerComponent>(middle).Following, Is.EqualTo(bottom));
            Assert.That(entMan.GetComponent<FollowerComponent>(top).Following, Is.EqualTo(middle));

            // The old code walked the chain through EntityTerminatingEvent and could
            // call StopAllFollowers again for an entity that was already being removed.
            entMan.DeleteEntity(bottom);

            Assert.That(entMan.HasComponent<FollowedComponent>(bottom), Is.False);
            Assert.That(entMan.HasComponent<FollowerComponent>(middle), Is.False);
            Assert.That(entMan.EntityExists(middle), Is.True);
            Assert.That(entMan.HasComponent<FollowedComponent>(middle), Is.True);

            // A follower of the middle entity is not part of the bottom link and must stay.
            Assert.That(entMan.GetComponent<FollowerComponent>(top).Following, Is.EqualTo(middle));
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     A corrupted self-reference must be cleaned up without bouncing between
    ///     StopAllFollowers and StopFollowingEntity.
    /// </summary>
    [Test]
    public async Task FollowerSelfReferenceStopTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var sysMan = server.ResolveDependency<IEntitySystemManager>();
        var mapSys = server.System<SharedMapSystem>();

        await server.WaitAssertion(() =>
        {
            var followerSystem = sysMan.GetEntitySystem<FollowerSystem>();

            mapSys.CreateMap(out var map);
            var uid = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, new MapCoordinates(0, 0, map));

            // StartFollowingEntity refuses to create this state, but stale or hand-edited
            // components can still contain it. Both sides must point at the entity for the
            // cleanup path to run.
            var follower = entMan.EnsureComponent<FollowerComponent>(uid);
            typeof(FollowerComponent).GetField(nameof(FollowerComponent.Following))!
                .SetValue(follower, uid);

            var followed = entMan.EnsureComponent<FollowedComponent>(uid);
            var following = (HashSet<EntityUid>) typeof(FollowedComponent)
                .GetField(nameof(FollowedComponent.Following))!.GetValue(followed)!;
            following.Add(uid);

            Assert.DoesNotThrow(() => followerSystem.StopAllFollowers(uid));

            Assert.That(entMan.HasComponent<FollowedComponent>(uid), Is.False);
            Assert.That(entMan.HasComponent<FollowerComponent>(uid), Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
