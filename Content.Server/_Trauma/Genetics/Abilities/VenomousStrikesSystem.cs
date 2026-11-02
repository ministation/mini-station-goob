// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared._Trauma.Genetics.Abilities;

namespace Content.Server._Trauma.Genetics.Abilities;

/// <summary>
/// Drives <see cref="VenomousStrikesComponent"/>.
/// Port of tg's /datum/mutation/venomous_strikes and the /datum/element/venomous element it uses:
/// unarmed hits inject venom into the target and the mob filters that same venom out of its own blood over time.
/// </summary>
public sealed partial class VenomousStrikesSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private ReactiveSystem _reactive = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    /// <summary>
    /// How often venom is filtered out of the mob's own bloodstream, in seconds. tg does this in on_life.
    /// </summary>
    private const float FilterInterval = 1f;

    private float _timer;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VenomousStrikesComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<VenomousStrikesComponent> ent, ref MeleeHitEvent args)
    {
        // MeleeHitEvent also gets raised when the weapon is merely examined, only real hits inject.
        if (!args.IsHit)
            return;

        // The event is raised on the melee weapon, and a mob is only its own weapon for unarmed attacks.
        // So this never triggers for hits with held weapons, matching tg where only unarmed hits poison.
        // Combat mode is already required by SharedMeleeWeaponSystem.AttemptAttack for any melee hit,
        // which is the equivalent of tg only triggering this on harm intent.
        foreach (var target in args.HitEntities)
        {
            if (target == ent.Owner)
                continue;

            // tg: if(target.stat == DEAD) return
            if (_mobState.IsDead(target))
                continue;

            InjectVenom(target, ent.Comp);
        }
    }

    private void InjectVenom(EntityUid target, VenomousStrikesComponent comp)
    {
        if (comp.AmountPerHit <= 0)
            return;

        // Injection instead of touching, same as tg transferring the venom with the INJECT method.
        if (!_solutions.TryGetInjectableSolution(target, out var injectable, out _))
            return;

        var venom = new Solution();
        venom.AddReagent(comp.Venom, comp.AmountPerHit);

        // React first so the target's reagents proc on the injected dose.
        _reactive.DoEntityReaction(target, venom, ReactionMethod.Injection);
        _solutions.TryAddSolution(injectable.Value, venom);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _timer += frameTime;
        if (_timer < FilterInterval)
            return;
        _timer -= FilterInterval;

        var query = EntityQueryEnumerator<VenomousStrikesComponent, BloodstreamComponent>();
        while (query.MoveNext(out var uid, out var comp, out var blood))
        {
            // tg's on_life does not run for dead mobs.
            if (_mobState.IsDead(uid))
                continue;

            if (!_solutions.TryGetSolution(uid, blood.BloodSolutionName, out var bloodSolution, out _))
                continue;

            // tg: owner.reagents.remove_reagent(venom_type, round(venom_amount * 0.33, CHEMICAL_VOLUME_ROUNDING))
            _solutions.RemoveReagent(bloodSolution.Value, comp.Venom, comp.SelfFilterRate);
        }
    }
}
