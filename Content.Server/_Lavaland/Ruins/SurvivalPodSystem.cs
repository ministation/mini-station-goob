// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Lavaland.Ruins;

/// <summary>
/// Builds the TG survival pod ruin: a sealed plastitanium pod with a dark floor,
/// a glass airlock, the miner's last camp inside and a trail of blood outside.
/// Layout decoded from lavaland_surface_survivalpod.dmm.
/// </summary>
public sealed class SurvivalPodSystem : EntitySystem
{
    private const string GroundTile = "FloorMiningDark";
    private const string Wall = "WallPlastitanium";
    private const string Door = "AirlockGlass";
    private const string Fridge = "SmartFridge";
    private const string Sleeper = "StasisBed";
    private const string Bed = "Bed";
    private const string Bedsheet = "BedsheetBlack";
    private const string Table = "Table";
    private const string Knife = "SurvivalKnife";
    private const string Pickaxe = "Pickaxe";
    private const string Crowbar = "Crowbar";
    private const string MinerCorpse = "MobRandomScienceCorpse";
    private const string ExplorerCorpse = "MobRandomCargoCorpse";
    private const string Blood = "PuddleBlood";

    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly ITileDefinitionManager _tiles = default!;

    // Pod grid, north row first. Decoded palette:
    // # plastitanium wall, . pod floor, + glass airlock, F smart fridge, S sleeper,
    // P pickaxe and blood, B bed and black bedsheet, T table and survival knife,
    // c dead miner and blood, C crowbar, x blood, g dead explorer and blood.
    private static readonly string[] Layout =
    {
        "          ",
        "          ",
        "          ",
        "  #####   ",
        "  #.F.# x ",
        "  #SPB# xg",
        "  #TcC# x ",
        "  ##+## x ",
        "       xx ",
        "       x  ",
        "          ",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurvivalPodSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
    }

    private void OnSpawnerMapInit(Entity<SurvivalPodSpawnerComponent> ent, ref MapInitEvent args)
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
                Spawn(Wall, coords);
                break;
            case '+':
                Spawn(Door, coords);
                break;
            case 'F':
                Spawn(Fridge, coords);
                break;
            case 'S':
                Spawn(Sleeper, coords);
                break;
            case 'P':
                Spawn(Pickaxe, coords);
                Spawn(Blood, coords);
                break;
            case 'B':
                Spawn(Bed, coords);
                Spawn(Bedsheet, coords);
                Spawn(Blood, coords);
                break;
            case 'T':
                Spawn(Table, coords);
                Spawn(Knife, coords);
                break;
            case 'c':
                Spawn(MinerCorpse, coords);
                Spawn(Blood, coords);
                break;
            case 'C':
                Spawn(Crowbar, coords);
                break;
            case 'x':
                Spawn(Blood, coords);
                break;
            case 'g':
                Spawn(ExplorerCorpse, coords);
                Spawn(Blood, coords);
                break;
        }
    }
}

[RegisterComponent]
public sealed partial class SurvivalPodSpawnerComponent : Component;
