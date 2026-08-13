// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Common.MartialArts;
using Content.Goobstation.Shared.MartialArts.Components;
using Content.Shared.Mobs.Components;

namespace Content.Goobstation.Shared.MartialArts;

/// <summary>
/// This handles determining if a combo was performed.
/// </summary>
public partial class SharedMartialArtsSystem
{
    private void InitializeCanPerformCombo()
    {
        SubscribeLocalEvent<CanPerformComboComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CanPerformComboComponent, ComboAttackPerformedEvent>(OnAttackPerformed);
        SubscribeLocalEvent<CanPerformComboComponent, ComboBeingPerformedEvent>(OnComboBeingPerformed);
        SubscribeLocalEvent<CanPerformComboComponent, SaveLastAttacksEvent>(OnSave);
        SubscribeLocalEvent<CanPerformComboComponent, ResetLastAttacksEvent>(OnReset);
        SubscribeLocalEvent<CanPerformComboComponent, LoadLastAttacksEvent>(OnLoad);
    }

    private void OnLoad(Entity<CanPerformComboComponent> ent, ref LoadLastAttacksEvent args)
    {
        if (ent.Comp.LastAttacksSaved == null)
            return;

        ent.Comp.LastAttacks = ent.Comp.LastAttacksSaved;
        ent.Comp.LastAttacksSaved = null;

        if (args.Dirty)
            Dirty(ent);
    }

    private void OnReset(Entity<CanPerformComboComponent> ent, ref ResetLastAttacksEvent args)
    {
        ent.Comp.LastAttacks.Clear();

        if (args.Dirty)
            Dirty(ent);
    }

    private void OnSave(Entity<CanPerformComboComponent> ent, ref SaveLastAttacksEvent args)
    {
        ent.Comp.LastAttacksSaved = new(ent.Comp.LastAttacks);
    }

    private void OnMapInit(EntityUid uid, CanPerformComboComponent component, MapInitEvent args)
    {
        foreach (var item in component.RoundstartCombos)
        {
            component.AllowedCombos.Add(_proto.Index(item));
        }
    }

    private void OnAttackPerformed(EntityUid uid, CanPerformComboComponent component, ComboAttackPerformedEvent args)
    {
        if (!HasComp<MobStateComponent>(args.Target))
            return;

        if (component.CurrentTarget != null && args.Target != component.CurrentTarget.Value)
            component.LastAttacks.Clear();

        var afterEv = new AfterComboCheckEvent(uid, args.Target, args.Weapon, args.Type);

        if (args.Weapon != uid)
        {
            component.LastAttacks.Clear();
            RaiseLocalEvent(uid, ref afterEv);
            Dirty(uid, component);
            return;
        }

        component.CurrentTarget = args.Target;
        component.ResetTime = _timing.CurTime + TimeSpan.FromSeconds(5);
        component.LastAttacks.Add(args.Type);
        if (component.LastAttacksLimit >= 0)
        {
            var difference = component.LastAttacks.Count - component.LastAttacksLimit;
            if (difference > 0)
                component.LastAttacks.RemoveRange(0, difference);
        }
        CheckCombo(uid, args.Target, component);
        RaiseLocalEvent(uid, ref afterEv);
        Dirty(uid, component);
    }

    private void CheckCombo(EntityUid uid, EntityUid target, CanPerformComboComponent comp)
    {
        // Prefer the longest matching combo so 3-hit moves are not stolen by 2-hit suffixes
        // (e.g. Disarm→Harm→Harm must not resolve as Harm→Harm).
        ComboPrototype? best = null;

        foreach (var proto in comp.AllowedCombos)
        {
            // If we are targeting ourselves and combo doesn't allow it (or otherwise), then continue
            if (uid == target != proto.PerformOnSelf)
                continue;

            var sum = comp.LastAttacks.Count - proto.AttackTypes.Count;
            if (sum < 0)
                continue;

            var list = comp.LastAttacks.GetRange(sum, proto.AttackTypes.Count);

            if (!list.SequenceEqual(proto.AttackTypes) || proto.ResultEvent == null)
                continue;

            if (best == null || proto.AttackTypes.Count > best.AttackTypes.Count)
                best = proto;
        }

        if (best?.ResultEvent is not { } resultEvent)
            return;

        RaiseLocalEvent(uid, new ComboBeingPerformedEvent(best.ID));
        RaiseLocalEvent(uid, resultEvent);
    }
    private void OnComboBeingPerformed(Entity<CanPerformComboComponent> ent, ref ComboBeingPerformedEvent args)
    {
        ent.Comp.BeingPerformed = args.ProtoId;
    }
}
