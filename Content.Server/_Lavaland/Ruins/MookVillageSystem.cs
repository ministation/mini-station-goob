// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.Map;

namespace Content.Server._Lavaland.Ruins;

/// <summary>
/// Builds the TG mook village landmark: two ashwood huts, a bonfire, the
/// village totems and the mook tribe that lives around them.
/// </summary>
public sealed class MookVillageSystem : EntitySystem
{
    private const string WoodFloor = "LavalandMookWoodFloor";
    private const string Wall = "WallWood";
    private const string Bonfire = "Bonfire";
    private const string Bed = "Bed";
    private const string Chair = "ChairWood";
    private const string Table = "TableWood";
    private const string Railing = "Railing";
    private const string RailingCorner = "RailingCorner";
    private const string Carpet = "Carpet";
    private const string Cabinet = "CrateWooden";
    private const string Lantern = "FlashlightLantern";
    private const string Candle = "CandleInfinite";
    private const string Totem = "LavalandMookTotem";
    private const string MaterialStand = "LavalandMookMaterialStand";
    private const string FireBlossom = "FloraAshFireBlossom";
    private const string Glowgrowth = "FloraAshGlowgrowth";
    private const string Tree = "FloraTree";
    private const string TreeLarge = "FloraTreeLarge";
    private const string BedsheetNT = "BedsheetNT";
    private const string FishingRod = "FishingRod";
    private const string Banana = "FoodBanana";
    private const string GoliathMeat = "FoodMeatGoliath";
    private const string Hatchet = "HydroponicsToolHatchet";
    private const string ChampionBelt = "ClothingBeltChampion";

    private const string Mook = "MobLavalandMook";
    private const string MookWorker = "MobLavalandMookWorker";
    private const string MookBard = "MobLavalandMookBard";
    private const string MookChief = "MobLavalandMookChief";

    /// <summary>
    /// Half size of the 20x20 TG grid. The marker itself sits at the village center.
    /// </summary>
    private const int HalfSize = 9;

    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly string[] Village =
    {
        "aaaaaaMaaaaaalaaaaaa",
        "aaaaaaVaaaaaaaaaaaaa",
        "aaabaaVaPaaaaaaaaaaa",
        "aaaaaaYaalaaPIahhhhh",
        "atFFFAeaaaaaaaacccGc",
        "aaaaXalIaaaaalaccccc",
        "aaaaaaaZddddHlacOxmc",
        "aaaaPaawQEEQJIahhhhh",
        "aaaaaaawyyWyJaaaaaXa",
        "aapaaaawyyyyJaaaaaaa",
        "aaaaaPaouuuuvlaaPala",
        "aaaaaaIaaaaaaXaaaaaa",
        "lXasXnaaaIaaazlrraaa",
        "alaaarraaaaaahgccgha",
        "aaahcccchaaPahgccgha",
        "aaahcccchaaaahqccgha",
        "aaahgccfhalaahgcmgha",
        "aaahjcckhalaahgccgha",
        "aaahcmcihaaaahgccgha",
        "aaahcccchaaaahgccgha",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MookVillageSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
    }

    private void OnSpawnerMapInit(Entity<MookVillageSpawnerComponent> ent, ref MapInitEvent args)
    {
        // No self-delete: the prototype sweep requires every entity to stay deletable.
        var baseCoords = Transform(ent).Coordinates;

        for (var y = 0; y < Village.Length; y++)
        {
            var row = Village[y];
            for (var x = 0; x < row.Length; x++)
            {
                var cell = baseCoords.Offset(new Vector2(x - HalfSize, HalfSize - y));
                BuildCell(row[x], cell);
            }
        }

        // Two totems flank the TG village landmark cell, west and north of it.
        Spawn(Totem, baseCoords.Offset(new Vector2(-7, -3)));
        Spawn(Totem, baseCoords.Offset(new Vector2(-6, -2)));
    }

    private void BuildCell(char key, EntityCoordinates cell)
    {
        switch (key)
        {
            // 'a' is basalt and 'y' is jungle grass, the planet already has ground there.
            case 'a':
            case 'y':
                break;
            case 'b':
                Spawn(Bonfire, cell);
                break;
            case 'c':
                Spawn(WoodFloor, cell);
                break;
            case 'd':
                SpawnRotated(Railing, cell, 180f);
                break;
            case 'e':
                Spawn(FireBlossom, cell);
                Spawn(Mook, cell);
                break;
            case 'f':
                SpawnRotated(Chair, cell, 180f);
                break;
            case 'g':
                Spawn(Bed, cell);
                break;
            case 'h':
                Spawn(Wall, cell);
                break;
            case 'i':
                Spawn(Chair, cell);
                break;
            case 'j':
                Spawn(Cabinet, cell);
                SpawnLoose(cell, Banana, GoliathMeat);
                break;
            case 'k':
                Spawn(Table, cell);
                Spawn(Candle, cell);
                break;
            case 'l':
                Spawn(FireBlossom, cell);
                break;
            case 'm':
                Spawn(Lantern, cell);
                break;
            case 'n':
                Spawn(FireBlossom, cell);
                Spawn(MookWorker, cell);
                break;
            case 'o':
                Spawn(RailingCorner, cell);
                break;
            case 'p':
                Spawn(MaterialStand, cell);
                break;
            case 'q':
                Spawn(Bed, cell);
                Spawn(FishingRod, cell);
                break;
            case 'r':
                Spawn(Carpet, cell);
                break;
            case 's':
                // The totems are spawned around the marker in OnSpawnerMapInit.
                break;
            case 't':
            case 'A':
            case 'F':
                Spawn(Chair, cell);
                break;
            case 'u':
                Spawn(Railing, cell);
                break;
            case 'v':
                SpawnRotated(RailingCorner, cell, 270f);
                break;
            case 'w':
                SpawnRotated(Railing, cell, 90f);
                break;
            case 'x':
                Spawn(Table, cell);
                break;
            case 'z':
                Spawn(MookChief, cell);
                break;
            case 'E':
                Spawn(Glowgrowth, cell);
                break;
            case 'G':
                Spawn(Bed, cell);
                Spawn(BedsheetNT, cell);
                break;
            case 'H':
                SpawnRotated(RailingCorner, cell, 180f);
                break;
            case 'I':
                Spawn(MookBard, cell);
                break;
            case 'P':
                Spawn(MookWorker, cell);
                break;
            case 'J':
                SpawnRotated(Railing, cell, 270f);
                break;
            case 'M':
            case 'V':
            case 'Y':
                SpawnRotated(Chair, cell, 90f);
                break;
            case 'O':
                Spawn(Table, cell);
                SpawnLoose(cell, Hatchet, ChampionBelt);
                break;
            case 'Q':
                Spawn(TreeLarge, cell);
                break;
            case 'W':
                Spawn(Tree, cell);
                break;
            case 'X':
                Spawn(Lantern, cell);
                break;
            case 'Z':
                SpawnRotated(RailingCorner, cell, 90f);
                break;
        }
    }

    private void SpawnRotated(string proto, EntityCoordinates coords, float degrees)
    {
        var uid = Spawn(proto, coords);
        _transform.SetLocalRotation(uid, Angle.FromDegrees(degrees));
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
public sealed partial class MookVillageSpawnerComponent : Component;
