// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Lavaland.Ruins;

/// <summary>
/// Builds the TG lavaland phone booth ruin: a tiny glass kiosk that Nanotrasen left
/// behind to advertise holopads, with two vending machines and a public GPS.
/// Layout decoded from lavaland_surface_phonebooth.dmm.
/// </summary>
public sealed class LavaPhoneboothSystem : EntitySystem
{
    private const string GroundTile = "FloorDark";
    private const string Wall = "WallSolid";
    private const string Window = "ReinforcedWindow";
    private const string SideWindow = "WindowReinforcedDirectional";
    private const string Door = "AirlockGlass";
    private const string Holopad = "Holopad";
    private const string SnackVendor = "VendingMachineSnack";
    private const string CigVendor = "VendingMachineCigs";
    private const string Gps = "HandheldGPSBasic";

    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly ITileDefinitionManager _tiles = default!;

    // Kiosk grid, north row first. Decoded palette:
    // # solid wall, W reinforced window, w directional window on the west edge,
    // + glass airlock, a holopad, s snack vendor with an east-facing window,
    // c cigarette vendor with an east-facing window, G public GPS with a west-facing window.
    private static readonly string[] Layout =
    {
        "   ",
        "#W#",
        "saG",
        "c w",
        "#+#",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavaPhoneboothSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
    }

    private void OnSpawnerMapInit(Entity<LavaPhoneboothSpawnerComponent> ent, ref MapInitEvent args)
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
            case 'W':
                Spawn(Window, coords);
                break;
            case 'w':
                SpawnRotated(SideWindow, coords, 270f);
                break;
            case '+':
                Spawn(Door, coords);
                break;
            case 'a':
                Spawn(Holopad, coords);
                break;
            case 's':
                Spawn(SnackVendor, coords);
                SpawnRotated(SideWindow, coords, 90f);
                break;
            case 'c':
                Spawn(CigVendor, coords);
                SpawnRotated(SideWindow, coords, 90f);
                break;
            case 'G':
                Spawn(Gps, coords);
                SpawnRotated(SideWindow, coords, 270f);
                break;
        }
    }

    private void SpawnRotated(string proto, EntityCoordinates coords, float degrees)
    {
        var uid = Spawn(proto, coords);
        _xform.SetLocalRotation(uid, Angle.FromDegrees(degrees));
    }
}

[RegisterComponent]
public sealed partial class LavaPhoneboothSpawnerComponent : Component;