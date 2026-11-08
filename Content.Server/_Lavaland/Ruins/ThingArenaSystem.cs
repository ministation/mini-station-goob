// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using Content.Server._Lavaland.BossMechanics;
using Content.Shared._Lavaland.BossMechanics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Server._Lavaland.Ruins;

/// <summary>
/// Builds The Thing arena: a stone lab with the boss, two single use molecular accelerators and
/// a small storage room locked behind a keycard, exactly like the Outpost 31 ruin on TG.
/// </summary>
public sealed class ThingArenaSystem : EntitySystem
{
    private const string StoneTile = "LavalandStoneTilePristine";
    private const string Wall = "WallNecropolisIndestructible";
    private const string Boss = "LavalandBossThing";
    private const string Accelerator = "ThingMolecularAccelerator";
    private const string StorageDoor = "ThingStorageDoor";
    private const string StorageChest = "LavalandCrateThingStorage";

    // Biome decoration that must never stay inside the landmark: lava, rock walls and ash rock.
    private static readonly HashSet<string> ArenaBlockers = new()
    {
        "FloorLavaEntity",
        "WallRockBasalt",
        "FloraRockSolid",
        "FruitCactus",
        "BasaltOne",
        "BasaltTwo",
        "BasaltThree",
        "BasaltFour",
        "BasaltFive",
    };

    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThingArenaSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
    }

    private void OnSpawnerMapInit(Entity<ThingArenaSpawnerComponent> ent, ref MapInitEvent args)
    {
        // No self-delete: the prototype sweep requires every entity to stay deletable.
        var radius = ent.Comp.ArenaRadius;
        var baseCoords = Transform(ent).Coordinates;

        ClearArenaBlockers(baseCoords, radius + 1);

        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
                Spawn(StoneTile, baseCoords.Offset(new Vector2(x, y)));
        }

        // Outer wall ring with a single entrance at the south side.
        var outer = radius + 1;
        for (var x = -outer; x <= outer; x++)
        {
            for (var y = -outer; y <= outer; y++)
            {
                if (MathF.Abs(x) != outer && MathF.Abs(y) != outer)
                    continue;

                if (x == 0 && y == -outer)
                {
                    Spawn(StoneTile, baseCoords.Offset(new Vector2(x, y)));
                    continue;
                }

                Spawn(Wall, baseCoords.Offset(new Vector2(x, y)));
            }
        }

        // Small storage room in the northern part of the arena.
        var storageWallY = radius - 2;
        for (var x = -3; x <= 3; x++)
        {
            if (x == 0)
            {
                var door = Spawn(StorageDoor, baseCoords.Offset(new Vector2(x, storageWallY)));
                _xform.SetLocalRotation(door, Angle.FromDegrees(90));
                continue;
            }

            Spawn(Wall, baseCoords.Offset(new Vector2(x, storageWallY)));
        }

        for (var y = storageWallY + 1; y <= radius; y++)
        {
            Spawn(Wall, baseCoords.Offset(new Vector2(-3, y)));
            Spawn(Wall, baseCoords.Offset(new Vector2(3, y)));
        }

        Spawn(StorageChest, baseCoords.Offset(new Vector2(0, storageWallY + 1)));

        // The boss itself and the two accelerators it is wired to.
        var boss = Spawn(Boss, baseCoords);
        foreach (var x in new[] { -4, 4 })
        {
            var accelerator = Spawn(Accelerator, baseCoords.Offset(new Vector2(x, 0)));
            EnsureComp<ThingAcceleratorComponent>(accelerator).Boss = boss;
        }
    }

    private void ClearArenaBlockers(EntityCoordinates baseCoords, int radius)
    {
        if (!TryComp<MapGridComponent>(baseCoords.EntityId, out var grid))
            return;

        var toDelete = new List<EntityUid>();
        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                var tile = _map.TileIndicesFor(baseCoords.EntityId, grid, baseCoords.Offset(new Vector2(x, y)));
                foreach (var uid in _map.GetAnchoredEntities(baseCoords.EntityId, grid, tile))
                {
                    var meta = MetaData(uid);
                    if (meta.EntityPrototype != null && ArenaBlockers.Contains(meta.EntityPrototype.ID))
                        toDelete.Add(uid);
                }
            }
        }

        foreach (var uid in toDelete)
            QueueDel(uid);
    }
}

[RegisterComponent]
public sealed partial class ThingArenaSpawnerComponent : Component
{
    /// <summary>
    /// Half-size of the arena interior, in tiles.
    /// </summary>
    [DataField]
    public int ArenaRadius = 6;
}
