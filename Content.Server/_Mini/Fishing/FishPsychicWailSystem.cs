// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Server.Stunnable;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Events;
using Content.Shared._Mini.Fishing.Systems;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;

namespace Content.Server._Mini.Fishing;

/// <summary>
/// The babbelfish screeches when it dies. Nearby fish die with it, nearby people get shaken up.
/// </summary>
public sealed class FishPsychicWailSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StunSystem _stun = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly FishSystem _fish = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;

    private readonly HashSet<EntityUid> _targets = new();
    private readonly HashSet<Entity<FishComponent>> _fishTargets = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishPsychicWailComponent, FishStatusChangedEvent>(OnStatusChanged);
    }

    private void OnStatusChanged(Entity<FishPsychicWailComponent> ent, ref FishStatusChangedEvent args)
    {
        if (args.Alive || TerminatingOrDeleted(ent))
            return;

        Wail(ent);
    }

    private void Wail(Entity<FishPsychicWailComponent> ent)
    {
        var coords = Transform(ent).Coordinates;

        _popup.PopupCoordinates(Loc.GetString("fish-psychic-wail"), coords, PopupType.LargeCaution);

        _targets.Clear();
        _lookup.GetEntitiesInRange(coords, ent.Comp.Range, _targets);
        foreach (var target in _targets)
        {
            if (target == ent.Owner || !HasComp<StatusEffectsComponent>(target) || !HasComp<MobStateComponent>(target))
                continue;

            if (!_mobState.IsAlive(target))
                continue;

            _stun.TryKnockdown((target, null), ent.Comp.KnockdownTime, refresh: true);
            _jitter.DoJitter(target, ent.Comp.EffectTime, true);
        }

        _fishTargets.Clear();
        _lookup.GetEntitiesInRange<FishComponent>(coords, ent.Comp.Range, _fishTargets);
        var killed = 0;
        foreach (var fish in _fishTargets)
        {
            if (fish.Owner == ent.Owner || TerminatingOrDeleted(fish.Owner) || !fish.Comp.Alive)
                continue;

            _fish.SetAlive(fish, false);
            killed++;
        }

        if (killed > 0)
            _popup.PopupCoordinates(Loc.GetString("fish-psychic-wail-kills", ("count", killed)), coords, PopupType.LargeCaution);
    }
}
