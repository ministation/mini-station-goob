// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Systems;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.StatusEffectNew;
using Content.Shared._Trauma.Genetics.Abilities;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Trauma.Genetics.Abilities;

/// <summary>
/// Applies the symptoms of the <see cref="ChemicalAllergyComponent"/> mutation.
/// Mirrors <c>/datum/mutation/chemical_allergy/on_life</c> from /tg/station.
/// </summary>
public sealed partial class ChemicalAllergySystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedStutteringSystem _stutter = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;

    // Stand-ins for tg's adjust_dizzy / adjust_confusion: this codebase has no direct equivalents.
    private static readonly EntProtoId DizzinessStatusEffect = "StatusEffectWoozy";
    private static readonly EntProtoId ConfusionStatusEffect = "StatusEffectDrunk";

    private static readonly ProtoId<DamageTypePrototype> Poison = "Poison";
    private static readonly ProtoId<DamageTypePrototype> Asphyxiation = "Asphyxiation";

    // Timings from /datum/mutation/chemical_allergy/on_life.
    private static readonly TimeSpan StutterDuration = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan JitterDuration = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan DizzinessDuration = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan ConfusionDuration = TimeSpan.FromSeconds(4);

    // tg's adjust_blood_volume(-pick(12, 16, 20, 24), minimum = BLOOD_VOLUME_OKAY) scaled
    // from tg's 560 unit blood volume to this codebase's 300 unit reference volume.
    private const int MinBloodLoss = 6;
    private const int MaxBloodLoss = 12;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ChemicalAllergyComponent, BloodstreamComponent>();
        while (query.MoveNext(out var uid, out var allergy, out var bloodstream))
        {
            if (now < allergy.NextUpdate)
                continue;

            allergy.NextUpdate = now + allergy.UpdateInterval;

            // tg only runs mutation life procs while the mob is not dead.
            if (_mobState.IsDead(uid))
                continue;

            if (!HasDangerReagent(uid, allergy, bloodstream))
                continue;

            if (!_random.Prob(allergy.SymptomChance))
                continue;

            ApplySymptoms(uid, bloodstream);
        }
    }

    /// <summary>
    /// Checks whether the mob's bloodstream contains a reagent that triggers the allergy.
    /// </summary>
    private bool HasDangerReagent(EntityUid uid, ChemicalAllergyComponent allergy, BloodstreamComponent bloodstream)
    {
        if (!_solutions.TryGetSolution(uid, bloodstream.BloodSolutionName, out _, out var solution))
            return false;

        foreach (var (reagentId, quantity) in solution.Contents)
        {
            if (quantity.Float() < allergy.MinimumQuantity)
                continue;

            if (allergy.ExcludedReagents.Contains(reagentId.Prototype))
                continue;

            var reagent = _proto.Index<ReagentPrototype>(reagentId.Prototype);

            // Approximation of tg's reagent.metabolizing check: only reagents that have a
            // metabolism defined are actually processed by the body.
            if (reagent.Metabolisms is not { Count: > 0 })
                continue;

            if (allergy.ReagentGroups.Contains(reagent.Group))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Rolls and applies the tg allergic reaction symptoms.
    /// </summary>
    private void ApplySymptoms(EntityUid uid, BloodstreamComponent bloodstream)
    {
        // 66%: stutter for 4 seconds.
        if (_random.Prob(0.66f))
            _stutter.DoStutter(uid, StutterDuration, refresh: true);

        // 33%: 2-4 toxin damage.
        if (_random.Prob(0.33f))
        {
            var toxin = new DamageSpecifier(_proto.Index(Poison), _random.Next(2, 5));
            _damageable.TryChangeDamage(uid, toxin, ignoreResistances: true, interruptsDoAfters: false);
        }

        // 12%: jitter, dizziness and a small amount of blood loss.
        if (_random.Prob(0.12f))
        {
            _jitter.DoJitter(uid, JitterDuration, refresh: true);
            _status.TryAddStatusEffectDuration(uid, DizzinessStatusEffect, DizzinessDuration);
            TryLoseBlood(uid, bloodstream);
        }

        // 6%: confusion and loss of breath.
        if (_random.Prob(0.06f))
        {
            _status.TryAddStatusEffectDuration(uid, ConfusionStatusEffect, ConfusionDuration);

            // There is no losebreath equivalent, so deal a small amount of asphyxiation damage instead.
            var asphyxiation = new DamageSpecifier(_proto.Index(Asphyxiation), 4);
            _damageable.TryChangeDamage(uid, asphyxiation, ignoreResistances: true, interruptsDoAfters: false);
        }
    }

    /// <summary>
    /// Removes a small amount of blood, but never enough to actually cause bloodloss damage,
    /// mirroring tg's BLOOD_VOLUME_OKAY floor.
    /// </summary>
    private void TryLoseBlood(EntityUid uid, BloodstreamComponent bloodstream)
    {
        var level = _bloodstream.GetBloodLevel((uid, bloodstream));
        if (level <= bloodstream.BloodlossThreshold)
            return;

        // BloodReferenceSolution has restricted access and cannot be read from here,
        // so just stay above the bloodloss threshold instead of scaling by reference volume.
        var loss = _random.Next(MinBloodLoss, MaxBloodLoss + 1);
        _bloodstream.TryModifyBloodLevel((uid, bloodstream), FixedPoint2.New(-loss));
    }
}
