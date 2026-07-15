using Content.Shared.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Hands.Systems;
using Content.Server.Popups;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Examine;
using Content.Shared.Fluids.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.WhiteDream.BloodCult.BloodCultist;
using Content.Shared.WhiteDream.BloodCult.Constructs;
using Content.Shared.WhiteDream.BloodCult.Spells;
using Content.Shared.WhiteDream.BloodCult.UI;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.WhiteDream.BloodCult.BloodRites;

public sealed class BloodRitesSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly HandsSystem _handsSystem = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private readonly ProtoId<ReagentPrototype> _bloodProto = "Blood";

    public override void Initialize()
    {
        SubscribeLocalEvent<BloodRitesAuraComponent, ExaminedEvent>(OnExamining);

        SubscribeLocalEvent<BloodRitesAuraComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<BloodRitesAuraComponent, MeleeHitEvent>(OnMeleeHit);

        SubscribeLocalEvent<BloodRitesAuraComponent, BeforeActivatableUIOpenEvent>(BeforeUiOpen);
        SubscribeLocalEvent<BloodRitesAuraComponent, BloodRitesMessage>(OnRitesMessage);

        SubscribeLocalEvent<BloodRitesAuraComponent, DroppedEvent>(OnDropped);
    }

    private void OnExamining(Entity<BloodRitesAuraComponent> rites, ref ExaminedEvent args) =>
        args.PushMarkup(Loc.GetString("blood-rites-stored-blood", ("amount", rites.Comp.StoredBlood.ToString())));

    private void OnAfterInteract(Entity<BloodRitesAuraComponent> rites, ref AfterInteractEvent args)
    {
        if (!args.Target.HasValue || args.Handled || args.Target == args.User)
            return;

        if (HasComp<BloodCultistComponent>(args.Target) || HasComp<ConstructComponent>(args.Target))
        {
            if (TryHealCultist(rites, args.User, args.Target.Value))
            {
                _audio.PlayPvs(rites.Comp.BloodRitesAudio, rites);
                args.Handled = true;
            }

            return;
        }

        if (HasComp<BloodstreamComponent>(args.Target))
        {
            if (TryExtractBlood(rites, args.Target.Value))
            {
                _audio.PlayPvs(rites.Comp.BloodRitesAudio, rites);
                args.Handled = true;
            }

            return;
        }

        if (HasComp<PuddleComponent>(args.Target))
        {
            ConsumePuddles(args.Target.Value, rites);
            args.Handled = true;
        }
        else if (TryComp(args.Target, out SolutionContainerManagerComponent? solutionContainer))
        {
            ConsumeBloodFromSolution((args.Target.Value, solutionContainer), rites);
            args.Handled = true;
        }
    }

    private void OnMeleeHit(Entity<BloodRitesAuraComponent> rites, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        if (args.HitEntities.Count == 0)
        {
            TryConsumePuddlesAtCoordinates(args.Coords, rites);
            return;
        }

        var playSound = false;

        foreach (var target in args.HitEntities)
        {
            if (target == args.User)
                continue;

            if (HasComp<BloodCultistComponent>(target) || HasComp<ConstructComponent>(target))
            {
                if (TryHealCultist(rites, args.User, target))
                    playSound = true;

                continue;
            }

            if (HasComp<PuddleComponent>(target))
            {
                ConsumePuddles(target, rites);
                playSound = true;
                continue;
            }

            if (HasComp<BloodstreamComponent>(target) && TryExtractBlood(rites, target))
            {
                playSound = true;
                args.Handled = true;
            }
        }

        if (playSound)
            _audio.PlayPvs(rites.Comp.BloodRitesAudio, rites);
    }

    private bool TryExtractBlood(Entity<BloodRitesAuraComponent> rites, EntityUid target)
    {
        if (!TryComp(target, out BloodstreamComponent? bloodstream) ||
            bloodstream.BloodSolution is not { } solution)
            return false;

        if (bloodstream.BloodReferenceSolution.Contents.Count == 0)
            return false;

        var extracted = solution.Comp.Solution.RemoveReagent(
            bloodstream.BloodReferenceSolution.Contents[0].Reagent,
            rites.Comp.BloodExtractionAmount,
            ignoreReagentData: true);

        if (extracted <= FixedPoint2.Zero)
            return false;

        _solutionContainer.UpdateChemicals(solution);
        rites.Comp.StoredBlood += extracted;
        Dirty(target, bloodstream);
        return true;
    }

    private bool TryHealCultist(Entity<BloodRitesAuraComponent> rites, EntityUid user, EntityUid target)
    {
        var healed = false;

        if (TryComp(target, out BloodstreamComponent? bloodstream) &&
            RestoreBloodLevel(rites, user, (target, bloodstream)))
        {
            healed = true;
        }

        if (TryComp(target, out DamageableComponent? damageable) && Heal(rites, user, (target, damageable)))
            healed = true;

        return healed;
    }

    private void TryConsumePuddlesAtCoordinates(EntityCoordinates coordinates, Entity<BloodRitesAuraComponent> rites)
    {
        var lookup = _lookup.GetEntitiesInRange<PuddleComponent>(
            coordinates,
            rites.Comp.PuddleConsumeRadius,
            LookupFlags.Uncontained);

        if (lookup.Count == 0)
            return;

        foreach (var puddle in lookup)
            ConsumePuddles(puddle, rites);

        _audio.PlayPvs(rites.Comp.BloodRitesAudio, rites);
    }

    private void BeforeUiOpen(Entity<BloodRitesAuraComponent> rites, ref BeforeActivatableUIOpenEvent args)
    {
        var state = new BloodRitesUiState(rites.Comp.Crafts, rites.Comp.StoredBlood);
        _ui.SetUiState(rites.Owner, BloodRitesUiKey.Key, state);
    }

    private void OnRitesMessage(Entity<BloodRitesAuraComponent> rites, ref BloodRitesMessage args)
    {
        Del(rites);

        var ent = Spawn(args.SelectedProto, _transform.GetMapCoordinates(args.Actor));
        _handsSystem.TryPickup(args.Actor, ent);
    }

    private void OnDropped(Entity<BloodRitesAuraComponent> rites, ref DroppedEvent args) => QueueDel(rites);

    private bool Heal(Entity<BloodRitesAuraComponent> rites, EntityUid user, Entity<DamageableComponent> target)
    {
        if (target.Comp.TotalDamage == 0)
            return false;

        if (TryComp(target, out MobStateComponent? mobState) && mobState.CurrentState == MobState.Dead)
        {
            _popup.PopupEntity(Loc.GetString("blood-rites-heal-dead"), target, user);
            return false;
        }

        if (!HasComp<BloodstreamComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("blood-rites-heal-no-bloodstream"), target, user);
            return false;
        }

        var bloodCost = rites.Comp.HealingCost;
        if (target.Owner == user)
            bloodCost *= rites.Comp.SelfHealRatio;

        if (bloodCost >= rites.Comp.StoredBlood)
        {
            _popup.PopupEntity(Loc.GetString("blood-rites-not-enough-blood"), rites, user);
            return false;
        }

        var healingLeft = rites.Comp.TotalHealing;

        foreach (var (type, value) in target.Comp.Damage.DamageDict)
        {
            // somehow?
            if (!_protoManager.TryIndex(type, out DamageTypePrototype? damageType))
                continue;

            var toHeal = value;
            if (toHeal > healingLeft)
                toHeal = healingLeft;

            _damageable.TryChangeDamage(target, new DamageSpecifier(damageType, -toHeal));

            healingLeft -= toHeal;
            if (healingLeft == 0)
                break;
        }

        rites.Comp.StoredBlood -= bloodCost;
        return true;
    }

    private bool RestoreBloodLevel(
        Entity<BloodRitesAuraComponent> rites,
        EntityUid user,
        Entity<BloodstreamComponent> target
    )
    {
        if (target.Comp.BloodSolution is null)
            return false;

        _bloodstream.FlushChemicals(target.AsNullable(), FixedPoint2.New(10));
        var missingBlood = target.Comp.BloodSolution.Value.Comp.Solution.AvailableVolume;
        if (missingBlood == 0)
            return false;

        var bloodCost = missingBlood * rites.Comp.BloodRegenerationRatio;
        if (target.Owner == user)
            bloodCost *= rites.Comp.SelfHealRatio;

        if (bloodCost > rites.Comp.StoredBlood)
        {
            _popup.PopupEntity("blood-rites-no-blood-left", rites, user);
            bloodCost = rites.Comp.StoredBlood;
        }

        _bloodstream.TryModifyBleedAmount(target.AsNullable(), -3);
        _bloodstream.TryModifyBloodLevel(target.AsNullable(), bloodCost / rites.Comp.BloodRegenerationRatio);

        rites.Comp.StoredBlood -= bloodCost;
        return true;
    }

    private void ConsumePuddles(EntityUid origin, Entity<BloodRitesAuraComponent> rites)
    {
        var coords = Transform(origin).Coordinates;

        var lookup = _lookup.GetEntitiesInRange<PuddleComponent>(
            coords,
            rites.Comp.PuddleConsumeRadius,
            LookupFlags.Uncontained);

        foreach (var puddle in lookup)
        {
            if (!TryComp(puddle, out SolutionContainerManagerComponent? solutionContainer))
                continue;
            ConsumeBloodFromSolution((puddle, solutionContainer), rites);
        }

        _audio.PlayPvs(rites.Comp.BloodRitesAudio, rites);
    }

    private void ConsumeBloodFromSolution(
        Entity<SolutionContainerManagerComponent?> ent,
        Entity<BloodRitesAuraComponent> rites
    )
    {
        foreach (var (_, solution) in _solutionContainer.EnumerateSolutions(ent))
        {
            rites.Comp.StoredBlood += solution.Comp.Solution.RemoveReagent(
                _bloodProto,
                1000,
                ignoreReagentData: true);
            _solutionContainer.UpdateChemicals(solution);
            break;
        }
    }
}
