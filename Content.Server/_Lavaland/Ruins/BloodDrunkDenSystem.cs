// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.Random;
using Robust.Shared.Random;

namespace Content.Server._Lavaland.Ruins;

/// <summary>
/// Builds the blood-drunk den landmark: a small campsite circle with one
/// randomly picked variant of the Blood-Drunk Miner.
/// </summary>
public sealed class BloodDrunkDenSystem : EntitySystem
{
    private const string StoneTile = "LavalandStoneTilePristine";
    private static readonly string[] Variants =
    {
        "LavalandBossBloodDrunk",
        "LavalandBossBloodDrunkGuidance",
        "LavalandBossBloodDrunkHunter",
    };

    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodDrunkDenSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
    }

    private void OnSpawnerMapInit(Entity<BloodDrunkDenSpawnerComponent> ent, ref MapInitEvent args)
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

        // TG: one of the three variants is picked at random.
        Spawn(_random.Pick(Variants), baseCoords);
    }
}

[RegisterComponent]
public sealed partial class BloodDrunkDenSpawnerComponent : Component
{
    /// <summary>
    /// Half-size of the campsite patch, in tiles.
    /// </summary>
    [DataField]
    public int ArenaRadius = 3;
}
