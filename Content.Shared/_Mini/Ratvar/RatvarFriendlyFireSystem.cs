using Content.Shared.Damage;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.RPSX.DarkForces.Ratvar.Righteous.Roles;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Ratvar;

public sealed class RatvarFriendlyFireSystem : EntitySystem
{
    private static readonly ProtoId<NpcFactionPrototype> RatvarFaction = "Ratvar";

    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamageableComponent, BeforeHarmfulActionEvent>(OnBeforeHarmful);
    }

    private void OnBeforeHarmful(Entity<DamageableComponent> ent, ref BeforeHarmfulActionEvent args)
    {
        if (args.Type != HarmfulActionType.Harm)
            return;

        // Self-harm is still allowed (e.g. fist yourself).
        if (args.User == ent.Owner)
            return;

        if (!IsRatvarAlly(args.User, ent))
            return;

        args.Cancel();

        _popup.PopupPredicted(
            Loc.GetString("ratvar-friendly-fire-blocked"),
            args.User,
            args.User,
            PopupType.SmallCaution);
    }

    public bool IsRatvarAlly(EntityUid a, EntityUid b)
    {
        // Do not treat self as an ally — that used to block self-harm for every Damageable entity.
        if (a == b)
            return false;

        if (!IsRatvarMember(a) || !IsRatvarMember(b))
            return false;

        return _npcFaction.IsEntityFriendly(a, b);
    }

    private bool IsRatvarMember(EntityUid uid)
    {
        if (HasComp<RatvarRighteousComponent>(uid))
            return true;

        return _npcFaction.IsMember(uid, RatvarFaction);
    }
}
