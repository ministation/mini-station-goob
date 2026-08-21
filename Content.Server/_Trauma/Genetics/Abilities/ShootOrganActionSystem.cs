using Content.Server.Polymorph.Systems;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Content.Shared._Trauma.Genetics.Abilities;

namespace Content.Server._Trauma.Genetics.Abilities;

public sealed partial class ShootOrganActionSystem : EntitySystem
{
    [Dependency] private PolymorphSystem _polymorph = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ThrowingSystem _throwing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShootOrganActionComponent, ShootOrganActionEvent>(OnShootOrganAction);
    }

    private void OnShootOrganAction(Entity<ShootOrganActionComponent> ent, ref ShootOrganActionEvent args)
    {
        args.Handled = true;

        var user = args.Performer;
        if (RemoveOrgan(ent, user) is not { } organ)
        {
            _popup.PopupEntity(Loc.GetString("MutationTongueSpike-popup-no-organ", ("organ", ent.Comp.Organ)), user, user);
            return;
        }

        if (_polymorph.PolymorphEntity(organ, ent.Comp.Polymorph) is not { } projectile)
            return;

        var projComp = EnsureComp<ActionProjectileComponent>(projectile);
        projComp.Container = args.Action.Comp.Container;
        Dirty(projectile, projComp);

        _throwing.TryThrow(projectile, args.Target, user: user);
    }

    private EntityUid? RemoveOrgan(Entity<ShootOrganActionComponent> ent, EntityUid user)
    {
        var organName = ent.Comp.Organ;
        foreach (var (organ, organComp) in _body.GetBodyOrgans(user))
        {
            if (!organComp.SlotId.Equals(organName, StringComparison.OrdinalIgnoreCase) &&
                Prototype(organ)?.ID.Contains(organName, StringComparison.OrdinalIgnoreCase) != true)
                continue;

            if (_body.RemoveOrgan(organ, organComp))
                return organ;
        }

        return null;
    }
}
