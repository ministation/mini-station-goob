// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using Robust.Shared.Map;

namespace Content.Server._Lavaland.Ruins;

/// <summary>
/// Builds the TG crash site ruin: an escape pod torn open on the basalt, a second
/// crushed pod, scattered survival gear, hot springs and the local fauna.
/// Layout is decoded from lavaland_surface_crashsite.dmm.
/// </summary>
public sealed class CrashsiteSystem : EntitySystem
{
    private const string PodWall = "WallPlastitaniumIndestructible";
    private const string Wall = "WallPlastitanium";
    private const string PodAirlock = "AirlockGlassShuttle";
    private const string Airlock = "AirlockShuttle";
    private const string Thruster = "Thruster";
    private const string Window = "ShuttleWindow";
    private const string Girder = "Girder";
    private const string HotSpring = "FloorWaterEntity";
    private const string Stump = "FloraTreeStump";
    private const string Bonfire = "Bonfire";
    private const string Scrap = "SheetSteel1";
    private const string Chair = "Chair";
    private const string PilotSeat = "ChairPilotSeat";
    private const string Rack = "Rack";
    private const string Bed = "Bed";
    private const string Bedsheet = "BedsheetBlack";
    private const string Table = "Table";
    private const string Fridge = "SmartFridge";
    private const string Suit = "ClothingOuterSuitEmergency";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CrashsiteSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
    }

    private void OnSpawnerMapInit(Entity<CrashsiteSpawnerComponent> ent, ref MapInitEvent args)
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
    }

    private void SpawnTile(char tile, EntityCoordinates coords)
    {
        switch (tile)
        {
            case 'N':
                Spawn(PodWall, coords);
                break;
            case 'E':
                Spawn(Wall, coords);
                break;
            case 'm':
                Spawn(PodAirlock, coords);
                break;
            case 'b':
                Spawn(Airlock, coords);
                break;
            case 'Y':
                Spawn(Thruster, coords);
                break;
            case 'O':
                Spawn(Window, coords);
                break;
            case 'k':
                Spawn(Girder, coords);
                break;
            case 't':
                Spawn(Stump, coords);
                break;
            case 'u':
                Spawn(Bonfire, coords);
                break;
            case 'l':
                Spawn(HotSpring, coords);
                break;
            case 'v':
                Spawn(Scrap, coords);
                Spawn(Scrap, coords);
                break;
            case 'x':
                Spawn(Chair, coords);
                break;
            case 'e':
                Spawn(PilotSeat, coords);
                break;
            case 'j':
                Spawn(PilotSeat, coords);
                Spawn("BitrunningGearPKAModsDisk", coords);
                break;
            case 'H':
                Spawn(PilotSeat, coords);
                Spawn("RandomServiceCorpseSpawner", coords);
                break;
            case 'y':
                Spawn(PilotSeat, coords);
                Spawn("RandomEngineerCorpseSpawner", coords);
                break;
            case 'F':
                Spawn("RandomCargoCorpseSpawner", coords);
                break;
            case 'P':
                Spawn("SalvageHumanCorpseSpawner", coords);
                break;
            case 'i':
                Spawn("LavalandCorpseGoliath", coords);
                break;
            case 'B':
                Spawn("MobLavalandGoliath", coords);
                break;
            case 'R':
                Spawn("MobLavalandGoliathAncient", coords);
                break;
            case 'M':
                Spawn(Rack, coords);
                Spawn("Pickaxe", coords);
                Spawn("Pickaxe", coords);
                Spawn("Crowbar", coords);
                break;
            case 'p':
                Spawn(Rack, coords);
                Spawn("ToolboxEmergency", coords);
                Spawn("ToolboxEmergency", coords);
                break;
            case 'S':
                Spawn(Bed, coords);
                Spawn(Bedsheet, coords);
                break;
            case 'V':
                Spawn(Table, coords);
                Spawn("FoodMeatGoliathCooked", coords);
                break;
            case 'g':
                Spawn(Rack, coords);
                Spawn(Suit, coords);
                Spawn(Suit, coords);
                break;
            case 'd':
                Spawn(Rack, coords);
                Spawn("LavalandCrashPodCache", coords);
                break;
            case 'J':
                Spawn(Fridge, coords);
                break;
            case 'D':
                Spawn("WeaponProtoKineticAccelerator", coords);
                break;
            case 'L':
                Spawn("ToolboxEmergencyFilled", coords);
                break;
            case 'U':
                Spawn("BaitWormPremium", coords);
                break;
            case 'z':
                Spawn("FishCatalog", coords);
                break;
        }
    }

    // Crash site grid, north row first. Decoded palette:
    // N pod wall, E titanium wall, m survival pod airlock, b titanium airlock, Y shuttle engine,
    // O reinforced shuttle window, k girder, t tree stump, u bonfire, l hot spring, v titanium scrap,
    // x plastic chair, e comfy shuttle chair, j chair + bitrunning PKA disk, H chair + cook corpse,
    // y chair + engineer corpse, F cargo corpse, P miner corpse, i goliath corpse, B goliath,
    // R ancient goliath, M rack with emergency mining gear, p rack with toolboxes, S bed,
    // V table with goliath steak, g rack with EVA suits, d rack + supply cache, J smart fridge,
    // D kinetic accelerator, L fishing toolbox, U premium worm bait, z fish catalog.
    private static readonly string[] Layout =
    {
        ".........................",
        ".........................",
        ".........................",
        ".....NNNNN...............",
        ".....NMpSN...............",
        ".....N...m...............",
        ".....N.JVN.B.............",
        ".....NNNNN......YEE......",
        ".....F..........beHv.....",
        "............u...YEEE.....",
        "..............t..........",
        ".....lllUL....NNNN.......",
        ".....llll.....NSgdN......",
        ".....lll.z....m...N......",
        ".....ll..R.iD.N.J.N......",
        ".............PNNNNN......",
        "......t......YEEE........",
        "........YbYB.bee.........",
        "........Eek..YEEE........",
        "........EyEv.............",
        "........EOE..............",
        ".........................",
        ".........................",
        ".........................",
        ".........................",
    };
}

[RegisterComponent]
public sealed partial class CrashsiteSpawnerComponent : Component;
