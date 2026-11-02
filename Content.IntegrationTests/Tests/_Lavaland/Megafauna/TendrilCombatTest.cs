// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

/// <summary>
/// Guards the TG parity tendril behaviors: melee retaliation with an 8 directional lash
/// and infection of the surrounding rock with necropolis walls on destruction.
/// </summary>
[TestFixture]
public sealed class TendrilCombatTest
{
    [Test]
    public async Task TendrilLashesBackWhenHitInMelee()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var centre = testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f));
        var mapSys = server.System<SharedMapSystem>();

        // CreateTestMap makes a single tile grid, the tendril arena and its rock ring need tiles.
        await server.WaitPost(() =>
        {
            for (var x = -4; x <= 4; x++)
            {
                for (var y = -4; y <= 4; y++)
                    mapSys.SetTile(testMap.Grid, new EntityCoordinates(testMap.Grid, x, y), testMap.Tile.Tile);
            }
        });

        EntityUid tendril = default;

        await server.WaitPost(() =>
        {
            tendril = entMan.SpawnEntity("LavalandTendrilGoliath", centre);
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            Assert.That(CountPrototype(entMan, "LavalandTendrilSpike"), Is.Zero, "a dormant tendril should not raise spikes");
        });

        await server.WaitPost(() =>
        {
            var human = entMan.SpawnEntity("MobHuman", centre.Offset(new Vector2(1, 0)));
            entMan.EventBus.RaiseLocalEvent(tendril, new AttackedEvent(human, human, centre), true);
        });

        // Let the lash spawner expand a couple of times, so the spikes cover the 8 directions.
        await server.WaitRunTicks(20);

        await server.WaitAssertion(() =>
        {
            Assert.That(CountPrototype(entMan, "LavalandTendrilSpike"), Is.GreaterThan(5),
                "a melee hit should make the tendril lash out with spikes in all directions");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TendrilInfectsSurroundingRockOnDeath()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var centre = testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f));
        var mapSys = server.System<SharedMapSystem>();

        // CreateTestMap makes a single tile grid, the tendril arena and its rock ring need tiles.
        await server.WaitPost(() =>
        {
            for (var x = -4; x <= 4; x++)
            {
                for (var y = -4; y <= 4; y++)
                    mapSys.SetTile(testMap.Grid, new EntityCoordinates(testMap.Grid, x, y), testMap.Tile.Tile);
            }
        });

        EntityUid tendril = default;

        await server.WaitPost(() =>
        {
            tendril = entMan.SpawnEntity("LavalandTendrilGoliath", centre);

            // Rock ring around the tendril arena, same layout a lavaland tendril sits in.
            for (var x = -2; x <= 2; x++)
            {
                for (var y = -2; y <= 2; y++)
                {
                    if (Math.Abs(x) <= 1 && Math.Abs(y) <= 1)
                        continue;

                    entMan.SpawnEntity("WallRock", centre.Offset(new Vector2(x, y)));
                }
            }
        });

        await server.WaitRunTicks(3);

        await server.WaitPost(() =>
        {
            entMan.System<DamageableSystem>().TryChangeDamage(
                tendril,
                new DamageSpecifier { DamageDict = { { "Blunt", 1000f } } },
                true);
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(tendril), Is.True, "the tendril should be destroyed by the damage");
            Assert.That(CountPrototype(entMan, "WallRock"), Is.Zero, "the rock around the dead tendril should be gone");
            Assert.That(CountPrototype(entMan, "WallNecropolisIndestructible"), Is.GreaterThan(5),
                "the rock around the dead tendril should be replaced with necropolis walls");
        });

        await pair.CleanReturnAsync();
    }

    private static int CountPrototype(IEntityManager entMan, string protoId)
    {
        var count = 0;
        var query = entMan.AllEntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var meta))
        {
            if (meta.EntityPrototype?.ID == protoId)
                count++;
        }

        return count;
    }
}