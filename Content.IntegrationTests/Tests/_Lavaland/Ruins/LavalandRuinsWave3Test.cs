// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Collections.Generic;
using Content.Shared.Maps;
using Content.Shared.Mining;
using Content.Shared.Mining.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Ruins;

/// <summary>
/// The wave 3 TG ruins are rebuilt in code on top of their markers. These tests spawn
/// each marker on an empty grid and check that the decoded layout actually appears.
/// </summary>
[TestFixture]
public sealed class LavalandRuinsWave3Test
{
    private sealed record RuinMap(MapId MapId, EntityUid Marker);

    private static async Task<RuinMap> BuildMarker(
        Robust.UnitTesting.RobustIntegrationTest.ServerIntegrationInstance server,
        string markerProto)
    {
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();

        var mapId = MapId.Nullspace;
        var marker = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out mapId);
            var grid = mapSys.CreateGridEntity(mapId);

            // The ruins are built by MapInit on the marker's grid tile; a fresh grid has
            // no tiles, so the marker would not even attach to it. Lay a floor patch
            // first, like the planet tile the marker sits on in a real round.
            var tileId = server.ResolveDependency<ITileDefinitionManager>()["FloorSteel"].TileId;
            for (var x = -5; x <= 5; x++)
            {
                for (var y = -5; y <= 5; y++)
                    mapSys.SetTile(grid, new Vector2i(x, y), new Tile(tileId));
            }

            marker = entMan.SpawnEntity(markerProto, new EntityCoordinates(grid.Owner, 0f, 0f));
        });

        await server.WaitRunTicks(3);
        return new RuinMap(mapId, marker);
    }

    private static Dictionary<string, int> CountPrototypes(
        Robust.UnitTesting.RobustIntegrationTest.ServerIntegrationInstance server,
        MapId mapId,
        HashSet<string> ids)
    {
        var entMan = server.ResolveDependency<IEntityManager>();
        var counts = new Dictionary<string, int>();
        var query = entMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out _, out var meta, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            var id = meta.EntityPrototype?.ID;
            if (id == null || !ids.Contains(id))
                continue;

            counts[id] = counts.GetValueOrDefault(id) + 1;
        }

        return counts;
    }

    private static void AssertCount(Dictionary<string, int> counts, string id, int expected)
    {
        Assert.That(counts.GetValueOrDefault(id), Is.EqualTo(expected), $"unexpected {id} count");
    }

    [Test]
    public async Task PhoneboothBuildsKiosk()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var ruin = await BuildMarker(server, "LavalandLavaPhoneboothSpawner");

        await server.WaitAssertion(() =>
        {
            var counts = CountPrototypes(server, ruin.MapId, new HashSet<string>
            {
                "WallSolid",
                "ReinforcedWindow",
                "WindowReinforcedDirectional",
                "AirlockGlass",
                "Holopad",
                "VendingMachineSnack",
                "VendingMachineCigs",
                "HandheldGPSBasic",
            });

            // The kiosk is 3x5 and the map is empty, so every count is ours.
            AssertCount(counts, "WallSolid", 4);
            AssertCount(counts, "ReinforcedWindow", 1);
            AssertCount(counts, "WindowReinforcedDirectional", 4);
            AssertCount(counts, "AirlockGlass", 1);
            AssertCount(counts, "Holopad", 1);
            AssertCount(counts, "VendingMachineSnack", 1);
            AssertCount(counts, "VendingMachineCigs", 1);
            AssertCount(counts, "HandheldGPSBasic", 1);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CultAltarBuildsRitualSite()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var ruin = await BuildMarker(server, "LavalandCultAltarSpawner");

        await server.WaitAssertion(() =>
        {
            var counts = CountPrototypes(server, ruin.MapId, new HashSet<string>
            {
                "WallCult",
                "CultPylon",
                "CultRuneDimensionalRending",
                "EldritchLongsword",
                "RitualDagger",
                "ClothingOuterRobesCult",
                "ClothingShoesCult",
                "MobRandomServiceCorpse",
            });

            // Decoded from lavaland_surface_cultaltar.dmm: 16 cult wall tiles, four
            // pylons around the ring, one central rune and the dead cultists.
            AssertCount(counts, "WallCult", 16);
            AssertCount(counts, "CultPylon", 4);
            AssertCount(counts, "CultRuneDimensionalRending", 1);
            AssertCount(counts, "EldritchLongsword", 3);
            AssertCount(counts, "RitualDagger", 1);
            AssertCount(counts, "ClothingOuterRobesCult", 3);
            AssertCount(counts, "ClothingShoesCult", 3);
            AssertCount(counts, "MobRandomServiceCorpse", 17);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StrongStoneSpawnsOneRichRock()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var ruin = await BuildMarker(server, "LavalandStrongStoneSpawner");

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            var query = entMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            var count = 0;
            EntityUid? rock = null;
            while (query.MoveNext(out var uid, out var meta, out var xform))
            {
                if (xform.MapID != ruin.MapId || meta.EntityPrototype?.ID != "LavalandStrongRock")
                    continue;

                count++;
                rock = uid;
            }

            Assert.That(count, Is.EqualTo(1), "the 3x3 template holds a single strong rock");
            Assert.That(rock, Is.Not.Null);
            Assert.That(entMan.TryGetComponent<OreVeinComponent>(rock!.Value, out var ore), Is.True);
            Assert.That(ore!.CurrentOre?.Id, Is.EqualTo("OreLavalandStrongRock"));
        });

        await pair.CleanReturnAsync();
    }
}
