// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.Ninja.Abilities.Snake;
using Content.Shared._Mini.Ninja.Base.AutoDust;
using Content.Shared._Mini.Ninja.Systems;
using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;

namespace Content.Server._Mini.Ninja.Abilities.Snake;

/// <summary>
/// Snake tier 5: the second chance. The first time the wearer would be destroyed (or would die,
/// when auto-dust is off) the module cancels it, heals the body and revives the ninja in place.
/// The module is single use: after the trigger it does nothing.
/// </summary>
public sealed class NinjaSecondChanceSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobThresholdSystem _threshold = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaSecondChanceComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<NinjaSecondChanceComponent, ClothingGotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<NinjaSecondChanceComponent, NinjaSuitPowerChangedEvent>(OnSuitPowerChanged);
        SubscribeLocalEvent<NinjaSecondChanceComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<NinjaSecondChanceMarkerComponent, NinjaAutoDustAttemptEvent>(OnAutoDustAttempt);
        SubscribeLocalEvent<NinjaSecondChanceMarkerComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnEquipped(Entity<NinjaSecondChanceComponent> ent, ref ClothingGotEquippedEvent args)
    {
        EnsureMarker(args.Wearer, ent.Owner);
    }

    private void OnUnequipped(Entity<NinjaSecondChanceComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        if (TryComp<NinjaSecondChanceMarkerComponent>(args.Wearer, out var marker) &&
            (marker.Suit == ent.Owner || marker.Suit == null))
        {
            RemComp<NinjaSecondChanceMarkerComponent>(args.Wearer);
        }
    }

    private void OnSuitPowerChanged(Entity<NinjaSecondChanceComponent> ent, ref NinjaSuitPowerChangedEvent args)
    {
        // The module component is added while the suit is already worn and activated.
        if (!args.Activated)
            return;

        if (!args.Wearer.IsValid())
            return;

        EnsureMarker(args.Wearer, ent.Owner);
    }

    private void OnShutdown(Entity<NinjaSecondChanceComponent> ent, ref ComponentShutdown args)
    {
        var wearer = Transform(ent.Owner).ParentUid;
        if (!wearer.IsValid())
            return;

        if (TryComp<NinjaSecondChanceMarkerComponent>(wearer, out var marker) && marker.Suit == ent.Owner)
            RemComp<NinjaSecondChanceMarkerComponent>(wearer);
    }

    private void EnsureMarker(EntityUid wearer, EntityUid suit)
    {
        var marker = EnsureComp<NinjaSecondChanceMarkerComponent>(wearer);
        if (marker.Suit == suit)
            return;

        marker.Suit = suit;
        Dirty(wearer, marker);
    }

    private bool TryGetSuit(EntityUid body, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out EntityUid? suit,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out NinjaSecondChanceComponent? comp)
    {
        suit = null;
        comp = null;

        if (!TryComp<NinjaSecondChanceMarkerComponent>(body, out var marker) || marker.Suit is not { } suitUid)
            return false;

        if (!Exists(suitUid) || !TryComp<NinjaSecondChanceComponent>(suitUid, out comp))
            return false;

        suit = suitUid;
        return true;
    }

    private void OnAutoDustAttempt(Entity<NinjaSecondChanceMarkerComponent> ent, ref NinjaAutoDustAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryGetSuit(ent.Owner, out var suit, out var comp) || suit.Value != args.Suit)
            return;

        if (comp.Used)
            return;

        if (!TrySecondChance(args.Body, (suit.Value, comp)))
            return;

        args.Cancelled = true;
    }

    private void OnMobStateChanged(Entity<NinjaSecondChanceMarkerComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState is not (MobState.Critical or MobState.Dead))
            return;

        if (!TryGetSuit(ent.Owner, out var suit, out var comp) || comp.Used)
            return;

        if (TryComp<NinjaAutoDustComponent>(suit.Value, out var dust) && dust.Mode != NinjaDustMode.Off)
        {
            // With auto-dust armed the dust attempt event is raised before the body is destroyed,
            // so the second chance is triggered from there. Triggering from the state change as well
            // would heal the body only to be dusted by the stale state change.
            return;
        }

        // Auto-dust is off, so nothing else would destroy the body: revive on actual death only.
        if (args.NewMobState != MobState.Dead)
            return;

        TrySecondChance(ent.Owner, (suit.Value, comp));
    }

    private bool TrySecondChance(EntityUid body, Entity<NinjaSecondChanceComponent> suit)
    {
        if (suit.Comp.Used || TerminatingOrDeleted(body) || !Exists(body))
            return false;

        if (!TryComp<DamageableComponent>(body, out var damageable))
            return false;

        suit.Comp.Used = true;
        Dirty(suit);

        // Exactly like the vanilla rejuvenate: allow the state machine to leave the dead state,
        // wipe the damage and lock the dead state again.
        _threshold.SetAllowRevives(body, true);
        _damageable.ClearAllDamage((body, damageable));
        _threshold.SetAllowRevives(body, false);

        // Some sources of death (for example a destroyed or missing brain) can not be reverted by healing.
        // In that case the second chance is not wasted and the body is left to the other systems.
        if (_mobState.IsDead(body))
        {
            suit.Comp.Used = false;
            Dirty(suit);
            return false;
        }

        _popup.PopupEntity(Loc.GetString("ninja-second-chance-used"), body, body, PopupType.Large);
        return true;
    }
}