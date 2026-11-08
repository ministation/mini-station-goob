// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared._Lavaland.Aggression;
using Content.Shared._Lavaland.BossMechanics;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// Runs the phase loop of The Thing. The fight is split into three phases of
/// <see cref="ThingBossComponent.PhaseHealth"/> health: once a phase is depleted the boss goes
/// down and can only be pushed forward by overloading a molecular accelerator.
/// </summary>
public sealed class ThingBossSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MegafaunaSystem _megafauna = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThingBossComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
        SubscribeLocalEvent<ThingBossComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<ThingBossComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ThingBossComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Invulnerable)
                continue;

            // The boss is down: no attacks, even if it re-acquires a target while healing.
            if (TryComp<MegafaunaAiComponent>(uid, out var ai) && ai.Active)
            {
                ai.Active = false;
                ai.Schedule.Clear();
            }

            if (_timing.CurTime < comp.DownedUntil)
                continue;

            Recover((uid, comp));
        }
    }

    private void OnBeforeDamage(Entity<ThingBossComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (ent.Comp.Invulnerable)
            args.Cancelled = true;
    }

    private void OnDamageChanged(Entity<ThingBossComponent> ent, ref DamageChangedEvent args)
    {
        if (ent.Comp.Invulnerable
            || ent.Comp.Phase >= ent.Comp.MaxPhases
            || !_mobState.IsAlive(ent)
            || !TryComp<DamageableComponent>(ent, out var damageable))
            return;

        var budget = ent.Comp.PhaseHealth * ent.Comp.Phase;
        var total = damageable.TotalDamage;

        if (total > budget)
        {
            // BeforeDamageChangedEvent cannot rescale the hit in this fork, the original spec is applied
            // anyway, so trim the overflow after the fact. The heal re-raises this event and depletes
            // the phase on the second pass.
            _damageable.HealEvenly((ent.Owner, damageable), -(total - budget));
            return;
        }

        if (total >= budget)
            Deplete(ent);
    }

    private void OnMobStateChanged(Entity<ThingBossComponent> ent, ref MobStateChangedEvent args)
    {
        if (_mobState.IsDead(ent))
            _appearance.SetData(ent, ThingVisuals.State, "dead");
    }

    /// <summary>
    /// Enters the invulnerable healing window, called when a phase runs out of health.
    /// </summary>
    private void Deplete(Entity<ThingBossComponent> ent)
    {
        var comp = ent.Comp;
        comp.Invulnerable = true;
        comp.DownedUntil = _timing.CurTime + comp.PhaseInvulnerabilityTime;

        if (TryComp<MegafaunaAiComponent>(ent, out var ai))
        {
            ai.Active = false;
            ai.Schedule.Clear();
        }

        Spawn("EffectSparks", Transform(ent).Coordinates);
        _audio.PlayPvs(comp.DepletedSound, ent);
        _popup.PopupEntity(Loc.GetString("thing-boss-phase-depleted"), ent);
    }

    private void Recover(Entity<ThingBossComponent> ent)
    {
        var comp = ent.Comp;
        comp.Invulnerable = false;

        // Half a phase worth of health, the boss drops back to the previous phase.
        _damageable.HealEvenly(ent.Owner, -comp.RecoverHeal);

        Spawn("EffectSparks", Transform(ent).Coordinates);
        _audio.PlayPvs(comp.RecoveredSound, ent);
        _popup.PopupEntity(Loc.GetString("thing-boss-recovered"), ent);

        TryResume(ent);
    }

    /// <summary>
    /// Pushes the boss into the next phase and out of the invulnerable state.
    /// Called by the molecular accelerator when it is overloaded.
    /// </summary>
    public void Overload(Entity<ThingBossComponent> ent)
    {
        var comp = ent.Comp;
        if (!comp.Invulnerable || comp.Phase >= comp.MaxPhases)
            return;

        comp.Invulnerable = false;
        comp.Phase++;
        _appearance.SetData(ent, ThingVisuals.State, "p" + comp.Phase);

        Spawn("EffectSparks", Transform(ent).Coordinates);
        _audio.PlayPvs(comp.OverloadSound, ent);
        _popup.PopupEntity(Loc.GetString("thing-boss-phase-overloaded"), ent);

        TryResume(ent);
    }

    private void TryResume(Entity<ThingBossComponent> ent)
    {
        if (!TryComp<MegafaunaAiComponent>(ent, out var ai) || ai.Active)
            return;

        if (!TryComp<AggressiveComponent>(ent, out var aggressive) || aggressive.Aggressors.Count == 0)
            return;

        _megafauna.StartupMegafauna((ent, ai));
    }
}