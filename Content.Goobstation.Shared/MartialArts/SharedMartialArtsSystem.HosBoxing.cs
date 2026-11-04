// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.MartialArts;
using Content.Goobstation.Shared.MartialArts.Components;
using Content.Goobstation.Shared.MartialArts.Events;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Pulling.Components;
using Robust.Shared.Audio;

namespace Content.Goobstation.Shared.MartialArts;

// Mini - "Кулак Правосудия", бокс ГСБ: 2 приёма по стамине, 2 по урону, 1 по нокдауну.
// Вводы только удар/толчок/захват — тяжёлая атака в комбо не используется.
public abstract partial class SharedMartialArtsSystem
{
    private void InitializeHosBoxing()
    {
        SubscribeLocalEvent<CanPerformComboComponent, HosBoxDoubleJabPerformedEvent>(OnHosBoxDoubleJab);
        SubscribeLocalEvent<CanPerformComboComponent, HosBoxLiverHookPerformedEvent>(OnHosBoxLiverHook);
        SubscribeLocalEvent<CanPerformComboComponent, HosBoxCrossPerformedEvent>(OnHosBoxCross);
        SubscribeLocalEvent<CanPerformComboComponent, HosBoxBodyUppercutPerformedEvent>(OnHosBoxBodyUppercut);
        SubscribeLocalEvent<CanPerformComboComponent, HosBoxKnockoutPerformedEvent>(OnHosBoxKnockout);

        SubscribeLocalEvent<GrantHosBoxingComponent, UseInHandEvent>(OnGrantHosBoxingUse);
    }

    #region Combos

    // Двойка в челюсть: [удар, удар] — стамина в голову.
    private void OnHosBoxDoubleJab(Entity<CanPerformComboComponent> ent, ref HosBoxDoubleJabPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out _))
            return;

        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage, out _, TargetBodyPart.Head);
        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/boxingpunch1.ogg"), target);
        ComboPopup(ent, target, proto.ID);
        ent.Comp.LastAttacks.Clear();
    }

    // Хук в печень: [толчок, удар] — стамина в корпус.
    private void OnHosBoxLiverHook(Entity<CanPerformComboComponent> ent, ref HosBoxLiverHookPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out _))
            return;

        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage, out _, TargetBodyPart.Chest);
        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/boxingpunch2.ogg"), target);
        ComboPopup(ent, target, proto.ID);
        ent.Comp.LastAttacks.Clear();
    }

    // Пробивной кросс: [удар, толчок, удар] — урон в голову.
    private void OnHosBoxCross(Entity<CanPerformComboComponent> ent, ref HosBoxCrossPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out _))
            return;

        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage, out _, TargetBodyPart.Head);
        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/boxingpunch3.ogg"), target);
        ComboPopup(ent, target, proto.ID);
        ent.Comp.LastAttacks.Clear();
    }

    // Апперкот в корпус: [толчок, толчок, удар] — урон в корпус.
    private void OnHosBoxBodyUppercut(Entity<CanPerformComboComponent> ent, ref HosBoxBodyUppercutPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out _))
            return;

        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage, out _, TargetBodyPart.Chest);
        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/genhit3.ogg"), target);
        ComboPopup(ent, target, proto.ID);
        ent.Comp.LastAttacks.Clear();
    }

    // Нокаут: [клинч, удар, удар] — нокдаун в голову.
    private void OnHosBoxKnockout(Entity<CanPerformComboComponent> ent, ref HosBoxKnockoutPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out _))
            return;

        if (TryComp<PullableComponent>(target, out var pullable))
            _pulling.TryStopPull(target, pullable, ent, true);

        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage, out _, TargetBodyPart.Head);
        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        _stun.TryKnockdown(target, proto.ParalyzeTime, true, true, proto.DropItems);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/boxingbell.ogg"), target);
        ComboPopup(ent, target, proto.ID);
        ent.Comp.LastAttacks.Clear();
    }

    #endregion

    private void OnGrantHosBoxingUse(EntityUid ent, GrantHosBoxingComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!_netManager.IsServer)
            return;

        if (!TryGrantMartialArt(args.User, comp))
            return;

        var coords = Transform(args.User).Coordinates;
        _audio.PlayPvs(comp.SoundOnUse, coords);

        if (comp.MultiUse)
            return;

        QueueDel(ent);
        if (comp.SpawnedProto == null)
            return;

        Spawn(comp.SpawnedProto, coords);
    }
}
