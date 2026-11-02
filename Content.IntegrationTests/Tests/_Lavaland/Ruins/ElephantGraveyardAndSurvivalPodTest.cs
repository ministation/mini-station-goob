// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Goobstation.Shared.GPS.Components;
using Content.Server._Lavaland.Ruins;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Lavaland.Ruins;

/// <summary>
/// Regression tests for the wave 2 elephant graveyard and survival pod landmarks:
/// the marker spawners must rebuild their layout, loot and beacon on map init.
/// </summary>
[TestFixture]
[TestOf(typeof(SurvivalPodSystem))]
public sealed class ElephantGraveyardAndSurvivalPodTest
{
    private const int TestRadius = 16;

    [Test]
    public async Task SurvivalPodBuildsSealedPodWithCamp()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var tileDefMan = server.ResolveDependency<ITileDefinitionManager>();

        EntityUid marker = default;
        await server.WaitPost(() =>
        {
            FillTiles(mapSys, tileDefMan, testMap.Grid);
            marker = entMan.SpawnEntity("LavalandSurvivalPodSpawner", testMap.GridCoords);
        });
        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var counts = CountPrototypes(entMan, testMap.Grid.Owner);

            Assert.Multiple(() =>
            {
                Assert.That(counts.GetValueOrDefault("WallPlastitanium"), Is.EqualTo(15),
                    "the pod must be ringed by plastitanium walls");
                Assert.That(counts.GetValueOrDefault("AirlockGlass"), Is.EqualTo(1),
                    "the pod must have a single glass airlock");
                Assert.That(counts.GetValueOrDefault("SmartFridge"), Is.EqualTo(1),
                    "the pod must keep its survival fridge");
                Assert.That(counts.GetValueOrDefault("StasisBed"), Is.EqualTo(1),
                    "the pod must keep its sleeper");
                Assert.That(counts.GetValueOrDefault("Bed"), Is.EqualTo(1),
                    "the pod must keep its bed");
                Assert.That(counts.GetValueOrDefault("BedsheetBlack"), Is.EqualTo(1),
                    "the bed must keep its black bedsheet");
                Assert.That(counts.GetValueOrDefault("Table"), Is.EqualTo(1),
                    "the pod must keep its table");
                Assert.That(counts.GetValueOrDefault("SurvivalKnife"), Is.EqualTo(1),
                    "the pod must keep the survival knife");
                Assert.That(counts.GetValueOrDefault("Pickaxe"), Is.EqualTo(1),
                    "the pod must keep the pickaxe");
                Assert.That(counts.GetValueOrDefault("Crowbar"), Is.EqualTo(1),
                    "the pod must keep the crowbar");
                Assert.That(counts.GetValueOrDefault("MobRandomScienceCorpse"), Is.EqualTo(1),
                    "the pod must hold the dead miner");
                Assert.That(counts.GetValueOrDefault("MobRandomCargoCorpse"), Is.EqualTo(1),
                    "the pod must have the dead explorer outside");
                Assert.That(counts.GetValueOrDefault("PuddleBlood"), Is.GreaterThanOrEqualTo(4),
                    "the pod must keep its blood trail");
            });

            Assert.That(mapSys.TryGetTileRef(testMap.Grid.Owner, testMap.Grid.Comp, Vector2i.Zero, out var tileRef),
                Is.True, "the pod interior must have a tile");
            Assert.That(tileDefMan[tileRef.Tile.TypeId].ID, Is.EqualTo("FloorMiningDark"),
                "the pod interior must use the dark pod floor tile");

            // GPS beacon replaces TG's pod computer and keeps the pod findable.
            Assert.That(entMan.TryGetComponent<GPSComponent>(marker, out var gps), Is.True,
                "the survival pod must carry a GPS beacon");
            Assert.Multiple(() =>
            {
                Assert.That(gps!.GpsName, Is.EqualTo("Survival Pod"));
                Assert.That(gps.Enabled, Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ElephantGraveyardBuildsGravesBonesAndCamp()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var tileDefMan = server.ResolveDependency<ITileDefinitionManager>();

        await server.WaitPost(() =>
        {
            FillTiles(mapSys, tileDefMan, testMap.Grid);
            entMan.SpawnEntity("LavalandElephantGraveyardSpawner", testMap.GridCoords);
        });
        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var counts = CountPrototypes(entMan, testMap.Grid.Owner);

            Assert.Multiple(() =>
            {
                Assert.That(counts.GetValueOrDefault("WallRockBasalt"), Is.EqualTo(70),
                    "the graveyard must be hollowed out of wasteland rock");
                Assert.That(counts.GetValueOrDefault("WallNecropolisIndestructible"), Is.EqualTo(4),
                    "the stone platform must keep its broken necropolis walls");
                Assert.That(counts.GetValueOrDefault("LavalandStoneTilePristine"), Is.EqualTo(16),
                    "the stone platform must be paved");
                Assert.That(counts.GetValueOrDefault("LavalandGraveTombstone"), Is.EqualTo(6),
                    "the grave rows must have tombstones");
                Assert.That(counts.GetValueOrDefault("CrateStoneGrave"), Is.EqualTo(6),
                    "the grave rows must have empty stone graves");
                Assert.That(counts.GetValueOrDefault("LavalandCrateGraveFilled"), Is.EqualTo(4),
                    "the grave rows must have filled stone graves");
                Assert.That(counts.GetValueOrDefault("CrateWoodenGrave"), Is.EqualTo(1),
                    "the graveyard must keep the wooden grave");
                Assert.That(counts.GetValueOrDefault("LavalandCrateGraveLeadResearcher"), Is.EqualTo(1),
                    "the lead researcher grave is unique");
                Assert.That(counts.GetValueOrDefault("LavalandBonePile"), Is.EqualTo(4),
                    "the graveyard must be littered with bones");
                Assert.That(counts.GetValueOrDefault("LavalandBoneSkull"), Is.EqualTo(4),
                    "the graveyard must have skulls");
                Assert.That(counts.GetValueOrDefault("MaterialBones1"), Is.EqualTo(6),
                    "loose bones must be scattered around");
                Assert.That(counts.GetValueOrDefault("MobRandomCargoCorpse"), Is.EqualTo(2),
                    "the graveyard must hold the dead miners");
                Assert.That(counts.GetValueOrDefault("MobRandomScienceCorpse"), Is.EqualTo(1),
                    "the graveyard must hold the dead researcher");
                Assert.That(counts.GetValueOrDefault("Floodlight"), Is.EqualTo(1),
                    "the archaeology camp must be lit");
                Assert.That(counts.GetValueOrDefault("PortableGeneratorPacman"), Is.EqualTo(1),
                    "the camp must have a generator");
                Assert.That(counts.GetValueOrDefault("ResearchDestructiveAnalyzer"), Is.EqualTo(1),
                    "the camp must have the portable analyzer");
                Assert.That(counts.GetValueOrDefault("Bonfire"), Is.EqualTo(1),
                    "the camp must have a bonfire");
                Assert.That(counts.GetValueOrDefault("filingCabinetTall"), Is.EqualTo(1),
                    "the camp must have its filing cabinet");
                Assert.That(counts.GetValueOrDefault("Pickaxe"), Is.EqualTo(1),
                    "the dig site must keep a pickaxe");
                Assert.That(counts.GetValueOrDefault("Shovel"), Is.EqualTo(1),
                    "the dig site must keep a shovel");
                Assert.That(counts.GetValueOrDefault("WeaponBoneAxe"), Is.EqualTo(1),
                    "the dig site must keep the bone axe");
            });

            Assert.That(mapSys.TryGetTileRef(testMap.Grid.Owner, testMap.Grid.Comp, Vector2i.Zero, out var tileRef),
                Is.True, "the graveyard must have a tile");
            Assert.That(tileDefMan[tileRef.Tile.TypeId].ID, Is.EqualTo("FloorAsteroidSandDug"),
                "the graveyard must lie on dug asteroid ground");

            // The lead researcher's grave carries the shared puzzle loot table.
            EntityUid leadGrave = default;
            var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out var uid, out var meta))
            {
                if (meta.EntityPrototype?.ID != "LavalandCrateGraveLeadResearcher")
                    continue;

                leadGrave = uid;
                break;
            }

            Assert.That(entMan.EntityExists(leadGrave), Is.True, "the lead researcher grave must spawn");
            var containers = entMan.System<SharedContainerSystem>();
            Assert.That(containers.TryGetContainer(leadGrave, "entity_storage", out var container), Is.True,
                "the lead researcher grave must have storage");
            Assert.That(container!.ContainedEntities, Is.Not.Empty,
                "the lead researcher grave must be filled by LavalandLootPuzzle");

            // The plain filled graves carry the necropolis rare material cache.
            EntityUid filledGrave = default;
            query = entMan.EntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out var uid, out var meta))
            {
                if (meta.EntityPrototype?.ID != "LavalandCrateGraveFilled")
                    continue;

                filledGrave = uid;
                break;
            }

            Assert.That(entMan.EntityExists(filledGrave), Is.True, "filled graves must spawn");
            Assert.That(containers.TryGetContainer(filledGrave, "entity_storage", out var filledContainer), Is.True,
                "filled graves must have storage");
            Assert.That(filledContainer!.ContainedEntities, Is.Not.Empty,
                "filled graves must be filled by LavalandMaterialRare");
        });

        await pair.CleanReturnAsync();
    }

    private static void FillTiles(SharedMapSystem mapSys, ITileDefinitionManager tileDefMan,
        Entity<MapGridComponent> grid)
    {
        var tile = new Tile(tileDefMan["Plating"].TileId);
        for (var x = -TestRadius; x <= TestRadius; x++)
        {
            for (var y = -TestRadius; y <= TestRadius; y++)
            {
                mapSys.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), tile);
            }
        }
    }

    // Only counts landmark pieces placed on the test grid: gear inside corpses and
    // loot inside graves is parented to its container and must not skew the layout check.
    private static Dictionary<string, int> CountPrototypes(IEntityManager entMan, EntityUid gridUid)
    {
        var counts = new Dictionary<string, int>();
        var query = entMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out _, out var meta, out var xform))
        {
            if (meta.EntityPrototype is not { } proto)
                continue;

            if (xform.ParentUid != gridUid)
                continue;

            counts[proto.ID] = counts.GetValueOrDefault(proto.ID) + 1;
        }

        return counts;
    }
}
