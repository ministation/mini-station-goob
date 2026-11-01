// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;

namespace Content.Server._Lavaland.Ruins;

/// <summary>
/// Builds the colossus arena landmark: a pristine stone arena with the boss
/// and its chest.
/// </summary>
public sealed class ColossusArenaSystem : EntitySystem
{
    private const string StoneTile = "LavalandStoneTilePristine";
    private const string Chest = "LavalandCrateColossus";
    private const string Boss = "LavalandBossColossus";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ColossusArenaSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
    }

    private void OnSpawnerMapInit(Entity<ColossusArenaSpawnerComponent> ent, ref MapInitEvent args)
    {
        // No self-delete: the prototype sweep requires every entity to stay deletable.
        var comp = ent.Comp;
        var baseCoords = Transform(ent).Coordinates;

        for (var x = -comp.ArenaRadius; x <= comp.ArenaRadius; x++)
        {
            for (var y = -comp.ArenaRadius; y <= comp.ArenaRadius; y++)
            {
                Spawn(StoneTile, baseCoords.Offset(new Vector2(x, y)));
            }
        }

        Spawn(Boss, baseCoords);
        Spawn(Chest, baseCoords.Offset(comp.ChestOffset));
    }
}

[RegisterComponent]
public sealed partial class ColossusArenaSpawnerComponent : Component
{
    /// <summary>
    /// Half-size of the arena patch, in tiles.
    /// </summary>
    [DataField]
    public int ArenaRadius = 6;

    /// <summary>
    /// Offset of the chest from the spawner, in tiles.
    /// </summary>
    [DataField]
    public Vector2i ChestOffset = (0, 4);
}
