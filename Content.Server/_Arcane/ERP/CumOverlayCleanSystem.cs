using Content.Shared._Arcane.ERP;
using Content.Shared.Rejuvenate;

namespace Content.Server._Arcane.ERP;

public sealed class CumOverlayCleanSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CumOverlayComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private void OnRejuvenate(Entity<CumOverlayComponent> ent, ref RejuvenateEvent args)
    {
        RemComp<CumOverlayComponent>(ent);
    }
}
