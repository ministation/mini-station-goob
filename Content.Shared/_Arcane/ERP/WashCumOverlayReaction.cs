using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.ERP;

public sealed partial class WashCumOverlayReactionSystem : EntityEffectSystem<CumOverlayComponent, WashCumOverlayReaction>
{
    protected override void Effect(Entity<CumOverlayComponent> entity, ref EntityEffectEvent<WashCumOverlayReaction> args)
    {
        RemComp<CumOverlayComponent>(entity.Owner);
    }
}

[UsedImplicitly]
public sealed partial class WashCumOverlayReaction : EntityEffectBase<WashCumOverlayReaction>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;
}
