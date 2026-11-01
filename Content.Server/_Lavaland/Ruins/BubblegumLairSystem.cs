// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;

namespace Content.Server._Lavaland.Ruins;

/// <summary>
/// Builds the bubblegum lair landmark: a stone arena with the boss
/// and an ancient sarcophagus with its loot.
/// </summary>
public sealed class BubblegumLairSystem : EntitySystem
{
    private const string StoneTile = "LavalandStoneTilePristine";
    private const string Sarcophagus = "LavalandCrateSarcophagus";
    private const string Boss = "LavalandBossBubblegum";

    [Dependency] private readonly IComponentFactory _factory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BubblegumLairSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
    }

    private void OnSpawnerMapInit(Entity<BubblegumLairSpawnerComponent> ent, ref MapInitEvent args)
    {
        // No self-delete: the prototype sweep requires every entity to stay deletable.
        BuildStructure(ent);
    }

    private void BuildStructure(Entity<BubblegumLairSpawnerComponent> ent)
    {
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
        Spawn(Sarcophagus, baseCoords.Offset(comp.SarcophagusOffset));
    }
}

[RegisterComponent]
public sealed partial class BubblegumLairSpawnerComponent : Component
{
    /// <summary>
    /// Half-size of the stone arena patch, in tiles.
    /// </summary>
    [DataField]
    public int ArenaRadius = 5;

    /// <summary>
    /// Offset of the sarcophagus from the spawner, in tiles.
    /// </summary>
    [DataField]
    public Vector2i SarcophagusOffset = (0, 3);
}
