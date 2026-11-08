// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;

namespace Content.Server._Lavaland.Ruins;

/// <summary>
/// Builds the TG lavaland sliding puzzle room: a 3x3 patch of necropolis
/// stone with eight puzzle pillars and the puzzling chest in the free slot.
/// The sliding minigame itself is not ported, so the chest is offered at once.
/// </summary>
public sealed class PuzzleRoomSystem : EntitySystem
{
    private const string Floor = "LavalandStoneTileBurnt";
    private const string Pillar = "LavalandPuzzlePillar";
    private const string Chest = "LavalandPuzzleCrate";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PuzzleRoomSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
    }

    private void OnSpawnerMapInit(Entity<PuzzleRoomSpawnerComponent> ent, ref MapInitEvent args)
    {
        // No self-delete: the prototype sweep requires every entity to stay deletable.
        var baseCoords = Transform(ent).Coordinates;

        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                var cell = baseCoords.Offset(new Vector2(x, y));
                Spawn(Floor, cell);

                if (x == 0 && y == 0)
                {
                    Spawn(Chest, cell);
                    continue;
                }

                Spawn(Pillar, cell);
            }
        }
    }
}

[RegisterComponent]
public sealed partial class PuzzleRoomSpawnerComponent : Component;
