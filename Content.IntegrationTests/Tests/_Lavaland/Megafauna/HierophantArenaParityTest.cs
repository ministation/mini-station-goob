// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable annotations

using System.Numerics;
using Content.Server._Lavaland.Megafauna;
using Content.Shared._Lavaland.Aggression;
using Content.Shared._Lavaland.Megafauna;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

/// <summary>
/// TG parity checks for the hierophant arena trap and the club beacon wiring:
/// a fleeing target gets walled in again, and the club carries the beacon pieces.
/// </summary>
[TestFixture]
public sealed class HierophantArenaParityTest
{
    /// <summary>
    /// Once the target is far outside the arena, the trap walls and blasts spawn
    /// around the target's current position (TG arena_trap on every move).
    /// </summary>
    [Test]
    public async Task FugitiveTargetGetsTrapped()
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
            for (var x = -16; x <= 16; x++)
            {
                for (var y = -16; y <= 16; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
            }

            boss = entMan.SpawnEntity("MobHierophant", testMap.GridCoords);
            cow = entMan.SpawnEntity("MobCow", testMap.GridCoords.Offset(new Vector2(13f, 0f)));

            var aggressive = entMan.GetComponent<AggressiveComponent>(boss);
            entMan.System<AggressorsSystem>().AddAggressor((boss, aggressive), cow);
        });

        // The trap polls every half second and fires once the cooldown check passes.
        await server.WaitRunTicks(60);

        await server.WaitAssertion(() =>
        {
            // The cow wanders, so count walls around wherever it is now.
            var nowCowPos = entMan.GetComponent<TransformComponent>(cow).Coordinates.Position;
            var walls = 0;
            var query = entMan.EntityQueryEnumerator<TransformComponent, MetaDataComponent>();
            while (query.MoveNext(out var xform, out var meta))
            {
                if (meta.EntityPrototype?.ID != "WallHierophantArenaTrap")
                    continue;
                if ((xform.Coordinates.Position - nowCowPos).Length() > 16f)
                    continue;
                walls++;
            }

            Assert.That(walls, Is.GreaterThan(30),
                "the fleeing target was not walled in by the arena trap, got " + walls + " walls");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The club and the beacon entity are wired for the TG beacon flow.
    /// </summary>
    [Test]
    public async Task ClubBeaconWiring()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var proto = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var club = proto.Index<EntityPrototype>("LavalandHierophantClub");
            Assert.That(club.TryGetComponent<HierophantClubBeaconComponent>(out var beacon, entMan.ComponentFactory), Is.True,
                "the club lost its beacon component");
            Assert.That(beacon!.TeleportRange, Is.EqualTo(1f),
                "the club teleport range must match the TG range(1)");

            var beaconProto = proto.Index<EntityPrototype>("LavalandHierophantBeacon");
            Assert.That(beaconProto.TryGetComponent<HierophantBeaconComponent>(out _, entMan.ComponentFactory), Is.True,
                "the beacon entity lost its marker component");

            Assert.That(proto.HasIndex<EntityPrototype>("WallHierophantArenaTrap"), Is.True,
                "the trap wall prototype is missing");
        });

        await pair.CleanReturnAsync();
    }
}
