// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.Ninja.Abilities.Steel;
using Content.Shared._Mini.Ninja.Systems;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Ninja.Components;
using Content.Shared.Ninja.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Mini.Ninja.Abilities.Steel;

/// <summary>
/// Steel tier 4: the energy net emitter. Straps a living target to a static net that cannot be taken off
/// and breaks only by damage or after its lifetime expires.
/// </summary>
public sealed class NinjaEnergyNetSystem : EntitySystem
{
    [Dependency] private readonly NinjaSuitPowerSystem _suitPower = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedNinjaSuitSystem _ninjaSuit = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaEnergyNetGunComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<NinjaEnergyNetComponent, UnstrapAttemptEvent>(OnUnstrapAttempt);
    }

    private void OnAfterInteract(Entity<NinjaEnergyNetGunComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        var curTime = _timing.CurTime;

        if (ent.Comp.RechargeTime > curTime)
        {
            _popup.PopupEntity(Loc.GetString("ninja-energy-net-recharging"), user, user, PopupType.MediumCaution);
            return;
        }

        if (args.Target is not { } target || !IsValidTarget(user, target))
            return;

        if (!_interaction.InRangeAndAccessible(user, target, ent.Comp.Range))
            return;

        if (!_suitPower.TryUseCharge(user, ent.Comp.Charge))
            return;

        args.Handled = true;

        RevealNinja(user);

        ent.Comp.RechargeTime = curTime + ent.Comp.Cooldown;
        Dirty(ent);

        var netUid = Spawn(ent.Comp.NetProto, Transform(target).Coordinates);
        _audio.PlayPvs(ent.Comp.FireSound, netUid);

        var net = EnsureComp<NinjaEnergyNetComponent>(netUid);
        net.BeamDeleteTime = curTime + net.BeamDuration;
        Dirty(netUid, net);

        var beam = EnsureComp<JointVisualsComponent>(netUid);
        beam.Sprite = ent.Comp.NetBeamSprite;
        beam.Target = user;
        Dirty(netUid, beam);

        _buckle.TryBuckle(target, user, netUid);
    }

    private bool IsValidTarget(EntityUid user, EntityUid target)
    {
        if (target == user)
            return false;

        // Only living mobs can be caught.
        if (!HasComp<MobStateComponent>(target))
            return false;

        return !_mobState.IsDead(target);
    }

    private void RevealNinja(EntityUid user)
    {
        if (!_suitPower.TryGetSuit(user, out var suitUid, out _))
            return;

        if (!TryComp<NinjaSuitComponent>(suitUid.Value, out var suit))
            return;

        _ninjaSuit.RevealNinja((suitUid.Value, suit), user);
    }

    private void OnUnstrapAttempt(Entity<NinjaEnergyNetComponent> ent, ref UnstrapAttemptEvent args)
    {
        // The net holds the target until it is destroyed or despawns.
        args.Cancelled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<NinjaEnergyNetComponent>();

        while (query.MoveNext(out var uid, out var net))
        {
            if (net.BeamDeleteTime > curTime || !HasComp<JointVisualsComponent>(uid))
                continue;

            RemComp<JointVisualsComponent>(uid);
        }
    }
}
