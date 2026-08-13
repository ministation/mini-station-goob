// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.MartialArts;
using Content.Goobstation.Shared.MartialArts.Components;
using Content.Goobstation.Shared.MartialArts.Events;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Damage.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Speech.Muting;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;

namespace Content.Goobstation.Shared.MartialArts;

public abstract partial class SharedMartialArtsSystem
{
    private void InitializeTwilight()
    {
        SubscribeLocalEvent<CanPerformComboComponent, TwilightSlamPerformedEvent>(OnTwilightSlam);
        SubscribeLocalEvent<CanPerformComboComponent, TwilightKickPerformedEvent>(OnTwilightKick);
        SubscribeLocalEvent<CanPerformComboComponent, TwilightRestrainPerformedEvent>(OnTwilightRestrain);
        SubscribeLocalEvent<CanPerformComboComponent, TwilightPressurePerformedEvent>(OnTwilightPressure);
        SubscribeLocalEvent<CanPerformComboComponent, TwilightConsecutivePerformedEvent>(OnTwilightConsecutive);
        SubscribeLocalEvent<CanPerformComboComponent, TwilightMutePulsePerformedEvent>(OnTwilightMutePulse);
        SubscribeLocalEvent<CanPerformComboComponent, TwilightVoidGripPerformedEvent>(OnTwilightVoidGrip);
        SubscribeLocalEvent<CanPerformComboComponent, TwilightEchoBladePerformedEvent>(OnTwilightEchoBlade);
        SubscribeLocalEvent<CanPerformComboComponent, TwilightShadowRipPerformedEvent>(OnTwilightShadowRip);
        SubscribeLocalEvent<CanPerformComboComponent, TwilightResonanceBreakPerformedEvent>(OnTwilightResonanceBreak);

        SubscribeLocalEvent<GrantTwilightComponent, UseInHandEvent>(OnGrantCQCUse);
    }

    #region CQC-parity combos

    private void OnTwilightSlam(Entity<CanPerformComboComponent> ent, ref TwilightSlamPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out var downed)
            || downed)
            return;

        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage, out _);
        _stun.TryKnockdown(target, proto.ParalyzeTime, true, true, proto.DropItems);
        if (TryComp<PullableComponent>(target, out var pullable))
            _pulling.TryStopPull(target, pullable, ent, true);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/genhit3.ogg"), target);
        ComboPopup(ent, target, proto.ID);
        ent.Comp.LastAttacks.Clear();
    }

    private void OnTwilightKick(Entity<CanPerformComboComponent> ent, ref TwilightKickPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out var downed))
            return;

        var mapPos = _transform.GetMapCoordinates(ent).Position;
        var hitPos = _transform.GetMapCoordinates(target).Position;
        var dir = hitPos - mapPos;
        dir *= 1f / dir.Length();

        if (downed)
        {
            if (TryComp<StaminaComponent>(target, out var stamina) && stamina.Critical)
                _newStatus.TryAddStatusEffectDuration(target, "StatusEffectForcedSleeping", out _, TimeSpan.FromSeconds(10));
            DoDamage(ent, target, proto.DamageType, proto.ExtraDamage, out _, TargetBodyPart.Head);
            _stamina.TakeStaminaDamage(target, proto.StaminaDamage * 2 + 5, source: ent);
        }
        else
        {
            _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        }

        if (TryComp<PullableComponent>(target, out var pullable))
            _pulling.TryStopPull(target, pullable, ent, true);
        _grabThrowing.Throw(target, ent, dir, proto.ThrownSpeed, behavior: proto.DropItems);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/genhit2.ogg"), target);
        ComboPopup(ent, target, proto.ID);
        ent.Comp.LastAttacks.Clear();
    }

    private void OnTwilightRestrain(Entity<CanPerformComboComponent> ent, ref TwilightRestrainPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out _))
            return;

        _stun.TryKnockdown(target, proto.ParalyzeTime, true, true, proto.DropItems);
        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        ComboPopup(ent, target, proto.ID);
        ent.Comp.LastAttacks.Clear();
    }

    private void OnTwilightPressure(Entity<CanPerformComboComponent> ent, ref TwilightPressurePerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out _))
            return;

        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        ComboPopup(ent, target, proto.ID);
        ent.Comp.LastAttacks.Clear();

        if (!_hands.TryGetActiveItem(target, out var activeItem))
            return;
        if (!_hands.TryDrop(target, activeItem.Value))
            return;
        if (!_hands.TryGetEmptyHand(ent.Owner, out var emptyHand))
            return;
        if (!_hands.TryPickup(ent, activeItem.Value, emptyHand))
            return;
        _hands.SetActiveHand(ent.Owner, emptyHand);
    }

    private void OnTwilightConsecutive(Entity<CanPerformComboComponent> ent, ref TwilightConsecutivePerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out _))
            return;

        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage, out _);
        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/genhit1.ogg"), target);
        ComboPopup(ent, target, proto.ID);
        ent.Comp.LastAttacks.Clear();
    }

    #endregion

    #region Umbra combos

    private void OnTwilightMutePulse(Entity<CanPerformComboComponent> ent, ref TwilightMutePulsePerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out _))
            return;

        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        _status.TryAddStatusEffect<MutedComponent>(target, "Muted", TimeSpan.FromSeconds(4), true);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/genhit1.ogg"), target);
        ComboPopup(ent, target, proto.ID);
        ent.Comp.LastAttacks.Clear();
    }

    private void OnTwilightVoidGrip(Entity<CanPerformComboComponent> ent, ref TwilightVoidGripPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out _))
            return;

        _movementMod.TryUpdateMovementSpeedModDuration(target,
            MartsGenericSlow,
            TimeSpan.FromSeconds(3),
            0.7f,
            0.7f);

        ComboPopup(ent, target, proto.ID);
        ent.Comp.LastAttacks.Clear();

        if (!_hands.TryGetActiveItem(target, out var activeItem))
            return;
        if (!_hands.TryDrop(target, activeItem.Value))
            return;
        if (!_hands.TryGetEmptyHand(ent.Owner, out var emptyHand))
            return;
        if (!_hands.TryPickup(ent, activeItem.Value, emptyHand))
            return;
        _hands.SetActiveHand(ent.Owner, emptyHand);
    }

    private void OnTwilightEchoBlade(Entity<CanPerformComboComponent> ent, ref TwilightEchoBladePerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out _))
            return;

        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage, out _);

        if (_backstab.TryBackstab(target, ent, Angle.FromDegrees(45d), true, false, false))
            _stun.TryKnockdown(target, TimeSpan.FromSeconds(2), true, true, proto.DropItems);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/bladeslice.ogg"), target);
        ComboPopup(ent, target, proto.ID);
        ent.Comp.LastAttacks.Clear();
    }

    private void OnTwilightShadowRip(Entity<CanPerformComboComponent> ent, ref TwilightShadowRipPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out _))
            return;

        var mapPos = _transform.GetMapCoordinates(ent).Position;
        var hitPos = _transform.GetMapCoordinates(target).Position;
        var dir = hitPos - mapPos;
        var len = dir.Length();
        if (len > 0f)
            dir *= 1f / len;

        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        if (TryComp<PullableComponent>(target, out var pullable))
            _pulling.TryStopPull(target, pullable, ent, true);
        _grabThrowing.Throw(target, ent, dir, proto.ThrownSpeed, behavior: proto.DropItems);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/genhit3.ogg"), target);
        ComboPopup(ent, target, proto.ID);
        ent.Comp.LastAttacks.Clear();
    }

    private void OnTwilightResonanceBreak(Entity<CanPerformComboComponent> ent, ref TwilightResonanceBreakPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out var downed))
            return;

        _stun.TryKnockdown(target, proto.ParalyzeTime, true, true, proto.DropItems);

        if (downed)
        {
            _stamina.TakeStaminaDamage(target, proto.StaminaDamage * 2, source: ent);
            if (TryComp<StaminaComponent>(target, out var stamina) && stamina.Critical)
                _newStatus.TryAddStatusEffectDuration(target, "StatusEffectForcedSleeping", out _, TimeSpan.FromSeconds(8));
        }
        else
        {
            _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/genhit2.ogg"), target);
        ComboPopup(ent, target, proto.ID);
        ent.Comp.LastAttacks.Clear();
    }

    #endregion
}
