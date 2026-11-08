// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable annotations

using System.Collections.Generic;
using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Lavaland.Aggression;
using Content.Shared._Lavaland.Damage.Components;
using Content.Shared._Lavaland.EntityShapes.Components;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Events;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

/// <summary>
/// TG parity checks for the hierophant: strong blink blasts, the melee burst,
/// the four-chaser swarm and the death burst.
/// </summary>
[TestFixture]
public sealed class HierophantParityTest
{
    /// <summary>
    /// TG blink blasts deal 30 burn, unlike the usual 10.
    /// </summary>
    [Test]
    public async Task BlinkBlastIsThirtyBurn()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var proto = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var blink = proto.Index<EntityPrototype>("LavalandHierophantBlinkSquare");
            Assert.That(blink.TryGetComponent<DamageSquareComponent>(out var square, entMan.ComponentFactory), Is.True,
                "the blink square lost its DamageSquareComponent");
            Assert.That(square!.Damage.DamageDict.TryGetValue("Heat", out var heat), Is.True,
                "the blink square deals no heat damage");
            Assert.That(heat, Is.EqualTo(FixedPoint2.New(30)),
                "the blink square must deal 30 burn like the TG blink blasts");

            var strongShape = proto.Index<EntityPrototype>("HierophantShapeBlinkStrong");
            Assert.That(strongShape.TryGetComponent<ShapeSpawnerComponent>(out var spawner, entMan.ComponentFactory), Is.True,
                "the strong blink shape lost its ShapeSpawnerComponent");
            Assert.That(spawner!.Spawn, Is.EqualTo(new EntProtoId("LavalandHierophantBlinkSquare")),
                "the strong blink shape must spawn the 30 burn squares");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A melee hit must release a burst of blast squares around the hierophant.
    /// </summary>
    [Test]
    public async Task MeleeHitReleasesBurst()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();

        var testMap = await pair.CreateTestMap();
        EntityUid boss = default;
        EntityUid cow = default;

        await server.WaitPost(() =>
        {
            var tile = testMap.Tile.Tile;
            for (var x = -12; x <= 12; x++)
            {
                for (var y = -12; y <= 12; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
            }

            boss = entMan.SpawnEntity("MobHierophant", testMap.GridCoords);
            cow = entMan.SpawnEntity("MobCow", testMap.GridCoords.Offset(new Vector2(1.5f, 0f)));
        });

        await server.WaitRunTicks(2);

        await server.WaitPost(() =>
        {
            var hit = new MeleeHitEvent(
                new List<EntityUid> { cow }, boss, boss, null, null, entMan.GetComponent<TransformComponent>(boss).Coordinates);
            entMan.EventBus.RaiseLocalEvent(boss, hit);
        });

        // The burst must grow: the expanding spawner releases several rings, not one static ring.
        await server.WaitRunTicks(25);

        await server.WaitAssertion(() =>
        {
            var bossPos = entMan.GetComponent<TransformComponent>(boss).Coordinates.Position;
            Assert.That(CountSquaresNear(entMan, bossPos, 6f), Is.GreaterThan(8),
                "the hierophant melee hit released one static ring instead of an expanding burst");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The chaser swarm action must release four chasers at once.
    /// </summary>
    [Test]
    public async Task ChaserSwarmSpawnsFourChasers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();

        var testMap = await pair.CreateTestMap();
        EntityUid boss = default;
        EntityUid cow = default;

        await server.WaitPost(() =>
        {
            var tile = testMap.Tile.Tile;
            for (var x = -12; x <= 12; x++)
            {
                for (var y = -12; y <= 12; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
            }

            boss = entMan.SpawnEntity("MobHierophant", testMap.GridCoords);
            cow = entMan.SpawnEntity("MobCow", testMap.GridCoords.Offset(new Vector2(6f, 0f)));
            var ev = new SpawnEntityActionEvent
            {
                Spawn = "LavalandHierophantChaser",
                SpawnAtUser = true,
                Amount = 4,
                Target = entMan.GetComponent<TransformComponent>(boss).Coordinates,
                Entity = cow,
                Performer = boss,
            };
            entMan.EventBus.RaiseLocalEvent(boss, ev, broadcast: true);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(CountSquares(entMan, "LavalandHierophantChaser"), Is.EqualTo(4),
                "the chaser swarm must release four chasers, got " + CountSquares(entMan, "LavalandHierophantChaser"));

            // Four chasers on the same tile would walk the same path and stack their squares.
            var tiles = new HashSet<Vector2i>();
            var query = entMan.EntityQueryEnumerator<TransformComponent, MetaDataComponent>();
            while (query.MoveNext(out var xform, out var meta))
            {
                if (meta.EntityPrototype?.ID == "LavalandHierophantChaser")
                    tiles.Add(new Vector2i((int) MathF.Round(xform.Coordinates.Position.X), (int) MathF.Round(xform.Coordinates.Position.Y)));
            }

            Assert.That(tiles.Count, Is.EqualTo(4),
                "the chaser swarm all spawned on the same tile, got " + tiles.Count + " distinct tiles");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Death must release a huge expanding burst (TG radius 10) before the body is deleted.
    /// </summary>
    [Test]
    public async Task DeathReleasesExpandingBurst()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var damage = server.System<DamageableSystem>();
        var proto = server.ResolveDependency<IPrototypeManager>();
        var mapSys = entMan.System<SharedMapSystem>();

        var testMap = await pair.CreateTestMap();
        EntityUid boss = default;
        var deathSpot = Vector2.Zero;

        await server.WaitPost(() =>
        {
            var tile = testMap.Tile.Tile;
            for (var x = -12; x <= 12; x++)
            {
                for (var y = -12; y <= 12; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
            }

            boss = entMan.SpawnEntity("MobHierophant", testMap.GridCoords);
            deathSpot = entMan.GetComponent<TransformComponent>(boss).Coordinates.Position;
            var aggressive = entMan.GetComponent<AggressiveComponent>(boss);
            entMan.System<AggressorsSystem>().AddAggressor(
                (boss, aggressive), entMan.SpawnEntity("MobCow", testMap.GridCoords.Offset(new Vector2(2f, 0f))));

            // MegafaunaGodmode blocks every damage change without a player origin, tests go around it.
            entMan.RemoveComponent<MegafaunaGodmodeComponent>(boss);

            var lethal = new DamageSpecifier(proto.Index<DamageTypePrototype>("Blunt"), 5000);
            damage.TryChangeDamage(boss, lethal, ignoreResistances: true);
        });

        // The TG burst expands for a few seconds; blast squares despawn after one second, so
        // sample the radius twice and require it to grow.
        await server.WaitRunTicks(30);
        var nearRadius = 0f;
        await server.WaitAssertion(() =>
        {
            nearRadius = MaxSquareDistance(entMan, deathSpot);
        });

        await server.WaitRunTicks(60);

        await server.WaitAssertion(() =>
        {
            var deleted = !entMan.EntityExists(boss)
                || entMan.GetComponent<MetaDataComponent>(boss).EntityLifeStage == EntityLifeStage.Deleted;
            Assert.That(deleted, Is.True,
                "the hierophant must still delete itself on death");

            Assert.That(CountSquares(entMan, "LavalandHierophantSquare"), Is.GreaterThan(20),
                "the death burst released no expanding blast squares, got " + CountSquares(entMan, "LavalandHierophantSquare"));

            Assert.That(MaxSquareDistance(entMan, deathSpot), Is.GreaterThan(nearRadius),
                "the death burst rings never grew outwards");
        });

        await pair.CleanReturnAsync();
    }

    private static int CountSquaresNear(IEntityManager entMan, Vector2 center, float radius)
    {
        var squares = 0;
        var query = entMan.EntityQueryEnumerator<TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var xform, out var meta))
        {
            if (meta.EntityPrototype?.ID != "LavalandHierophantSquare")
                continue;
            if ((xform.Coordinates.Position - center).Length() > radius)
                continue;
            squares++;
        }

        return squares;
    }

    private static float MaxSquareDistance(IEntityManager entMan, Vector2 center)
    {
        var max = 0f;
        var query = entMan.EntityQueryEnumerator<TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var xform, out var meta))
        {
            if (meta.EntityPrototype?.ID != "LavalandHierophantSquare")
                continue;
            var distance = (xform.Coordinates.Position - center).Length();
            if (distance > max)
                max = distance;
        }

        return max;
    }

    private static int CountSquares(IEntityManager entMan, string prototype)
    {
        var squares = 0;
        var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var meta))
        {
            if (meta.EntityPrototype?.ID == prototype)
                squares++;
        }

        return squares;
    }
}