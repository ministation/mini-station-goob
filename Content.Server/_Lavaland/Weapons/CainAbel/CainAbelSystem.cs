// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using Content.Server.NPC.HTN;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Weapons.CainAbel;
using Content.Shared.ActionBlocker;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Weapons.CainAbel;

/// <summary>
/// Cain and Abel: combo counter, stored blood wisps, two throw modes and the dagger swing ability.
/// </summary>
public sealed class CainAbelSystem : EntitySystem
{
    private const float WispSpacing = 0.15f;
    private const float SwingDuration = 1.6f;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    /// <summary>TG hexagon of wisp sprites, in pixels converted to tiles.</summary>
    private static readonly Vector2[] WispOffsets =
    {
        new(-18f / 32f, -2f / 32f),
        new(-6f / 32f, -6f / 32f),
        new(6f / 32f, -6f / 32f),
        new(18f / 32f, -2f / 32f),
        new(6f / 32f, 2f / 32f),
        new(-6f / 32f, 2f / 32f),
    };

    private static readonly SoundSpecifier[] SwingSounds =
    {
        new SoundPathSpecifier("/Audio/_Lavaland/Weapons/CainAbel/dagger_slash_1.ogg"),
        new SoundPathSpecifier("/Audio/_Lavaland/Weapons/CainAbel/dagger_slash_2.ogg"),
        new SoundPathSpecifier("/Audio/_Lavaland/Weapons/CainAbel/dagger_slash_3.ogg"),
        new SoundPathSpecifier("/Audio/_Lavaland/Weapons/CainAbel/dagger_slash_4.ogg"),
        new SoundPathSpecifier("/Audio/_Lavaland/Weapons/CainAbel/dagger_slash_5.ogg"),
        new SoundPathSpecifier("/Audio/_Lavaland/Weapons/CainAbel/dagger_slash_6.ogg"),
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CainAbelComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<CainAbelComponent, GetMeleeAttackRateEvent>(OnAttackRate);
        SubscribeLocalEvent<CainAbelComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<CainAbelComponent, BeforeThrowEvent>(OnBeforeThrow);
        SubscribeLocalEvent<CainAbelComponent, GotEquippedHandEvent>(OnEquippedHand);
        SubscribeLocalEvent<CainAbelComponent, GotUnequippedHandEvent>(OnUnequippedHand);
        SubscribeLocalEvent<CainAbelComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<HandsComponent, AttackedEvent>(OnHolderAttacked);
        SubscribeLocalEvent<CainAbelSwingActionEvent>(OnSwing);
        SubscribeLocalEvent<CainAbelReleaseWispsActionEvent>(OnReleaseWisps);
    }

    private void OnMeleeHit(Entity<CainAbelComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        var large = false;
        foreach (var hit in args.HitEntities)
        {
            if (IsLargeMob(hit))
            {
                large = true;
                break;
            }
        }

        ent.Comp.LastTargetLarge = large;

        if (!large)
        {
            // TG: hitting anything small or dead breaks the combo instead of building it.
            if (ent.Comp.Combo > 0)
                SetCombo(ent, 0, args.User);
            return;
        }

        // The hit itself uses the combo count from before this swing.
        var multiplier = MathF.Max(1f, ent.Comp.Combo) * ent.Comp.DamageBoost;
        args.BonusDamage += args.BaseDamage * (multiplier - 1f);

        if (ent.Comp.Combo < ent.Comp.MaxCombo)
            SetCombo(ent, ent.Comp.Combo + 1, args.User);
    }

    private void OnAttackRate(Entity<CainAbelComponent> ent, ref GetMeleeAttackRateEvent args)
    {
        args.Rate = ent.Comp.LastTargetLarge ? ent.Comp.LargeAttackRate : ent.Comp.SmallAttackRate;
    }

    private void OnEquippedHand(Entity<CainAbelComponent> ent, ref GotEquippedHandEvent args)
    {
        ent.Comp.Holder = args.User;
    }

    private void OnUnequippedHand(Entity<CainAbelComponent> ent, ref GotUnequippedHandEvent args)
    {
        ent.Comp.Holder = null;
        SetCombo(ent, 0, null);
    }

    private void OnShutdown(Entity<CainAbelComponent> ent, ref ComponentShutdown args)
    {
        SetCombo(ent, 0, null);
    }

    private void OnHolderAttacked(Entity<HandsComponent> ent, ref AttackedEvent args)
    {
        foreach (var held in _hands.EnumerateHeld(ent.Owner))
        {
            if (TryComp<CainAbelComponent>(held, out var cain) && cain.Combo > 0)
                SetCombo((held, cain), 0, ent.Owner);
        }
    }

    private void OnUseInHand(Entity<CainAbelComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Crystals = !ent.Comp.Crystals;
        DirtyField(ent, ent.Comp, nameof(CainAbelComponent.Crystals));

        var key = ent.Comp.Crystals ? "cain-abel-popup-crystals-on" : "cain-abel-popup-crystals-off";
        _popup.PopupEntity(Loc.GetString(key), ent, args.User);
    }

    private void OnBeforeThrow(Entity<CainAbelComponent> ent, ref BeforeThrowEvent args)
    {
        // TG replaces the item throw entirely: the dagger stays in the hand and fires a projectile.
        args.Cancelled = true;

        var user = args.PlayerUid;
        var comp = ent.Comp;

        if (_timing.CurTime < comp.NextThrow)
        {
            _popup.PopupEntity(Loc.GetString("cain-abel-popup-on-cooldown"), ent, user);
            return;
        }

        if (!_actionBlocker.CanConsciouslyPerformAction(user))
        {
            _popup.PopupEntity(Loc.GetString("cain-abel-popup-unconscious"), ent, user);
            return;
        }

        if (HasOtherHeldItem(user, ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("cain-abel-popup-offhand-busy"), ent, user);
            return;
        }

        if (args.Direction == Vector2.Zero)
            return;

        var projectile = Spawn(comp.Crystals ? comp.CrystalsProjectile : comp.LaunchProjectile, Transform(user).Coordinates);
        _gun.ShootProjectile(projectile, args.Direction, Vector2.Zero, ent.Owner, user, speed: 20f);
        _audio.PlayPvs(comp.ThrowSound, user);

        comp.NextThrow = _timing.CurTime + TimeSpan.FromSeconds(comp.Crystals ? comp.CrystalsCooldown : comp.LaunchCooldown);
        SetCombo(ent, 0, user);
    }

    private void OnSwing(CainAbelSwingActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryFindHeldDagger(args.Performer, out var dagger))
            return;

        if (HasOtherHeldItem(args.Performer, dagger.Owner))
        {
            _popup.PopupEntity(Loc.GetString("cain-abel-popup-offhand-busy"), dagger, args.Performer);
            return;
        }

        args.Handled = true;

        _status.TryAddStatusEffectDuration(args.Performer, dagger.Comp.SwingStatusEffect, TimeSpan.FromSeconds(SwingDuration));

        var slash = Spawn(dagger.Comp.SwingVisual, Transform(args.Performer).Coordinates);
        _transform.SetParent(slash, args.Performer);

        var performer = args.Performer;
        for (var i = 0; i < SwingSounds.Length; i++)
        {
            var sound = SwingSounds[i];
            var delay = TimeSpan.FromSeconds(i * WispSpacing);
            Timer.Spawn(delay, () =>
            {
                if (!TerminatingOrDeleted(performer))
                    _audio.PlayPvs(sound, performer);
            });
        }
    }

    private void OnReleaseWisps(CainAbelReleaseWispsActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryFindHeldDagger(args.Performer, out var dagger))
            return;

        var comp = dagger.Comp;
        if (comp.Combo <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cain-abel-popup-no-wisps"), dagger, args.Performer);
            return;
        }

        var userCoords = _transform.GetMapCoordinates(args.Performer);
        var targetCoords = _transform.ToMapCoordinates(args.Target);
        if (!userCoords.InRange(targetCoords, comp.ThrowRange))
        {
            _popup.PopupEntity(Loc.GetString("cain-abel-popup-too-far"), dagger, args.Performer);
            return;
        }

        args.Handled = true;

        var count = comp.Combo;
        var user = args.Performer;
        for (var i = 0; i < count; i++)
        {
            var delay = TimeSpan.FromSeconds(i * WispSpacing);
            Timer.Spawn(delay, () => FireWisp(dagger, user, targetCoords));
        }
    }

    private void FireWisp(Entity<CainAbelComponent> dagger, EntityUid user, MapCoordinates target)
    {
        if (TerminatingOrDeleted(dagger) || TerminatingOrDeleted(user) || dagger.Comp.Combo <= 0)
            return;

        var from = _transform.GetMapCoordinates(user);
        if (from.MapId != target.MapId)
            return;

        var direction = target.Position - from.Position;
        if (direction == Vector2.Zero)
            direction = new Vector2(1f, 0f);

        var wisp = Spawn(dagger.Comp.WispProjectile, Transform(user).Coordinates);
        _gun.ShootProjectile(wisp, direction, Vector2.Zero, dagger.Owner, user, speed: 25f);

        SetCombo(dagger, dagger.Comp.Combo - 1, user);
    }

    private void SetCombo(Entity<CainAbelComponent> ent, int combo, EntityUid? user)
    {
        combo = Math.Clamp(combo, 0, ent.Comp.MaxCombo);
        ent.Comp.Combo = combo;
        DirtyField(ent, ent.Comp, nameof(CainAbelComponent.Combo));

        while (ent.Comp.Wisps.Count > combo)
        {
            var last = ent.Comp.Wisps[^1];
            ent.Comp.Wisps.RemoveAt(ent.Comp.Wisps.Count - 1);
            if (!TerminatingOrDeleted(last))
                QueueDel(last);
        }

        var holder = user ?? ent.Comp.Holder;
        if (holder is not { } ownerUid || TerminatingOrDeleted(ownerUid))
            return;

        while (ent.Comp.Wisps.Count < combo)
        {
            var index = ent.Comp.Wisps.Count;
            var wisp = Spawn(ent.Comp.WispVisual, Transform(ownerUid).Coordinates);
            _transform.SetParent(wisp, ownerUid);
            _transform.SetLocalPosition(wisp, WispOffsets[index]);
            ent.Comp.Wisps.Add(wisp);
        }
    }

    private bool TryFindHeldDagger(EntityUid user, out Entity<CainAbelComponent> dagger)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (TryComp<CainAbelComponent>(held, out var comp))
            {
                dagger = (held, comp);
                return true;
            }
        }

        dagger = default;
        return false;
    }

    private bool HasOtherHeldItem(EntityUid user, EntityUid dagger)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (held != dagger)
                return true;
        }

        return false;
    }

    private bool IsLargeMob(EntityUid uid)
    {
        if (!HasComp<MobStateComponent>(uid) || !_mobState.IsAlive(uid))
            return false;

        return HasComp<MegafaunaAiComponent>(uid) || HasComp<HTNComponent>(uid);
    }
}
