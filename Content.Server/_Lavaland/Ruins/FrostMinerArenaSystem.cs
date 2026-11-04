// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;

namespace Content.Server._Lavaland.Ruins;

/// <summary>
/// Builds the demonic frost miner arena landmark: a pristine stone arena ringed by light prisms,
/// with the boss in the middle. The chest is not put here, it drops only when the boss dies.
/// </summary>
public sealed class FrostMinerArenaSystem : EntitySystem
{
    private const string StoneTile = "LavalandStoneTilePristine";
    private const string Prism = "LavalandFrostMinerPrism";
    private const string Boss = "LavalandBossDemonicFrostMiner";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FrostMinerArenaSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
    }

    private void OnSpawnerMapInit(Entity<FrostMinerArenaSpawnerComponent> ent, ref MapInitEvent args)
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

        // TG's arena is lit by four prisms that change color when the miner enrages.
        for (var x = -1; x <= 1; x += 2)
        {
            for (var y = -1; y <= 1; y += 2)
            {
                Spawn(Prism, baseCoords.Offset(new Vector2(x * comp.PrismOffset, y * comp.PrismOffset)));
            }
        }

        Spawn(Boss, baseCoords);
    }
}

[RegisterComponent]
public sealed partial class FrostMinerArenaSpawnerComponent : Component
{
    /// <summary>
    /// Half-size of the arena patch, in tiles.
    /// </summary>
    [DataField]
    public int ArenaRadius = 5;

    /// <summary>
    /// Distance of the corner prisms from the center, in tiles.
    /// </summary>
    [DataField]
    public int PrismOffset = 3;
}
