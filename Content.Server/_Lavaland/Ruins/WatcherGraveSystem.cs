// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using Robust.Shared.Map;

namespace Content.Server._Lavaland.Ruins;

/// <summary>
/// Builds the TG watcher grave ruin: a stone mosaic tomb over a chasm, ringed by
/// indestructible walls, with watcher and goliath remains and a small grave cache.
/// Layout is decoded from lavaland_surface_watcher_grave.dmm.
/// </summary>
public sealed class WatcherGraveSystem : EntitySystem
{
    private const string Wall = "WallNecropolisIndestructible";
    private const string Chasm = "FloorChasmEntity";
    private const string GraveSlab = "LavalandGraveTileSlab";
    private const string GraveCracked = "LavalandGraveTileCracked";
    private const string GraveCenter = "LavalandGraveTileCenter";
    private const string GraveSurrounding = "LavalandGraveTileSurrounding";
    private const string WatcherEgg = "LavalandWatcherEgg";
    private const string WatcherCorpse = "LavalandCorpseWatcher";
    private const string GoliathCorpse = "LavalandCorpseGoliath";
    private const string GraveCache = "LavalandGraveCache";
    private const string FireBlossom = "FloraAshFireBlossom";
    private const string Cactus = "FruitCactus";
    private const string TallMushroom = "FloraAshLargeMushrooms";
    private const string StemMushroom = "FloraAshNumerousMushrooms";
    private const string Rock = "FloraRockSolid";

    // Watcher grave grid, north row first. Decoded palette:
    // W wall, m chasm, S slab, c cracked tile, D mosaic center, e surrounding tile + watcher egg,
    // G slab + goliath corpse, v watcher corpse, o goliath corpse, f fire blossom, x cactus,
    // 1 large mushrooms, 2 numerous mushrooms, R rock.
    private static readonly string[] Layout =
    {
        "...............",
        "......mm.......",
        ".....mmm.......",
        "....mmm.WWW....",
        ".f.WWmx..1WWc..",
        "..cW.c.SSc.WSv.",
        "...WW.cco2WWfc.",
        "...W2cGecRW....",
        "...WWRDc1WW....",
        "....WWWWWW.....",
        "...............",
        "...............",
        "...............",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WatcherGraveSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
    }

    private void OnSpawnerMapInit(Entity<WatcherGraveSpawnerComponent> ent, ref MapInitEvent args)
    {
        // No self-delete: the prototype sweep requires every entity to stay deletable.
        var baseCoords = Transform(ent).Coordinates;
        var height = Layout.Length;

        for (var row = 0; row < height; row++)
        {
            var line = Layout[row];
            for (var col = 0; col < line.Length; col++)
            {
                var tile = line[col];
                if (tile == '.')
                    continue;

                var offset = new Vector2(col - line.Length / 2, height - 1 - row - height / 2);
                SpawnTile(tile, baseCoords.Offset(offset));
            }
        }

        Spawn(GraveCache, baseCoords.Offset(new Vector2(-1, -2)));
    }

    private void SpawnTile(char tile, EntityCoordinates coords)
    {
        switch (tile)
        {
            case 'W':
                Spawn(Wall, coords);
                break;
            case 'm':
                Spawn(Chasm, coords);
                break;
            case 'S':
                Spawn(GraveSlab, coords);
                break;
            case 'c':
                Spawn(GraveCracked, coords);
                break;
            case 'D':
                Spawn(GraveCenter, coords);
                break;
            case 'e':
                Spawn(GraveSurrounding, coords);
                Spawn(WatcherEgg, coords);
                break;
            case 'G':
                Spawn(GraveSlab, coords);
                Spawn(GoliathCorpse, coords);
                break;
            case 'v':
                Spawn(WatcherCorpse, coords);
                break;
            case 'o':
                Spawn(GoliathCorpse, coords);
                break;
            case 'f':
                Spawn(FireBlossom, coords);
                break;
            case 'x':
                Spawn(Cactus, coords);
                break;
            case '1':
                Spawn(TallMushroom, coords);
                break;
            case '2':
                Spawn(StemMushroom, coords);
                break;
            case 'R':
                Spawn(Rock, coords);
                break;
        }
    }
}

[RegisterComponent]
public sealed partial class WatcherGraveSpawnerComponent : Component;
