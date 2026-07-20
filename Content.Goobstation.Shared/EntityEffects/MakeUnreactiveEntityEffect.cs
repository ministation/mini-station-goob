using Content.Shared.Chemistry.Reaction;
using Content.Shared.EntityEffects;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.EntityEffects;

public sealed partial class MakeUnreactiveEntityEffectSystem
    : EntityEffectSystem<ReactiveComponent, MakeUnreactiveEntityEffect>
{
    private static readonly ProtoId<TagPrototype> TrashTag = "Trash";

    [Dependency] private readonly TagSystem _tags = default!;

    protected override void Effect(Entity<ReactiveComponent> entity, ref EntityEffectEvent<MakeUnreactiveEntityEffect> args)
    {
        // Clear reactions immediately so further reagents do nothing, but defer removing the
        // component so later effects in the same batch (SpawnEntity, CreateRQuantity, etc.) still run.
        entity.Comp.Reactions = null;
        entity.Comp.ReactiveGroups = null;
        RemCompDeferred<ReactiveComponent>(entity.Owner);
        _tags.AddTag(entity.Owner, TrashTag);
    }
}

public sealed partial class MakeUnreactiveEntityEffect : EntityEffectBase<MakeUnreactiveEntityEffect>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;
}
