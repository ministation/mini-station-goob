using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.ERP;

public sealed partial class RemoveCumWallEffectSystem : EntityEffectSystem<TransformComponent, RemoveCumWallEffect>
{
    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<RemoveCumWallEffect> args)
    {
        QueueDel(entity.Owner);
    }
}

[UsedImplicitly]
public sealed partial class RemoveCumWallEffect : EntityEffectBase<RemoveCumWallEffect>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;
}
