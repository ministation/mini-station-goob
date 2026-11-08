// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Lavaland.Ruins;

/// <summary>
/// Builds the TG strong stone ruin: one very strong boulder standing alone in the
/// wasteland. Layout decoded from lavaland_strong_rock.dmm.
/// </summary>
public sealed class StrongStoneSystem : EntitySystem
{
    private const string Rock = "LavalandStrongRock";

    // The whole ruin is a single rock in the middle of the 3x3 template.
    private static readonly string[] Layout =
    {
        "aaa",
        "aba",
        "aaa",
    };

    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StrongStoneSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
    }

    private void OnSpawnerMapInit(Entity<StrongStoneSpawnerComponent> ent, ref MapInitEvent args)
    {
        // No self-delete: the prototype sweep requires every entity to stay deletable.
        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        if (!_xform.TryGetGridTilePosition((ent.Owner, xform), out var origin, grid))
            return;

        var height = Layout.Length;
        for (var row = 0; row < height; row++)
        {
            var line = Layout[row];
            var width = line.Length;
            for (var col = 0; col < width; col++)
            {
                if (line[col] != 'b')
                    continue;

                var indices = origin + new Vector2i(col - width / 2, height - 1 - row - height / 2);
                Spawn(Rock, new EntityCoordinates(gridUid, _map.TileCenterToVector(gridUid, grid, indices)));
            }
        }
    }
}

[RegisterComponent]
public sealed partial class StrongStoneSpawnerComponent : Component;
