// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Goobstation.Common.Weapons.Ranged;
using Content.Shared.Actions;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Toggleable;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.TypanWar;

public sealed class TypanWarFriendlyFireSystem : EntitySystem
{
    private static readonly EntProtoId FriendlyFireAction = "ActionTypanWarFriendlyFire";

    [Dependency] private readonly SharedActionsSystem _actions = default!;

    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TypanWarFriendlyFireComponent, ToggleActionEvent>(OnToggle);
        SubscribeLocalEvent<TypanWarFriendlyFireComponent, AmmoShotUserEvent>(OnAmmoShot);
    }

    public void SetFaction(EntityUid uid, TypanWarSide side)
    {
        var faction = EnsureComp<TypanWarFactionComponent>(uid);
        faction.Side = side;
        Dirty(uid, faction);
    }

    public void SetupCombatant(EntityUid uid, TypanWarSide side, bool enabled = true)
    {
        SetFaction(uid, side);

        var ff = EnsureComp<TypanWarFriendlyFireComponent>(uid);
        ff.Enabled = enabled;

        if (ff.ActionEntity == null || !Exists(ff.ActionEntity))
            _actions.AddAction(uid, ref ff.ActionEntity, FriendlyFireAction);

        if (ff.ActionEntity is { } action)
            _actions.SetToggled(action, ff.Enabled);

        Dirty(uid, ff);
    }

    public void RemoveCombatant(EntityUid uid)
    {
        if (TryComp<TypanWarFriendlyFireComponent>(uid, out var ff) && ff.ActionEntity is { } action)
            _actions.RemoveAction(uid, action);

        RemComp<TypanWarFriendlyFireComponent>(uid);
        RemComp<TypanWarFactionComponent>(uid);
    }

    public bool ShouldPassThrough(EntityUid shooter, EntityUid target)
    {
        if (shooter == target)
            return true;

        if (!TryComp<TypanWarFriendlyFireComponent>(shooter, out var ff) || !ff.Enabled)
            return false;

        return IsSameFaction(shooter, target);
    }

    public bool IsSameFaction(EntityUid a, EntityUid b)
    {
        if (!TryComp<TypanWarFactionComponent>(a, out var factionA))
            return false;

        if (!TryComp<TypanWarFactionComponent>(b, out var factionB))
            return false;

        return factionA.Side == factionB.Side;
    }

    private void OnToggle(Entity<TypanWarFriendlyFireComponent> ent, ref ToggleActionEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.ActionEntity is not { } ffAction || args.Action.Owner != ffAction)
            return;

        args.Handled = true;

        ent.Comp.Enabled = !ent.Comp.Enabled;

        _actions.SetToggled(ffAction, ent.Comp.Enabled);

        Dirty(ent);

        var msg = ent.Comp.Enabled
            ? Loc.GetString("typan-war-ff-enabled")
            : Loc.GetString("typan-war-ff-disabled");
        _popup.PopupClient(msg, ent, args.Performer);
    }

    private void OnAmmoShot(Entity<TypanWarFriendlyFireComponent> ent, ref AmmoShotUserEvent args)
    {
        if (!ent.Comp.Enabled || args.FiredProjectiles.Count == 0)
            return;

        if (!TryComp<TypanWarFactionComponent>(ent, out var shooterFaction))
            return;

        var allies = GetAllies(shooterFaction.Side);
        if (allies.Count == 0)
            return;

        foreach (var projectile in args.FiredProjectiles)
        {
            if (!TryComp<ProjectileComponent>(projectile, out var proj))
                continue;

            foreach (var ally in allies)
            {
                if (!proj.IgnoredEntities.Contains(ally))
                    proj.IgnoredEntities.Add(ally);
            }

            Dirty(projectile, proj);
        }
    }

    private List<EntityUid> GetAllies(TypanWarSide side)
    {
        var allies = new List<EntityUid>();
        var query = EntityQueryEnumerator<TypanWarFactionComponent>();
        while (query.MoveNext(out var uid, out var faction))
        {
            if (faction.Side == side)
                allies.Add(uid);
        }

        return allies;
    }
}
