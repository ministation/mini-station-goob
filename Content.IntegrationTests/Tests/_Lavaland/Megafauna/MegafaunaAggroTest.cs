// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.Damage.Systems;
using Content.Shared._Lavaland.Aggression;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.UnitTesting;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

/// <summary>
/// A boss has to notice a player that walks into its vision range without taking a hit first.
/// Otherwise the boss stands inert until the player attacks it, because the megafauna AI only
/// starts on <see cref="AggressorAddedEvent"/>.
/// </summary>
[TestFixture]
public sealed class MegafaunaAggroTest
{
    /// <summary>
    /// All reachable lavaland megafauna. Each one has a MegafaunaAiComponent and an
    /// AggressiveComponent, which is what proximity aggro is built on.
    /// </summary>
    private static readonly string[] BossPrototypes =
    {
        "LavalandBossAshDrake",
        "LavalandBossColossus",
        "MobHierophant",
        "LavalandBossWendigo",
        "LavalandBossDemonicFrostMiner",
        "LavalandBossThing",
        // Bubblegum has no MegafaunaAiComponent, its own BubblegumSystem drives it. It is checked last
        // so a failure there does not hide the other bosses.
        "LavalandBossBubblegum",
    };

    /// <summary>Inside the TG vision range, which is nine tiles.</summary>
    private const float AggroDistance = 6f;

    /// <summary>Proximity scans happen once per 0.5 s, 90 ticks is three scans.</summary>
    private const int AggroTickLimit = 90;

    /// <summary>Time for the first selector to run and for the boss to start walking.</summary>
    private const int ActTickLimit = 240;

    /// <summary>The death cleanup raises its events immediately, one second is plenty.</summary>
    private const int DropTickLimit = 60;

    private const int PollChunk = 10;

    [Test]
    public async Task BossesAggroWithoutDamage()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var xformSys = entMan.System<SharedTransformSystem>();
        var testMap = await pair.CreateTestMap();

        await CreateArena(server, mapSys, testMap);
        var player = await SpawnPlayer(server, entMan, testMap, Vector2.Zero, pair.Player);

        foreach (var bossId in BossPrototypes)
        {
            // Boss attacks can shove or drag the player around, put it back in front of the next boss.
            await server.WaitPost(() => xformSys.SetCoordinates(player, testMap.GridCoords));

            EntityUid boss = default;
            await server.WaitPost(() =>
                boss = entMan.SpawnEntity(bossId, testMap.GridCoords.Offset(new Vector2(AggroDistance, 0f))));

            var aggroed = await WaitUntil(server, AggroTickLimit, () =>
                entMan.TryGetComponent<AggressiveComponent>(boss, out var aggressive)
                && aggressive.Aggressors.Contains(player));

            Assert.That(aggroed, Is.True,
                bossId + " never noticed a player " + AggroDistance + " tiles away, the boss stands inert until it takes damage");

            await server.WaitAssertion(() =>
            {
                var aggressive = entMan.GetComponent<AggressiveComponent>(boss);
                Assert.That(aggressive.Aggressors, Does.Contain(player), bossId + " must target the nearby player");

                // Bubblegum has no MegafaunaAiComponent, its own BubblegumSystem is the brain.
                if (entMan.TryGetComponent<MegafaunaAiComponent>(boss, out var ai))
                {
                    Assert.That(ai.Active, Is.True, bossId + " must start its AI on proximity aggro");
                    Assert.That(ai.Schedule, Is.Not.Empty, bossId + " must schedule an attack after proximity aggro");
                }
            });

            // Snapshot the state at the moment of aggro: from here the boss must either walk or pick an attack.
            var startPos = Vector2.Zero;
            HashSet<TimeSpan> scheduledBefore = default!;
            await server.WaitPost(() =>
            {
                var bossXform = entMan.GetComponent<TransformComponent>(boss);
                startPos = xformSys.GetWorldPosition(bossXform);
                scheduledBefore = entMan.TryGetComponent<MegafaunaAiComponent>(boss, out var ai)
                    ? ai.Schedule.Keys.ToHashSet()
                    : new HashSet<TimeSpan>();
            });

            var acted = await WaitUntil(server, ActTickLimit, () =>
            {
                if (entMan.TryGetComponent<MegafaunaAiComponent>(boss, out var ai)
                    && ai.Schedule.Keys.Any(time => !scheduledBefore.Contains(time)))
                {
                    return true;
                }

                var bossXform = entMan.GetComponent<TransformComponent>(boss);
                return (xformSys.GetWorldPosition(bossXform) - startPos).Length() > 0.25f;
            });

            Assert.That(acted, Is.True, bossId + " has an active AI but never moved and never picked an attack");

            await CleanMap(server, entMan, testMap, player);
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AggroDropsWhenPlayerDies()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var mobState = entMan.System<MobStateSystem>();
        var testMap = await pair.CreateTestMap();

        await CreateArena(server, mapSys, testMap);

        foreach (var bossId in BossPrototypes)
        {
            var player = await SpawnPlayer(server, entMan, testMap, Vector2.Zero, pair.Player);

            EntityUid boss = default;
            await server.WaitPost(() =>
                boss = entMan.SpawnEntity(bossId, testMap.GridCoords.Offset(new Vector2(AggroDistance, 0f))));

            var aggroed = await WaitUntil(server, AggroTickLimit, () =>
                entMan.TryGetComponent<AggressiveComponent>(boss, out var aggressive)
                && aggressive.Aggressors.Contains(player));

            Assert.That(aggroed, Is.True, bossId + " never aggroed, so the drop cannot be checked");

            // A corpse stops being a threat: the aggressor must be dropped and the boss must go idle,
            // otherwise it keeps attacking the corpse or walks around with a stale target.
            await server.WaitPost(() => mobState.ChangeMobState(player, MobState.Dead));

            var dropped = await WaitUntil(server, DropTickLimit, () =>
                entMan.TryGetComponent<AggressiveComponent>(boss, out var aggressive)
                && aggressive.Aggressors.Count == 0
                && IsIdle(entMan, boss));

            Assert.That(dropped, Is.True, bossId + " must drop a dead player and go idle again");

            await CleanMap(server, entMan, testMap, player);
            await server.WaitPost(() =>
            {
                if (!entMan.Deleted(player))
                    entMan.DeleteEntity(player);
            });
        }

        await pair.CleanReturnAsync();
    }

    private static bool IsIdle(IEntityManager entMan, EntityUid boss)
        => !entMan.TryGetComponent<MegafaunaAiComponent>(boss, out var ai) || !ai.Active;

    private static async Task CreateArena(RobustIntegrationTest.ServerIntegrationInstance server, SharedMapSystem mapSys, TestMapData testMap)
    {
        await server.WaitPost(() =>
        {
            var tile = testMap.Tile.Tile;
            for (var x = -60; x <= 60; x++)
            {
                for (var y = -60; y <= 60; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
            }
        });
    }

    private static async Task<EntityUid> SpawnPlayer(RobustIntegrationTest.ServerIntegrationInstance server,
        IEntityManager entMan,
        TestMapData testMap,
        Vector2 offset,
        ICommonSession? session)
    {
        EntityUid player = default;
        await server.WaitPost(() =>
        {
            player = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(offset));
            // A real session matters: mob threshold updates send network events to the actor, and a
            // hand made ActorComponent has no session to send them to.
            server.PlayerMan.SetAttachedEntity(session, player, true);
            // Keep the player alive: this test is about noticing a player, not about surviving the boss.
            entMan.System<GodmodeSystem>().EnableGodmode(player);
        });
        return player;
    }

    private static async Task CleanMap(RobustIntegrationTest.ServerIntegrationInstance server, IEntityManager entMan, TestMapData testMap, EntityUid keep)
    {
        await server.WaitPost(() =>
        {
            var toDelete = new List<EntityUid>();
            var query = entMan.EntityQueryEnumerator<TransformComponent>();
            while (query.MoveNext(out var uid, out var xform))
            {
                if (uid == keep || uid == testMap.Grid.Owner || uid == testMap.MapUid)
                    continue;

                if (xform.MapID != testMap.MapId)
                    continue;

                toDelete.Add(uid);
            }

            foreach (var uid in toDelete)
            {
                if (!entMan.Deleted(uid))
                    entMan.DeleteEntity(uid);
            }
        });
    }

    private static async Task<bool> WaitUntil(RobustIntegrationTest.ServerIntegrationInstance server, int limit, Func<bool> condition)
    {
        for (var elapsed = 0; elapsed < limit; elapsed += PollChunk)
        {
            await server.WaitRunTicks(PollChunk);

            var done = false;
            await server.WaitPost(() => done = condition());
            if (done)
                return true;
        }

        return false;
    }
}
