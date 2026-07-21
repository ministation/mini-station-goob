using Robust.Shared.Network;

namespace Content.Shared.EntityEffects.Effects.EntitySpawning;

/// <summary>
/// Spawns a number of entities of a given prototype at the coordinates of this entity.
/// Amount is modified by scale.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class SpawnEntityEntityEffectSystem : EntityEffectSystem<TransformComponent, SpawnEntity>
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<SpawnEntity> args)
    {
        var quantity = args.Effect.ShouldScale ? args.Effect.Number * (int) Math.Floor(args.Scale) : args.Effect.Number; // Goobstation - Added ShouldSCale
        var proto = args.Effect.Entity;

        // Spawn at map coordinates so MapInit (e.g. RandomSpawner) runs at the real location.
        // SpawnNextToOrDrop MapInits in nullspace first, which breaks delete-after-spawn spawners.
        var coords = _xform.GetMapCoordinates(entity, xform: entity.Comp);

        if (args.Effect.Predicted)
        {
            for (var i = 0; i < quantity; i++)
            {
                // EntitySystem has no PredictedSpawn(MapCoordinates) proxy — use EntityManager.
                EntityManager.PredictedSpawn(proto, coords);
            }
        }
        else if (_net.IsServer)
        {
            for (var i = 0; i < quantity; i++)
            {
                Spawn(proto, coords);
            }
        }
    }
}

/// <inheritdoc cref="BaseSpawnEntityEntityEffect{T}"/>
public sealed partial class SpawnEntity : BaseSpawnEntityEntityEffect<SpawnEntity>;
