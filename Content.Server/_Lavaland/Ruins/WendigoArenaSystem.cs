// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;

namespace Content.Server._Lavaland.Ruins;

/// <summary>
/// Builds the wendigo arena landmark: a stone arena with the boss in the middle.
/// The chest is not put here, it drops only when the boss dies.
/// </summary>
public sealed class WendigoArenaSystem : EntitySystem
{
    private const string StoneTile = "LavalandStoneTilePristine";
    private const string Boss = "LavalandBossWendigo";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WendigoArenaSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
    }

    private void OnSpawnerMapInit(Entity<WendigoArenaSpawnerComponent> ent, ref MapInitEvent args)
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
    }
}

[RegisterComponent]
public sealed partial class WendigoArenaSpawnerComponent : Component
{
    /// <summary>
    /// Half-size of the arena patch, in tiles.
    /// </summary>
    [DataField]
    public int ArenaRadius = 7;
}
