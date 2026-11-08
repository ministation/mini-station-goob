// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Lavaland.Ruins;

/// <summary>
/// Builds the TG lavaland summoning ritual ruin: a circular blood cult altar in a
/// basalt clearing, ringed with cult pylons, robes and the dead.
/// Layout decoded from lavaland_surface_cultaltar.dmm.
/// </summary>
public sealed class CultAltarSystem : EntitySystem
{
    private const string GroundTile = "CultFloor";
    private const string Wall = "WallCult";
    private const string Pylon = "CultPylon";
    private const string Rune = "CultRuneDimensionalRending";
    private const string Sword = "EldritchLongsword";
    private const string Dagger = "RitualDagger";
    private const string Robe = "ClothingOuterRobesCult";
    private const string Shoes = "ClothingShoesCult";
    private const string Corpse = "MobRandomServiceCorpse";
    private const string Blood = "PuddleBlood";

    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly ITileDefinitionManager _tiles = default!;

    // Altar grid, north row first. Decoded palette:
    // b,z cult floor, d cult wall, c,T lava (the planet has no lava tile here, so it stays basalt),
    // a,j,s bare basalt, e remains, f pylon, g blood on cult floor,
    // i remains and blood on cult floor, k remains and cult blade,
    // l remains in cult robes, m remains on cult floor, o the central rune,
    // q remains in bloody cult robes.
    private static readonly string[] Layout =
    {
        "aaaaaazTbaaaaaa",
        "adeTjTbbbTjTeda",
        "aefjdccgccdsfea",
        "aTsccccbccccsTa",
        "ajdccbgbbgccdsa",
        "acccbbidkbbccTa",
        "bbccblbbbmbccbb",
        "cbgbbdbobdbbgbT",
        "bbccbkbbbqbccbb",
        "acccbbldmbbccTa",
        "ajdccbbbbbccdja",
        "aTeccccbcccceTa",
        "ajfedTTgccdefsa",
        "adjTjTbbbTscsda",
        "aaaaaabTbaaaaaa",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CultAltarSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
    }

    private void OnSpawnerMapInit(Entity<CultAltarSpawnerComponent> ent, ref MapInitEvent args)
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
                if (symbol == ' ' || symbol == 'a' || symbol == 'j' || symbol == 's' || symbol == 'c' || symbol == 'T')
                    continue;

                var indices = origin + new Vector2i(col - width / 2, height - 1 - row - height / 2);
                var coords = new EntityCoordinates(gridUid, _map.TileCenterToVector(gridUid, grid, indices));

                if (symbol != '#' && symbol != 'd' && symbol != 'e' && symbol != 'f' && ground != null)
                    _map.SetTile(gridUid, grid, indices, new Tile(ground.TileId));

                SpawnProp(symbol, coords);
            }
        }
    }

    private void SpawnProp(char symbol, EntityCoordinates coords)
    {
        switch (symbol)
        {
            case 'b':
            case 'z':
            case 'g':
                if (symbol == 'g')
                    Spawn(Blood, coords);
                break;
            case 'd':
                Spawn(Wall, coords);
                break;
            case 'e':
                Spawn(Corpse, coords);
                Spawn(Blood, coords);
                break;
            case 'f':
                Spawn(Pylon, coords);
                break;
            case 'i':
                Spawn(Corpse, coords);
                Spawn(Blood, coords);
                break;
            case 'k':
                Spawn(Corpse, coords);
                SpawnLoose(coords, Sword);
                break;
            case 'l':
                Spawn(Corpse, coords);
                Spawn(Robe, coords);
                Spawn(Shoes, coords);
                break;
            case 'm':
                Spawn(Corpse, coords);
                break;
            case 'o':
                Spawn(Rune, coords);
                Spawn(Corpse, coords);
                SpawnLoose(coords, Sword, Dagger);
                break;
            case 'q':
                Spawn(Corpse, coords);
                Spawn(Robe, coords);
                Spawn(Shoes, coords);
                Spawn(Blood, coords);
                break;
        }
    }

    private void SpawnLoose(EntityCoordinates coords, params string[] protos)
    {
        var offset = 0f;
        foreach (var proto in protos)
        {
            Spawn(proto, coords.Offset(new Vector2(offset, 0f)));
            offset += 0.4f;
        }
    }
}

[RegisterComponent]
public sealed partial class CultAltarSpawnerComponent : Component;
