using Content.Server._Mini.ERT.Components;
using Content.Shared.Implants;

namespace Content.Server._Mini.ERT;

public sealed class ResponceErtImplantSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ResponceErtImplantComponent, ImplantImplantedEvent>(OnImplantImplanted);
    }

    private void OnImplantImplanted(Entity<ResponceErtImplantComponent> ent, ref ImplantImplantedEvent args)
    {
        var implanted = args.Implanted;

        if (TryComp<ResponceErtOnAllowedStateComponent>(implanted, out var imp))
        {
            imp.AllowedStates = ent.Comp.AllowedStates;
            imp.Team = ent.Comp.Team;
            imp.IsReady = true;
        }
        else
        {
            AddComp(implanted, new ResponceErtOnAllowedStateComponent
            {
                AllowedStates = ent.Comp.AllowedStates,
                Team = ent.Comp.Team,
                IsReady = true
            });
        }
    }
}