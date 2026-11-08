// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable annotations

using System.Collections.Generic;
using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Goobstation.Shared.Projectiles;
using Content.Server._Lavaland.Bubblegum;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Random.Helpers;
using Content.Shared.Sprite;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

/// <summary>
/// TG parity checks for Bubblegum's blood warp enrage: the red tint, the duration
/// window and the cooldown before it can enrage again.
/// </summary>
[TestFixture]
public sealed class BubblegumParityTest
{
    private const string BossProto = "LavalandBossBubblegum";

    [Test]
    public async Task BloodWarpEnrageTintsAndThenCooldownBlocks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var puddles = server.System<PuddleSystem>();
        var mapSys = entMan.System<SharedMapSystem>();
        var sys = server.System<BubblegumSystem>();

        var testMap = await pair.CreateTestMap();
        EntityUid boss = default;
        EntityUid cow = default;
        EntityUid grid = default;

        await server.WaitPost(() =>
        {
            var tile = testMap.Tile.Tile;
            for (var x = -14; x <= 14; x++)
            {
                for (var y = -14; y <= 14; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
            }

            boss = entMan.SpawnEntity(BossProto, testMap.GridCoords);
            cow = entMan.SpawnEntity("MobCow", testMap.GridCoords.Offset(new Vector2(8f, 0f)));
            grid = testMap.Grid;

            var aggressive = entMan.GetComponent<Content.Shared._Lavaland.Aggression.AggressiveComponent>(boss);
            entMan.System<Content.Shared._Lavaland.Aggression.AggressorsSystem>().AddAggressor((boss, aggressive), cow);
        });

        await server.WaitRunTicks(2);

        // Blood pools near the boss and near the target are the warp anchors. The boss drinks
        // nearby puddles within a couple of ticks, so spill and warp inside the same post.
        // TrySpillAt consumes the solution, so every spill needs a fresh one.
        Solution FreshBlood() => new Solution("Blood", FixedPoint2.New(25));

        bool AttemptWarp()
        {
            var gridComp = entMan.GetComponent<MapGridComponent>(grid);
            var bossPos = entMan.GetComponent<TransformComponent>(boss).Coordinates;
            var cowPos = entMan.GetComponent<TransformComponent>(cow).Coordinates;
            var bossTile = mapSys.TileIndicesFor(grid, gridComp, bossPos);
            var cowAnchor = mapSys.TileIndicesFor(grid, gridComp, cowPos) + new Vector2i(2, 0);
            puddles.TrySpillAt(mapSys.GetTileRef(grid, gridComp, bossTile), FreshBlood(), out _, sound: false);
            puddles.TrySpillAt(mapSys.GetTileRef(grid, gridComp, cowAnchor), FreshBlood(), out _, sound: false);

            var bubblegum = entMan.GetComponent<BubblegumComponent>(boss);
            var result = sys.TryBloodWarp(boss, bubblegum, grid, gridComp, cow);
            return result;
        }

        var warped = false;
        await server.WaitPost(() =>
        {
            warped = AttemptWarp();
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(warped, Is.True, "the blood warp did not fire with pools near both ends");

            var bubblegum = entMan.GetComponent<BubblegumComponent>(boss);
            Assert.That(bubblegum.Enraged, Is.True, "the warp did not enrage Bubblegum");

            var duration = (bubblegum.EnrageUntil - timing.CurTime).TotalSeconds;
            Assert.That(duration, Is.GreaterThan(3.0),
                "the enrage duration must scale from 3.5 seconds upward, was " + duration);
            Assert.That(duration, Is.LessThanOrEqualTo(7.5),
                "the enrage duration must cap at 7 seconds, was " + duration);

            Assert.That(entMan.HasComponent<ProjectileImmunityComponent>(boss), Is.True,
                "the enrage did not grant projectile immunity");

            var tint = entMan.GetComponent<RandomSpriteComponent>(boss);
            Assert.That(tint.Selected.TryGetValue("enum.DamageStateVisualLayers.Base", out var entry), Is.True,
                "the enrage applied no red tint");
            Assert.That(entry.Color, Is.EqualTo(bubblegum.EnrageTint),
                "the enrage tint is not the red one");
        });

        // Second warp while enraged must not extend the enrage (TG enrage is not refreshed).
        await server.WaitAssertion(() =>
        {
            var bubblegum = entMan.GetComponent<BubblegumComponent>(boss);
            var enrageUntil = bubblegum.EnrageUntil;
            AttemptWarp();
            Assert.That(bubblegum.EnrageUntil, Is.EqualTo(enrageUntil),
                "a warp during the enrage must not refresh the enrage timer");
        });

        // The enrage duration is anger-scaled, so poll until it ends; the cooldown
        // window is armed right when it does.
        var waited = 0;
        while (waited < 1500 && entMan.GetComponent<BubblegumComponent>(boss).Enraged)
        {
            await server.WaitRunTicks(60);
            waited += 60;
        }

        await server.WaitAssertion(() =>
        {
            var bubblegum = entMan.GetComponent<BubblegumComponent>(boss);
            Assert.That(bubblegum.Enraged, Is.False, "the enrage never ended");
            Assert.That(entMan.HasComponent<RandomSpriteComponent>(boss), Is.False,
                "the red tint was not removed when the enrage ended");
            Assert.That(bubblegum.EnrageCooldownUntil, Is.GreaterThan(timing.CurTime),
                "the re-enrage cooldown was not armed when the enrage ended");

            var before = bubblegum.EnrageCooldownUntil;
            var warped = AttemptWarp();
            if (warped)
            {
                Assert.That(bubblegum.Enraged, Is.False,
                    "Bubblegum enrages again while the cooldown is still running");
                Assert.That(bubblegum.EnrageCooldownUntil, Is.EqualTo(before),
                    "a warp during the cooldown changed the cooldown window");
            }
        });

        await pair.CleanReturnAsync();
    }
}
