// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Events;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

/// <summary>
/// The ash drake's lava arena (TG enraged lava swoop): a fire wall ring encases the
/// target and the interior floods with lava each wave. The escape punishment needs a
/// real player session, so it is out of scope here — the ring and the flood are what
/// can be asserted deterministically.
/// </summary>
[TestFixture]
public sealed class DrakeArenaTest
{
    [Test]
    public async Task ArenaEncasesAndFloods()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var tiles = server.ResolveDependency<ITileDefinitionManager>();
        var proto = server.ResolveDependency<IPrototypeManager>();

        Entity<DrakeArenaComponent> drake = default;
        Entity<MapGridComponent> grid = default;
        var floorTile = tiles["FloorSteel"].TileId;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var mapId);
            grid = mapSys.CreateGridEntity(mapId);

            // The arena only walls/floods non-empty tiles: lay out a floor patch.
            for (var x = -5; x <= 5; x++)
            {
                for (var y = -5; y <= 5; y++)
                    mapSys.SetTile(grid, new Vector2i(x, y), new Tile(floorTile));
            }

            var center = mapSys.GridTileToLocal(grid, grid, Vector2i.Zero);
            var drakeUid = entMan.SpawnEntity(null, center);
            drake = (drakeUid, entMan.AddComponent<DrakeArenaComponent>(drakeUid));

            entMan.EventBus.RaiseLocalEvent(drake.Owner, new MegafaunaLavaArenaActionEvent
            {
                Performer = drake.Owner,
                Target = center,
            });
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            Assert.That(drake.Comp.Active, Is.True, "arena should be active after the action");

            var walls = 0;
            var query = entMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var meta, out _))
            {
                if (meta.EntityPrototype?.ID == "WallDrakeArenaTemporary")
                    walls++;
            }

            // The ring of a 7x7 arena: 24 wall entities.
            Assert.That(walls, Is.EqualTo(24), $"expected the full fire ring, got {walls}");
        });

        // Start delay (1s) plus the first wave.
        await server.WaitRunTicks(180);

        await server.WaitAssertion(() =>
        {
            Assert.That(proto.Index<EntityPrototype>("LavalandLavaPool"), Is.Not.Null);

            var pools = 0;
            var query = entMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var meta, out var xform))
            {
                if (meta.EntityPrototype?.ID != "LavalandLavaPool")
                    continue;

                var local = mapSys.LocalToTile(grid, grid, xform.Coordinates);
                if (Math.Max(Math.Abs(local.X), Math.Abs(local.Y)) <= 2)
                    pools++;
            }

            // The 5x5 interior floods; no player session inside means no safe tiles.
            Assert.That(pools, Is.GreaterThanOrEqualTo(20), $"interior should flood, got {pools} pools");
        });

        // Start delay + three waves + the finish check.
        await server.WaitRunTicks(720);

        await server.WaitAssertion(() =>
        {
            Assert.That(drake.Comp.Active, Is.False, "arena should finish after the last wave");
        });

        await pair.CleanReturnAsync();
    }
}
