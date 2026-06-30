// SPDX-FileCopyrightText: 2025 August Eymann <august.eymann@gmail.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 SolsticeOfTheWinter <solsticeofthewinter@gmail.com>
// SPDX-FileCopyrightText: 2025 TheBorzoiMustConsume <197824988+TheBorzoiMustConsume@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Nutrition.EntitySystems;
using Content.Goobstation.Shared.Xenobiology;
using Content.Goobstation.Shared.Xenobiology.Components;
using Content.Goobstation.Shared.Xenobiology.Components.Equipment;
using Content.Server.NPC.HTN;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Content.Shared.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chemistry.Components;
using Content.Shared.Throwing;
using System.Numerics;

namespace Content.Goobstation.Server.Xenobiology;

// This handles any actions that slime mobs may have.
public sealed partial class SlimeLatchSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly StomachSystem _stomach = default!;
    [Dependency] private readonly GoobHungerSystem _goobHunger = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly HTNSystem _htn = default!;

    public const float XenoVacuumReleaseLatchBlockSeconds = 4f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlimeLatchEvent>(OnLatchAttempt);
        SubscribeLocalEvent<SlimeComponent, SlimeLatchDoAfterEvent>(OnSlimeLatchDoAfter);
        SubscribeLocalEvent<SlimeComponent, DoAfterAttemptEvent<SlimeLatchDoAfterEvent>>(OnDoAfterAttempt);

        SubscribeLocalEvent<SlimeDamageOvertimeComponent, MobStateChangedEvent>(OnMobStateChangedSOD);
        SubscribeLocalEvent<SlimeComponent, MobStateChangedEvent>(OnMobStateChangedSlime);
        SubscribeLocalEvent<SlimeComponent, PullAttemptEvent>(OnPullAttempt);
        SubscribeLocalEvent<SlimeComponent, EntGotRemovedFromContainerMessage>(OnEntGotRemovedFromContainer);
        SubscribeLocalEvent<SlimeComponent, EntGotInsertedIntoContainerMessage>(OnEntGotInsertedIntoContainer);
        SubscribeLocalEvent<SlimeComponent, SlimeMitosisEvent>(OnSlimeMitosis);
        SubscribeLocalEvent<SlimeComponent, SlimeTamedEvent>(OnSlimeTamed);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var sodQuery = EntityQueryEnumerator<SlimeDamageOvertimeComponent>();
        while (sodQuery.MoveNext(out var uid, out var dotComp))
            UpdateHunger((uid, dotComp));

        var slimeQuery = EntityQueryEnumerator<SlimeComponent>();
        while (slimeQuery.MoveNext(out var uid, out var slime))
            EnsureLatchedSlimeAnchored((uid, slime));
    }

    private void UpdateHunger(Entity<SlimeDamageOvertimeComponent> ent)
    {
        if (_gameTiming.CurTime < ent.Comp.NextTickTime || _mobState.IsDead(ent))
            return;

        ent.Comp.NextTickTime = _gameTiming.CurTime + ent.Comp.Interval;
        _damageable.TryChangeDamage(ent, ent.Comp.Damage, ignoreResistances: true, targetPart: TargetBodyPart.Chest);

        if (ent.Comp.SourceEntityUid is not { } source)
            return;

        var addedHunger = (float) ent.Comp.Damage.GetTotal();
        if (TryComp<HungerComponent>(source, out var hunger))
        {
            _hunger.ModifyHunger(source, addedHunger, hunger);
            Dirty(source, hunger);
        }

        var stomachList = _body.GetBodyOrganEntityComps<StomachComponent>(source);

        if (stomachList.Count == 0)
            return;

        FixedPoint2 availabaleVolume = 0;
        foreach (var stomach in stomachList)
        {
            if (_solutionContainer.ResolveSolution(stomach.Owner, StomachSystem.DefaultSolutionName, ref stomach.Comp1.Solution, out var sol))
                availabaleVolume += sol.AvailableVolume;
        }

        if (TryComp<BloodstreamComponent>(ent, out var bloodstream)
            && _solutionContainer.ResolveSolution(ent.Owner, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var blood)
            && _solutionContainer.ResolveSolution(ent.Owner, bloodstream.ChemicalSolutionName, ref bloodstream.ChemicalSolution, out var chem))
        {
            FixedPoint2 bloodProportion = blood.Volume/(chem.Volume + blood.Volume);
            FixedPoint2 chemProportion = 1 - bloodProportion;
            FixedPoint2 bloodTransfer = FixedPoint2.Min(ent.Comp.SuctionUnits * bloodProportion, availabaleVolume * bloodProportion);
            FixedPoint2 chemTransfer = FixedPoint2.Min(ent.Comp.SuctionUnits * chemProportion, availabaleVolume * chemProportion);
            foreach (var stomach in stomachList)
            {
                var bloodSolution = blood.SplitSolutionWithout(bloodTransfer/FixedPoint2.New(stomachList.Count), ent.Comp.ToxinReagent); // we don't want slime sucking it's own toxin instad of drinking blood
                _stomach.TryTransferSolution(stomach.Owner, bloodSolution, stomach); // blood first, other chemicals later
                var chemSolution = blood.SplitSolution(chemTransfer/FixedPoint2.New(stomachList.Count));
                _stomach.TryTransferSolution(stomach.Owner, chemSolution, stomach);
            }
            chem.AddReagent(ent.Comp.ToxinReagent, ent.Comp.ToxinUnits);
        }
    }

    private void OnMobStateChangedSOD(Entity<SlimeDamageOvertimeComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var source = ent.Comp.SourceEntityUid;
        if (source.HasValue && TryComp<SlimeComponent>(source, out var slime))
            Unlatch((source.Value, slime));
    }

    private void OnMobStateChangedSlime(Entity<SlimeComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            Unlatch(ent);
    }

    private void OnPullAttempt(Entity<SlimeComponent> ent, ref PullAttemptEvent args)
    {
        if (IsLatched(ent) && args.PullerUid == ent.Owner) // slimes can't pull when latched
        {
            args.Cancelled = true;
            return;
        }

        Unlatch(ent);
    }

    private void OnEntGotRemovedFromContainer(Entity<SlimeComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        Unlatch(ent);
    }

    private void OnEntGotInsertedIntoContainer(Entity<SlimeComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        Unlatch(ent);

        if (!HasComp<XenoVacuumTankComponent>(args.Container.Owner))
            return;

        if (TryComp<HTNComponent>(ent, out _))
            _htn.SetHTNEnabled(ent, false);
    }

    private void OnSlimeMitosis(Entity<SlimeComponent> ent, ref SlimeMitosisEvent args)
    {
        Unlatch(ent);
    }

    private void OnSlimeTamed(Entity<SlimeComponent> ent, ref SlimeTamedEvent args)
    {
        if (IsLatched(ent, args.Tamer))
        {
            Unlatch(ent);
            return;
        }

        if (ent.Comp.PendingLatchTarget == args.Tamer)
            CancelLatchAttempt(ent);
    }
    private void OnLatchAttempt(SlimeLatchEvent args)
    {
        if (TerminatingOrDeleted(args.Target)
        || TerminatingOrDeleted(args.Performer)
        || !TryComp<SlimeComponent>(args.Performer, out var slime))
            return;

        var ent = new Entity<SlimeComponent>(args.Performer, slime);

        if (IsLatched(ent))
        {
            Unlatch(ent);
            return;
        }

        if (!TryValidateLatchTarget((args.Performer, slime), args.Target, out var failMessage))
        {
            _popup.PopupEntity(failMessage, ent, ent);
            return;
        }

        if (SlimeClusterHelper.IsMergedCluster(args.Performer, EntityManager))
        {
            _popup.PopupEntity(Loc.GetString("slime-latch-fail-cluster"), ent, ent);
            return;
        }

        if (IsVacuumLatchBlocked(args.Performer))
            return;

        if (CanLatch((args.Performer, slime), args.Target))
        {
            StartSlimeLatchDoAfter((args.Performer, slime), args.Target);
            return;
        }

        // improvement space (tm)
    }

    private bool StartSlimeLatchDoAfter(Entity<SlimeComponent> ent, EntityUid target)
    {
        if (IsLatchAttemptInProgress(ent))
            return false;

        if (HasComp<BeingLatchedComponent>(target) || HasComp<SlimeDamageOvertimeComponent>(target))
            return false;

        if (_mobState.IsDead(target))
        {
            var targetDeadPopup = Loc.GetString("slime-latch-fail-target-dead", ("ent", target));
            _popup.PopupEntity(targetDeadPopup, ent, ent);

            return false;
        }

        if (ent.Comp.Stomach.Count >= ent.Comp.MaxContainedEntities)
        {
            var maxEntitiesPopup = Loc.GetString("slime-latch-fail-max-entities", ("ent", target));
            _popup.PopupEntity(maxEntitiesPopup, ent, ent);

            return false;
        }

        if (HasComp<BeingLatchedComponent>(target))
        {
            var maxEntitiesPopup = Loc.GetString("slime-latch-fail-already-latched", ("ent", target));
            _popup.PopupEntity(maxEntitiesPopup, ent, ent);

            return false;
        }

        var attemptPopup = Loc.GetString("slime-latch-attempt", ("slime", ent), ("ent", target));
        _popup.PopupEntity(attemptPopup, target, PopupType.LargeCaution);

        var doAfterArgs = new DoAfterArgs(EntityManager, ent, ent.Comp.LatchDoAfterDuration, new SlimeLatchDoAfterEvent(), ent, target)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            AttemptFrequency = AttemptFrequency.StartAndEnd,
        };

        BeginLatchAttempt(ent, target);
        EnsureComp<BeingLatchedComponent>(target);

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
        {
            CancelLatchAttempt(ent);
            return false;
        }

        return true;
    }

    private void OnDoAfterAttempt(EntityUid uid, SlimeComponent comp, ref DoAfterAttemptEvent<SlimeLatchDoAfterEvent> args)
    {
        if (IsVacuumLatchBlocked(uid))
        {
            args.Cancel();
            return;
        }

        if (args.Event.Target is not { } target)
            return;

        if (comp.PendingLatchTarget == target)
            return;

        if (HasComp<BeingLatchedComponent>(target) || HasComp<SlimeDamageOvertimeComponent>(target))
            args.Cancel();
    }

    private void OnSlimeLatchDoAfter(Entity<SlimeComponent> ent, ref SlimeLatchDoAfterEvent args)
    {
        if (args.Target is not { } target)
            return;

        if (args.Handled || args.Cancelled)
        {
            ClearLatchAttempt(ent);
            RemCompDeferred<BeingLatchedComponent>(target);
            return;
        }

        if (!CanLatch(ent, target, ignoreBeingLatched: true))
        {
            ClearLatchAttempt(ent);
            RemCompDeferred<BeingLatchedComponent>(target);
            return;
        }

        ClearLatchAttempt(ent);
        Latch(ent, target);
        args.Handled = true;
    }

    private void OnEntityEscape(Entity<SlimeComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (!HasComp<SlimeDamageOvertimeComponent>(args.Entity))
            return;

        RemCompDeferred<SlimeDamageOvertimeComponent>(args.Entity);
        RemCompDeferred<BeingLatchedComponent>(args.Entity);
        ent.Comp.LatchedTarget = null;
    }

    #region Helpers

    public bool IsLatched(Entity<SlimeComponent> ent)
        => ent.Comp.LatchedTarget.HasValue;

    public bool IsLatched(Entity<SlimeComponent> ent, EntityUid target)
        => IsLatched(ent) && ent.Comp.LatchedTarget!.Value == target;

    public bool IsLatchAttemptInProgress(Entity<SlimeComponent> ent)
    {
        if (ent.Comp.PendingLatchTarget is not { } target)
            return false;

        if (_gameTiming.CurTime >= ent.Comp.PendingLatchUntil || TerminatingOrDeleted(target))
        {
            ClearLatchAttempt(ent);
            return false;
        }

        return true;
    }

    public bool IsLatchAttemptInProgress(Entity<SlimeComponent> ent, EntityUid target)
        => IsLatchAttemptInProgress(ent) && ent.Comp.PendingLatchTarget == target;

    public bool IsVacuumLatchBlocked(EntityUid uid)
    {
        if (_container.TryGetContainingContainer(uid, out var container)
            && HasComp<XenoVacuumTankComponent>(container.Owner))
            return true;

        if (HasComp<ThrownItemComponent>(uid))
            return true;

        if (TryComp<SlimeXenoVacuumReleasedComponent>(uid, out var released)
            && _gameTiming.CurTime < released.BlockLatchUntil)
            return true;

        return false;
    }

    public void MarkXenoVacuumReleased(EntityUid uid)
    {
        var released = EnsureComp<SlimeXenoVacuumReleasedComponent>(uid);
        released.BlockLatchUntil = _gameTiming.CurTime + TimeSpan.FromSeconds(XenoVacuumReleaseLatchBlockSeconds);
    }

    public void CancelLatchIfSlime(EntityUid uid)
    {
        if (TryComp<SlimeComponent>(uid, out var slime))
            CancelLatchAttempt((uid, slime));
    }

    public bool CanLatch(Entity<SlimeComponent> ent, EntityUid target, bool ignoreBeingLatched = false)
    {
        if (SlimeClusterHelper.IsMergedCluster(ent.Owner, EntityManager))
            return false;

        if (IsVacuumLatchBlocked(ent.Owner))
            return false;

        return TryValidateLatchTarget(ent, target, out _)
            && !(IsLatched(ent)
            || _mobState.IsDead(target)
            || !_actionBlocker.CanInteract(ent, target)
            || (!ignoreBeingLatched && HasComp<BeingLatchedComponent>(target))
            || HasComp<SlimeDamageOvertimeComponent>(target)
            || !HasComp<MobStateComponent>(target));
    }

    private bool TryValidateLatchTarget(Entity<SlimeComponent> ent, EntityUid target, out string failMessage)
    {
        failMessage = string.Empty;

        if (ent.Comp.Tamer != target)
            return true;

        if (TryComp<MobGrowthComponent>(ent, out var growth) && growth.IsFirstStage)
        {
            failMessage = Loc.GetString("slime-latch-fail-tamer", ("ent", target));
            return false;
        }

        if (TryComp<HungerComponent>(ent, out var hunger)
            && _goobHunger.IsHungerAboveState(ent, HungerThreshold.Peckish, comp: hunger))
        {
            failMessage = Loc.GetString("slime-latch-fail-tamer", ("ent", target));
            return false;
        }

        return true;
    }

    public bool NpcTryLatch(Entity<SlimeComponent> ent, EntityUid target)
    {
        if (!CanLatch(ent, target))
            return false;

        return StartSlimeLatchDoAfter(ent, target);
    }

    public void CancelLatchAttempt(Entity<SlimeComponent> ent)
    {
        if (ent.Comp.PendingLatchTarget is { } pendingTarget)
            RemCompDeferred<BeingLatchedComponent>(pendingTarget);

        ClearLatchAttempt(ent);
    }

    public void Latch(Entity<SlimeComponent> ent, EntityUid target)
    {
        RemCompDeferred<BeingLatchedComponent>(target);

        _xform.SetCoordinates(ent, Transform(target).Coordinates);
        _xform.SetParent(ent, target);
        if (TryComp<InputMoverComponent>(ent, out var inpm))
            inpm.CanMove = false;

        ent.Comp.LatchedTarget = target;

        EnsureComp<BeingLatchedComponent>(target);
        EnsureComp(target, out SlimeDamageOvertimeComponent comp);
        comp.SourceEntityUid = ent;

        RemComp<PullableComponent>(ent);
        RemComp<PullerComponent>(ent); // crutches

        _audio.PlayEntity(ent.Comp.EatSound, ent, ent);
        _popup.PopupEntity(Loc.GetString("slime-action-latch-success", ("slime", ent), ("target", target)), ent, PopupType.SmallCaution);

        Dirty(ent);
        Dirty(target, comp);

        // We also need to set a new state for the slime when it's consuming,
        // this will be easy however it's important to take MobGrowthSystem into account... possibly we should use layers?
    }

    public void Unlatch(Entity<SlimeComponent> ent)
    {
        CancelLatchAttempt(ent);

        if (!IsLatched(ent))
            return;

        var target = ent.Comp.LatchedTarget!.Value;

        RemCompDeferred<BeingLatchedComponent>(target);
        RemCompDeferred<SlimeDamageOvertimeComponent>(target);

        EnsureComp<PullableComponent>(ent);
        EnsureComp<PullerComponent>(ent); // on top of crutches

        if (TryComp<TransformComponent>(target, out var targetXform)
            && _xform.IsParentOf(targetXform, ent.Owner))
            _xform.SetParent(ent.Owner, _xform.GetParentUid(target));
        if (TryComp<InputMoverComponent>(ent, out var inpm))
            inpm.CanMove = true;

        ent.Comp.LatchedTarget = null;
    }

    private void EnsureLatchedSlimeAnchored(Entity<SlimeComponent> ent)
    {
        if (!IsLatched(ent))
            return;

        var target = ent.Comp.LatchedTarget!.Value;
        if (TerminatingOrDeleted(target))
        {
            Unlatch(ent);
            return;
        }

        _xform.SetCoordinates(ent, new EntityCoordinates(target, Vector2.Zero));

        if (TryComp<InputMoverComponent>(ent, out var inpm))
            inpm.CanMove = false;
    }

    private void BeginLatchAttempt(Entity<SlimeComponent> ent, EntityUid target)
    {
        ent.Comp.PendingLatchTarget = target;
        ent.Comp.PendingLatchUntil = _gameTiming.CurTime + ent.Comp.LatchDoAfterDuration + TimeSpan.FromSeconds(0.25);
    }

    private static void ClearLatchAttempt(Entity<SlimeComponent> ent)
    {
        ent.Comp.PendingLatchTarget = null;
        ent.Comp.PendingLatchUntil = TimeSpan.Zero;
    }

    #endregion
}
