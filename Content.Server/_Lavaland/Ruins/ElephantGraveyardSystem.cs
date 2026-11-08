// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Lavaland.Ruins;

/// <summary>
/// Builds the TG elephant graveyard ruin: a hollow in the wasteland rock with a
/// necropolis stone platform, rows of miner graves, bone trophies and the
/// archaeology shuttle camp. Layout decoded from lavaland_surface_elephant_graveyard.dmm.
/// </summary>
public sealed class ElephantGraveyardSystem : EntitySystem
{
    private const string GroundTile = "FloorAsteroidSandDug";
    private const string Rock = "WallRockBasalt";
    private const string StoneTile = "LavalandStoneTilePristine";
    private const string RuinWall = "WallNecropolisIndestructible";
    private const string Tombstone = "LavalandGraveTombstone";
    private const string StoneGrave = "CrateStoneGrave";
    private const string FilledGrave = "LavalandCrateGraveFilled";
    private const string WoodenGrave = "CrateWoodenGrave";
    private const string LeadGrave = "LavalandCrateGraveLeadResearcher";
    private const string BonePile = "LavalandBonePile";
    private const string Skull = "LavalandBoneSkull";
    private const string MinerCorpse = "MobRandomCargoCorpse";
    private const string ResearcherCorpse = "MobRandomScienceCorpse";
    private const string Table = "Table";
    private const string Rack = "Rack";
    private const string FilingCabinet = "filingCabinetTall";
    private const string Chair = "ChairOfficeLight";
    private const string Floodlight = "Floodlight";
    private const string Generator = "PortableGeneratorPacman";
    private const string Analyzer = "ResearchDestructiveAnalyzer";
    private const string Bonfire = "Bonfire";
    private const string PaperBin = "PaperBin";
    private const string Medkit = "MedkitO2";
    private const string WaterCooler = "WaterCooler";
    private const string Pickaxe = "Pickaxe";
    private const string Shovel = "Shovel";
    private const string Lantern = "Lantern";
    private const string BoneAxe = "WeaponBoneAxe";
    private const string Bones = "MaterialBones1";
    private const string Blood = "PuddleBlood";

    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly ITileDefinitionManager _tiles = default!;

    // Graveyard grid, north row first. Decoded palette:
    // # wasteland rock, , dig site ground, S necropolis stone tile, n broken necropolis wall,
    // T tombstone, G stone grave, X filled grave, W wooden grave, L lead researcher grave, b bone pile,
    // k bone skull, c miner corpse, r researcher corpse, t table, R rack, f filing cabinet,
    // C chair, F floodlight, P portable generator, D destructive analyzer, o bonfire,
    // p paper bin, m medkit, u water cooler, i pickaxe, s shovel, l lantern,
    // a bone axe, B loose bones, w blood.
    private static readonly string[] Layout =
    {
        "#########,#########",
        "#,,,,,,,,,,,,,,,,,#",
        "#,,,,,,,,w,,,,,,,,#",
        "#,,,,,,,c,,,,,,,,,#",
        "#,TG,X,,,,c,,,,,,,#",
        "#,,,,,,,b,,,,ttR,,#",
        "#,TG,X,,,,,,,CfF,,#",
        "#,,,,,,nSSSn,PDp,,#",
        "#,TG,X,SSbSS,muo,,#",
        "#,,,,,,SkkBS,,r,,,#",
        "#,TG,X,SSbSS,,,,,,#",
        "#,,,,,knSSSn,,is,,#",
        "#,TG,G,,,,,,,,la,,#",
        "#,TW,L,,,,,,,,,,,,#",
        "#,,,,,,,,,,,,,,,,,#",
        "#,,,,,,k,,,b,,,,,,#",
        "#,,,B,B,B,B,B,,,,,#",
        "#,,,,,,,,,,,,,,,,,#",
        "#########,#########",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ElephantGraveyardSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
    }

    private void OnSpawnerMapInit(Entity<ElephantGraveyardSpawnerComponent> ent, ref MapInitEvent args)
    {
        // No self-delete: the prototype sweep requires every entity to stay deletable.
        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        if (!_xform.TryGetGridTilePosition((ent.Owner, xform), out var origin, grid))
            return;

        _tiles.TryGetDefinition(GroundTile, out var ground);
        var height = Layout.Length;

        for (var row = 0; row < height; row++)
        {
            var line = Layout[row];
            var width = line.Length;
            for (var col = 0; col < width; col++)
            {
                var symbol = line[col];
                if (symbol == ' ')
                    continue;

                var indices = origin + new Vector2i(col - width / 2, height - 1 - row - height / 2);
                var coords = new EntityCoordinates(gridUid, _map.TileCenterToVector(gridUid, grid, indices));

                if (symbol != '#' && ground != null)
                    _map.SetTile(gridUid, grid, indices, new Tile(ground.TileId));

                SpawnProp(symbol, coords);
            }
        }
    }

    private void SpawnProp(char symbol, EntityCoordinates coords)
    {
        switch (symbol)
        {
            case '#':
                Spawn(Rock, coords);
                break;
            case 'S':
                Spawn(StoneTile, coords);
                break;
            case 'n':
                Spawn(RuinWall, coords);
                break;
            case 'T':
                Spawn(Tombstone, coords);
                break;
            case 'G':
                Spawn(StoneGrave, coords);
                break;
            case 'X':
                Spawn(FilledGrave, coords);
                break;
            case 'W':
                Spawn(WoodenGrave, coords);
                break;
            case 'L':
                Spawn(LeadGrave, coords);
                break;
            case 'b':
                Spawn(BonePile, coords);
                break;
            case 'k':
                Spawn(Skull, coords);
                break;
            case 'c':
                Spawn(MinerCorpse, coords);
                break;
            case 'r':
                Spawn(ResearcherCorpse, coords);
                break;
            case 't':
                Spawn(Table, coords);
                break;
            case 'R':
                Spawn(Rack, coords);
                break;
            case 'f':
                Spawn(FilingCabinet, coords);
                break;
            case 'C':
                Spawn(Chair, coords);
                break;
            case 'F':
                Spawn(Floodlight, coords);
                break;
            case 'P':
                Spawn(Generator, coords);
                break;
            case 'D':
                Spawn(Analyzer, coords);
                break;
            case 'o':
                Spawn(Bonfire, coords);
                break;
            case 'p':
                Spawn(PaperBin, coords);
                break;
            case 'm':
                Spawn(Medkit, coords);
                break;
            case 'u':
                Spawn(WaterCooler, coords);
                break;
            case 'i':
                Spawn(Pickaxe, coords);
                break;
            case 's':
                Spawn(Shovel, coords);
                break;
            case 'l':
                Spawn(Lantern, coords);
                break;
            case 'a':
                Spawn(BoneAxe, coords);
                break;
            case 'B':
                Spawn(Bones, coords);
                break;
            case 'w':
                Spawn(Blood, coords);
                break;
        }
    }
}

[RegisterComponent]
public sealed partial class ElephantGraveyardSpawnerComponent : Component;
