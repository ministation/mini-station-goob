// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Goobstation.Shared.EntityEffects;

public sealed partial class CreateRQuantityEntityReactionEffectSystem : EntityEffectSystem<TransformComponent, CreateRQuantityEntityReactionEffect>
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<CreateRQuantityEntityReactionEffect> args)
    {
        var quantity = _random.Next(1, args.Effect.MaxEntities + 1);

        var coords = _transform.GetMapCoordinates(entity, xform: entity.Comp);

        for (var i = 0; i < quantity; i++)
        {
            var uid = EntityManager.SpawnEntity(args.Effect.Entity, coords);
            _transform.AttachToGridOrMap(uid);
        }
    }
}

[DataDefinition]
public sealed partial class CreateRQuantityEntityReactionEffect : EntityEffectBase<CreateRQuantityEntityReactionEffect>
{
    /// <summary>
    ///     What entity to create.
    /// </summary>
    [DataField(required: true, customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string Entity = default!;

    /// <summary>
    ///     What is our maximum allowed entities to be spawned?
    /// </summary>
    [DataField]
    public int MaxEntities = 1;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-create-entity-reaction-effect",
            ("chance", Probability),
            ("entname", prototype.Index<EntityPrototype>(Entity).Name),
            ("amount", MaxEntities));
}
