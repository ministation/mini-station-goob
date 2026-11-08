// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;

namespace Content.Server._Lavaland.Ruins;

/// <summary>
/// Builds the ash drake den landmark: a scorched stone arena with the boss.
/// The chest is not put here, it drops only when the boss dies.
/// </summary>
public sealed class DrakeDenSystem : EntitySystem
{
    private const string StoneTileBurnt = "LavalandStoneTileBurnt";
    private const string Boss = "LavalandBossAshDrake";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DrakeDenSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
    }

    private void OnSpawnerMapInit(Entity<DrakeDenSpawnerComponent> ent, ref MapInitEvent args)
    {
        // No self-delete: the prototype sweep requires every entity to stay deletable.
        var comp = ent.Comp;
        var baseCoords = Transform(ent).Coordinates;

        for (var x = -comp.ArenaRadius; x <= comp.ArenaRadius; x++)
        {
            for (var y = -comp.ArenaRadius; y <= comp.ArenaRadius; y++)
            {
                Spawn(StoneTileBurnt, baseCoords.Offset(new Vector2(x, y)));
            }
        }

        Spawn(Boss, baseCoords);
    }
}

[RegisterComponent]
public sealed partial class DrakeDenSpawnerComponent : Component
{
    /// <summary>
    /// Half-size of the scorched arena patch, in tiles.
    /// </summary>
    [DataField]
    public int ArenaRadius = 4;
}
