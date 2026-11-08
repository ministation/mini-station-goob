// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Goobstation.Shared.Projectiles;
using Content.Server.Fluids.EntitySystems;
using Content.Shared._Goobstation.Wizard.Projectiles;
using Content.Shared._Lavaland.Aggression;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Fluids.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Random.Helpers;
using Content.Shared.Sprite;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Throwing;
using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Lavaland.Bubblegum;

public sealed class BubblegumSystem : EntitySystem
{
    private static readonly Vector2i[] Cardinals =
    {
        new(0, 1),
        new(1, 0),
        new(0, -1),
        new(-1, 0),
    };

    [Dependency] private readonly AggressorsSystem _aggressors = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly PuddleSystem _puddle = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    private static readonly string[] BloodReagents =
    [
        "Blood",
        "Slime",
        "CopperBlood",
        "BloodChangeling",
        "BlackBlood",
    ];

    private readonly List<EntityUid> _participants = new();
    private readonly List<EntityUid> _bloodTargets = new();
    private readonly List<Vector2i> _poolTiles = new();
    private readonly List<Vector2i> _cloneTiles = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BubblegumComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<BubblegumComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BubblegumComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<BubblegumComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<BubblegumComponent, MoveEvent>(OnBloodWalkMoved);
        SubscribeLocalEvent<PuddleComponent, StepTriggeredOnEvent>(OnBloodPuddleStepped);
        SubscribeLocalEvent<BubblegumEnrageCloudComponent, StepTriggeredOnEvent>(OnEnrageCloudStepped);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<BubblegumComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var bubblegum, out var xform))
        {
            if (IsDead(uid) ||
                bubblegum.CombatGrid is not { Valid: true } gridUid ||
                xform.GridUid != gridUid ||
                !TryComp<MapGridComponent>(gridUid, out var grid))
            {
                ClearRuntimeState(uid, bubblegum, false);
                continue;
            }

            PruneTracked(bubblegum);
            ProcessEnrage(uid, bubblegum, now);
            ProcessActiveClones(bubblegum, now);
            ProcessCloneCharges(bubblegum, gridUid, grid, now);
            ProcessPendingBloodTiles(bubblegum, gridUid, grid, now);
            ProcessPendingHandAttacks(uid, bubblegum, gridUid, grid, now);

            var participantCount = CollectParticipants(uid, bubblegum, gridUid, grid);
            if (participantCount == 0)
            {
                ClearRuntimeState(uid, bubblegum, true);
                bubblegum.BusyUntil = TimeSpan.Zero;
                continue;
            }

            if (ProcessCharge(uid, bubblegum, gridUid, grid, now) ||
                ProcessQueuedCharge(uid, bubblegum, gridUid, grid, now))
            {
                continue;
            }

            var bloodReactionWindow = GetBloodReactionWindow(bubblegum);
            if (bubblegum.BusyUntil <= now &&
                bubblegum.NextAttack - now > bloodReactionWindow &&
                bubblegum.NextBloodReaction <= now &&
                TryQueueBloodAttack(uid, bubblegum, gridUid, grid, now))
            {
                bubblegum.LastPressureAt = now;
                bubblegum.LastAttackKind = "blood-reaction";
                bubblegum.NextBloodReaction = now + bubblegum.BloodReactionCooldown;
                continue;
            }

            if (bubblegum.BusyUntil > now ||
                bubblegum.NextAttack > now)
            {
                continue;
            }

            var target = PickTarget(bubblegum, uid, gridUid, grid, now);
            if (target == null)
                continue;

            RunAttack(uid, bubblegum, gridUid, grid, target.Value, now);
        }
    }

    private void OnDamageChanged(Entity<BubblegumComponent> ent, ref DamageChangedEvent args)
    {
        TryAddAggressorFromDamage(ent, ref args);
        TrySpawnBloodOnDamage(ent, ref args);
    }

    private void TryAddAggressorFromDamage(Entity<BubblegumComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased ||
            args.DamageDelta == null ||
            args.DamageDelta.GetTotal() <= 0 ||
            args.Origin is not { Valid: true } origin ||
            origin == ent.Owner ||
            !TryComp<AggressiveComponent>(ent.Owner, out var aggressive))
        {
            return;
        }

        if (IsDead(origin) || ent.Comp.Slaughterlings.Contains(origin))
            return;

        _aggressors.AddAggressor((ent.Owner, aggressive), origin);
    }

    private void TrySpawnBloodOnDamage(Entity<BubblegumComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased ||
            args.DamageDelta == null ||
            args.DamageDelta.GetTotal() <= 0 ||
            !_random.Prob(0.25f) ||
            ent.Comp.CombatGrid is not { Valid: true } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return;
        }

        var tile = GetEntityTile(ent.Owner, gridUid, grid);
        if (tile == null)
            return;

        if (_random.Prob(0.4f))
            tile += _random.Pick(Cardinals);

        if (IsInsideCombatArea(ent.Comp, tile.Value))
        {
            TrySpillBlood(ent.Comp, gridUid, grid, tile.Value);
            SpawnAnchored(ent.Comp.BloodGibsPrototype, gridUid, grid, tile.Value);
        }
    }

    private void OnBloodPuddleStepped(Entity<PuddleComponent> ent, ref StepTriggeredOnEvent args)
    {
        if (!IsBloodPuddle(ent.Owner) || IsDead(args.Tripper))
            return;

        var tripper = args.Tripper;
        if (HasComp<BubblegumComponent>(tripper) || !TryComp<TransformComponent>(tripper, out var tripperXform))
            return;

        var query = EntityQueryEnumerator<BubblegumComponent, TransformComponent, AggressiveComponent>();
        while (query.MoveNext(out var boss, out var bubblegum, out var bossXform, out var aggressive))
        {
            if (IsDead(boss) ||
                bubblegum.CombatGrid is not { Valid: true } gridUid ||
                tripperXform.GridUid != gridUid ||
                bossXform.GridUid != gridUid ||
                !TryComp<MapGridComponent>(gridUid, out var grid))
            {
                continue;
            }

            var tile = GetEntityTile(tripper, gridUid, grid);
            if (tile == null || !IsInsideCombatArea(bubblegum, tile.Value))
                continue;

            if (bubblegum.Slaughterlings.Contains(tripper))
                continue;

            _aggressors.AddAggressor((boss, aggressive), tripper);
        }
    }

    private void PrepareFight(EntityUid uid, BubblegumComponent component)
    {
        ClearRuntimeState(uid, component, true);
        var now = _timing.CurTime;
        component.NextAttack = now + TimeSpan.FromSeconds(1);
        component.NextSummon = now + TimeSpan.FromSeconds(3);
        component.NextBloodReaction = now + TimeSpan.FromSeconds(1.5);
        component.LastPressureAt = now;
        component.LastAttackKind = string.Empty;
    }

    private void OnMobStateChanged(Entity<BubblegumComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            ClearRuntimeState(ent.Owner, ent.Comp, false);
    }

    private void OnRefreshMovementSpeed(EntityUid uid, BubblegumComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (component.Charging)
        {
            args.ModifySpeed(0f, 0f);
            return;
        }

        var rage = CalculateRage(uid);
        var modifier = Math.Clamp(1f + rage * 0.025f, 1f, 1.5f);
        if (component.Enraged)
            modifier *= component.EnrageSpeedMultiplier;

        args.ModifySpeed(modifier, modifier);
    }

    /// <summary>
    /// Blood warp enrage: projectile immunity, a speed boost and a blood cloud at both ends of the warp.
    /// </summary>
    private void StartEnrage(
        EntityUid boss,
        BubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i from,
        Vector2i to)
    {
        var now = _timing.CurTime;
        if (bubblegum.Enraged || now < bubblegum.EnrageCooldownUntil)
            return; // TG: no re-enrage until the cooldown after the previous one has passed.

        var duration = bubblegum.EnrageDuration;
        if (TryComp<DamageableComponent>(boss, out var damageable)
            && _mobThreshold.TryGetThresholdForState(boss, MobState.Dead, out var maxHealth)
            && maxHealth != null)
        {
            // TG enrage_time: scales from 3.5 to 7 seconds as the boss loses health.
            var missing = (float) maxHealth.Value - (float) damageable.TotalDamage;
            var fraction = Math.Clamp(missing / 60f, 0f, 20f) / 20f;
            duration = TimeSpan.FromSeconds(3.5f + 3.5f * fraction);
        }

        bubblegum.Enraged = true;
        bubblegum.EnrageUntil = now + duration;
        bubblegum.EnrageCooldownUntil = now + duration + bubblegum.EnrageCooldown;

        var immunity = EnsureComp<ProjectileImmunityComponent>(boss);
        immunity.ExpireTime = bubblegum.EnrageUntil;
        Dirty(boss, immunity);

        // TG parity: Bubblegum turns red while enraged.
        var tint = EnsureComp<RandomSpriteComponent>(boss);
        tint.Selected[bubblegum.EnrageTintLayer] = (bubblegum.EnrageTintState, bubblegum.EnrageTint);
        Dirty(boss, tint);

        _movement.RefreshMovementSpeedModifiers(boss);

        // The cloud marks both ends of the warp: where it vanished and where it reappeared.
        SpawnEnrageCloud(bubblegum, gridUid, grid, from);
        if (to != from)
            SpawnEnrageCloud(bubblegum, gridUid, grid, to);
    }

    private void ProcessEnrage(EntityUid boss, BubblegumComponent bubblegum, TimeSpan now)
    {
        if (!bubblegum.Enraged || now < bubblegum.EnrageUntil)
            return;

        bubblegum.Enraged = false;
        bubblegum.EnrageUntil = TimeSpan.Zero;
        bubblegum.EnrageCooldownUntil = now + bubblegum.EnrageCooldown;
        RemComp<RandomSpriteComponent>(boss);
        _movement.RefreshMovementSpeedModifiers(boss);
    }

    private void SpawnEnrageCloud(BubblegumComponent bubblegum, EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        Spawn(bubblegum.EnrageCloudPrototype, _map.GridTileToLocal(gridUid, grid, tile));
    }

    /// <summary>
    /// TG blood walk: Bubblegum leaves a blood puddle under every step it takes.
    /// </summary>
    private void OnBloodWalkMoved(Entity<BubblegumComponent> ent, ref MoveEvent args)
    {
        // Deletion detaches the boss to nullspace and raises MoveEvent; spilling blood there
        // would spawn a puddle parented to the terminating grid.
        if (TerminatingOrDeleted(ent.Owner))
            return;

        var bubblegum = ent.Comp;
        var now = _timing.CurTime;
        if (now < bubblegum.BloodWalkCooldownUntil || args.OldPosition == args.NewPosition)
            return;

        if (_transform.GetGrid(args.OldPosition) is not { } gridUid
            || !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return;
        }

        var tile = _map.LocalToTile(gridUid, grid, args.OldPosition);
        if (TrySpillBlood(bubblegum, gridUid, grid, tile))
            bubblegum.BloodWalkCooldownUntil = now + bubblegum.BloodWalkDelay;
    }

    private void OnEnrageCloudStepped(Entity<BubblegumEnrageCloudComponent> ent, ref StepTriggeredOnEvent args)
    {
        var tripper = args.Tripper;
        if (!HasComp<MobStateComponent>(tripper) || IsDead(tripper) || HasComp<BubblegumComponent>(tripper))
            return;

        var cloudXform = Transform(ent.Owner);
        var tripperXform = Transform(tripper);
        var direction = _transform.GetWorldPosition(tripperXform) - _transform.GetWorldPosition(cloudXform);
        if (direction.LengthSquared() < 0.01f)
            direction = _random.NextVector2();

        _throwing.TryThrow(tripper, direction.Normalized() * ent.Comp.KnockbackDistance, ent.Comp.KnockbackSpeed, ent.Owner, playSound: false, doSpin: false);
        _damageable.TryChangeDamage(tripper, ent.Comp.Damage, origin: ent.Owner);
    }

    private void RunAttack(
        EntityUid boss,
        BubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        EntityUid target,
        TimeSpan now)
    {
        var rage = CalculateRage(boss);
        var healthFraction = GetHealthFraction(boss);
        var belowHalf = healthFraction <= 0.5f;
        var targetTile = GetEntityTile(target, gridUid, grid);
        var bossTile = GetEntityTile(boss, gridUid, grid);
        if (targetTile == null || bossTile == null)
        {
            bubblegum.NextAttack = now + TimeSpan.FromSeconds(0.5);
            return;
        }

        var targetDistance = ChebyshevDistance(bossTile.Value, targetTile.Value);
        if (ShouldPrioritizeMovement(bubblegum, boss, targetDistance, now))
        {
            RunMovementCombo(boss, bubblegum, gridUid, grid, target, rage, healthFraction, now);
            return;
        }

        var forcePressure = NeedsPressure(bubblegum, now);
        var pressureTarget = PickPressureTarget(bubblegum, target, gridUid, grid, now) ?? target;
        var pressureTargetTile = GetEntityTile(pressureTarget, gridUid, grid) ?? targetTile.Value;
        var pressureDistance = ChebyshevDistance(bossTile.Value, pressureTargetTile);
        var pressureTargetHasBlood = HasBloodWithin(gridUid, grid, pressureTargetTile, 1);
        var pressureStale = IsPressureStale(bubblegum, pressureTarget, now, TimeSpan.FromSeconds(bubblegum.TargetPressureMemory.TotalSeconds * 0.45));
        var forcedAmbush = false;

        if ((forcePressure || !IsRecentPressureAttack(bubblegum)) &&
            (forcePressure || !pressureTargetHasBlood && (pressureDistance > 3 || belowHalf || pressureStale)))
        {
            forcedAmbush = QueueBloodPressureAtTarget(boss, bubblegum, gridUid, grid, pressureTargetTile, now, forcePressure);
            if (forcedAmbush)
            {
                MarkPressure(bubblegum, now, forcePressure ? "forced-blood-pressure" : "blood-pressure", pressureTarget);
                bubblegum.BusyUntil = now + bubblegum.BloodSmackDelay + bubblegum.BloodHandRecover;
                bubblegum.NextAttack = now + GetScaledCooldown(bubblegum.RangedCooldown, rage);
                bubblegum.NextBloodReaction = now + bubblegum.BloodReactionCooldown;
                return;
            }
        }

        var canUseBloodHand = !IsRecentBloodHandAttack(bubblegum) || _random.Prob(belowHalf ? 0.45f : 0.25f);
        var didBloodAttack = !forcedAmbush &&
                             canUseBloodHand &&
                             TryQueueBloodAttack(boss, bubblegum, gridUid, grid, now);
        if (didBloodAttack)
        {
            MarkPressure(bubblegum, now, "blood-hand", target);
            bubblegum.NextAttack = now + GetScaledCooldown(bubblegum.RangedCooldown, rage);
            bubblegum.NextBloodReaction = now + bubblegum.BloodReactionCooldown;
            return;
        }

        var warped = false;
        if (!didBloodAttack)
        {
            var sprayTarget = belowHalf && _participants.Count > 1 && _random.Prob(0.35f)
                ? PickSecondaryTarget(bubblegum, target, gridUid, grid, now) ?? target
                : target;
            QueueBloodSpray(boss, bubblegum, gridUid, grid, sprayTarget, rage, now);
            warped = TryBloodWarp(boss, bubblegum, gridUid, grid, target);
            if (warped)
                MarkPressure(bubblegum, now, "blood-warp", target);
        }

        var shouldSummon = forcePressure || !_random.Prob(Math.Clamp((88f - rage) / 100f, 0f, 1f));
        if (shouldSummon &&
            TrySummonSlaughterlings(boss, bubblegum, gridUid, grid, now, out var summonedFullWave) &&
            summonedFullWave)
        {
            MarkPressure(bubblegum, now, "summon", target);
            bubblegum.BusyUntil = now + TimeSpan.FromSeconds(0.6);
            bubblegum.NextAttack = now + GetScaledCooldown(bubblegum.RangedCooldown, rage);
            return;
        }

        if (belowHalf)
        {
            var tripleChargeChance = healthFraction <= 0.25f ? 0.9f : 0.78f;
            if (_random.Prob(tripleChargeChance) || warped)
                StartCharge(boss, bubblegum, gridUid, grid, target, bubblegum.TripleChargeSteps, 2, now);
            else
            {
                TryBloodWarp(boss, bubblegum, gridUid, grid, target);
                StartCharge(boss, bubblegum, gridUid, grid, target, bubblegum.ChargeMaxSteps, 0, now);
            }
        }
        else
        {
            StartCharge(boss, bubblegum, gridUid, grid, target, bubblegum.ChargeMaxSteps, 0, now);
        }

        MarkPressure(bubblegum, now, belowHalf ? "triple-charge" : "charge", target);
        bubblegum.NextAttack = now + GetScaledCooldown(bubblegum.RangedCooldown, rage);
    }

    private void RunMovementCombo(
        EntityUid boss,
        BubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        EntityUid target,
        float rage,
        float healthFraction,
        TimeSpan now)
    {
        QueueBloodSpray(boss, bubblegum, gridUid, grid, target, rage, now);

        var warped = false;
        if (healthFraction <= 0.5f || _random.Prob(0.35f))
            warped = TryBloodWarp(boss, bubblegum, gridUid, grid, target);

        var belowHalf = healthFraction <= 0.5f;
        if (belowHalf)
        {
            var extraCharges = healthFraction <= 0.25f ? 2 : 1;
            StartCharge(boss, bubblegum, gridUid, grid, target, bubblegum.TripleChargeSteps, extraCharges, now);
        }
        else
        {
            StartCharge(boss, bubblegum, gridUid, grid, target, bubblegum.ChargeMaxSteps, 0, now);
        }

        MarkPressure(bubblegum, now, warped ? "movement-warp-charge" : "movement-charge", target);
        bubblegum.NextAttack = now + GetScaledCooldown(bubblegum.RangedCooldown, rage);
    }

    private bool TryQueueBloodAttack(
        EntityUid boss,
        BubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        _bloodTargets.Clear();
        foreach (var participant in _participants)
        {
            var tile = GetEntityTile(participant, gridUid, grid);
            if (tile != null && HasBloodWithin(gridUid, grid, tile.Value, 1))
                _bloodTargets.Add(participant);
        }

        if (_bloodTargets.Count == 0)
            return false;

        var attacks = Math.Min(GetBloodHandAttackLimit(boss), _bloodTargets.Count);
        var rightHand = _random.Prob(0.5f);
        var latestAttack = now;
        for (var i = 0; i < attacks; i++)
        {
            var target = _random.PickAndTake(_bloodTargets);
            var tile = GetEntityTile(target, gridUid, grid);
            if (tile == null)
                continue;

            var grabChance = IsBelowHalfHealth(boss)
                ? bubblegum.BloodGrabChanceBelowHalf
                : bubblegum.BloodGrabChance;
            var grab = (TryComp<MobStateComponent>(target, out var targetMobState) &&
                targetMobState.CurrentState != MobState.Alive) || _random.Prob(Math.Clamp(grabChance, 0f, 1f));
            QueueHandAttack(bubblegum, gridUid, grid, tile.Value, now, grab, rightHand);
            QueueCloneHandAttacks(boss, bubblegum, gridUid, grid, tile.Value, now, grab);
            MarkTargetPressure(bubblegum, target, now);
            latestAttack = now + (grab ? bubblegum.BloodGrabDelay : bubblegum.BloodSmackDelay);
            rightHand = !rightHand;
        }

        bubblegum.BusyUntil = latestAttack + TimeSpan.FromSeconds(0.25);
        return true;
    }

    private void QueueHandAttack(
        BubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        TimeSpan now,
        bool grab,
        bool rightHand)
    {
        if (grab)
        {
            SpawnAnchored(rightHand ? bubblegum.RightPawPrototype : bubblegum.LeftPawPrototype, gridUid, grid, tile);
            SpawnAnchored(rightHand ? bubblegum.RightThumbPrototype : bubblegum.LeftThumbPrototype, gridUid, grid, tile);
        }
        else
        {
            SpawnAnchored(rightHand ? bubblegum.RightSmackPrototype : bubblegum.LeftSmackPrototype, gridUid, grid, tile);
        }

        bubblegum.PendingHandAttacks.Add(new BubblegumPendingHandAttack
        {
            Grid = gridUid,
            Tile = tile,
            AttackAt = now + (grab ? bubblegum.BloodGrabDelay : bubblegum.BloodSmackDelay),
            Grab = grab,
            RightHand = rightHand,
        });
    }

    private void ProcessPendingHandAttacks(
        EntityUid boss,
        BubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        for (var i = bubblegum.PendingHandAttacks.Count - 1; i >= 0; i--)
        {
            var pending = bubblegum.PendingHandAttacks[i];
            if (pending.AttackAt > now)
                continue;

            if (pending.Grid == gridUid)
                DamageHandTile(boss, bubblegum, gridUid, grid, pending.Tile, pending.Grab);

            bubblegum.PendingHandAttacks.RemoveAt(i);
        }
    }

    private void DamageHandTile(
        EntityUid boss,
        BubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        bool grab)
    {
        var bossTile = GetEntityTile(boss, gridUid, grid);
        var hit = false;
        var query = EntityQueryEnumerator<DamageableComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var damageable, out var mobState, out var xform))
        {
            if (uid == boss ||
                bubblegum.Slaughterlings.Contains(uid) ||
                mobState.CurrentState == MobState.Dead ||
                xform.GridUid != gridUid ||
                _map.LocalToTile(gridUid, grid, xform.Coordinates) != tile)
            {
                continue;
            }

            _damageable.TryChangeDamage(uid, grab ? bubblegum.GrabDamage : bubblegum.SmackDamage, origin: boss);
            hit = true;

            if (!grab || bossTile == null)
                continue;

            var direction = StepTowards(bossTile.Value, tile) - bossTile.Value;
            if (direction == Vector2i.Zero)
                direction = _random.Pick(Cardinals);

            var destination = ClampToCombatArea(bubblegum, bossTile.Value + direction);
            _audio.PlayPvs(bubblegum.EnterBloodSound, uid, AudioParams.Default.WithVolume(-3f));
            _transform.SetCoordinates(uid, _map.GridTileToLocal(gridUid, grid, destination));
            _audio.PlayPvs(bubblegum.ExitBloodSound, uid, AudioParams.Default.WithVolume(-3f));
        }

        if (hit)
            _audio.PlayPvs(bubblegum.AttackSound, _map.GridTileToLocal(gridUid, grid, tile), AudioParams.Default.WithVolume(-2f));
    }

    private void QueueBloodSpray(
        EntityUid boss,
        BubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        EntityUid target,
        float rage,
        TimeSpan now)
    {
        var bossTile = GetEntityTile(boss, gridUid, grid);
        var targetTile = GetEntityTile(target, gridUid, grid);
        if (bossTile == null || targetTile == null)
            return;

        var direction = StepTowards(bossTile.Value, targetTile.Value) - bossTile.Value;
        if (direction == Vector2i.Zero)
            direction = _random.Pick(Cardinals);

        TrySpillBlood(bubblegum, gridUid, grid, bossTile.Value);
        var range = Math.Max(1, bubblegum.BloodSprayBaseRange + (int) MathF.Round(rage * bubblegum.BloodSprayRageRangeMultiplier));
        for (var step = 1; step <= range; step++)
        {
            if (bubblegum.PendingBloodTiles.Count >= Math.Max(0, bubblegum.MaxPendingBloodTiles))
                break;

            var tile = bossTile.Value + direction * step;
            if (!IsInsideCombatArea(bubblegum, tile))
                break;

            if (HasBloodAtTile(gridUid, grid, tile) || HasPendingBloodTile(bubblegum, gridUid, tile))
                continue;

            bubblegum.PendingBloodTiles.Add(new BubblegumPendingBloodTile
            {
                Grid = gridUid,
                Tile = tile,
                SpawnAt = now + TimeSpan.FromSeconds(bubblegum.BloodSprayStepDelay.TotalSeconds * step),
            });
        }

        QueueCloneBloodSprays(boss, bubblegum, gridUid, grid, bossTile.Value, targetTile.Value, rage, now);
        _audio.PlayPvs(bubblegum.SplatSound, boss, AudioParams.Default.WithVolume(-2f));
    }

    private void ProcessPendingBloodTiles(
        BubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        var playedSound = false;
        for (var i = bubblegum.PendingBloodTiles.Count - 1; i >= 0; i--)
        {
            var pending = bubblegum.PendingBloodTiles[i];
            if (pending.SpawnAt > now)
                continue;

            if (pending.Grid == gridUid && IsInsideCombatArea(bubblegum, pending.Tile))
            {
                if (!pending.Fake)
                    TrySpillBlood(bubblegum, gridUid, grid, pending.Tile);

                if (pending.Fake || _random.Prob(0.65f))
                    SpawnAnchored(bubblegum.BloodSplatterPrototype, gridUid, grid, pending.Tile);

                if (!playedSound)
                {
                    _audio.PlayPvs(bubblegum.SplatSound, _map.GridTileToLocal(gridUid, grid, pending.Tile), AudioParams.Default.WithVolume(-5f));
                    playedSound = true;
                }
            }

            bubblegum.PendingBloodTiles.RemoveAt(i);
        }
    }

    private bool QueueBloodPressureAtTarget(
        EntityUid boss,
        BubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i targetTile,
        TimeSpan now,
        bool urgent)
    {
        if (!IsInsideCombatArea(bubblegum, targetTile))
            return false;

        var queued = TrySpillBlood(bubblegum, gridUid, grid, targetTile);
        var maxAdjacent = urgent ? 4 : 2;
        var addedAdjacent = 0;

        foreach (var direction in Cardinals)
        {
            if (addedAdjacent >= maxAdjacent ||
                bubblegum.PendingBloodTiles.Count >= Math.Max(0, bubblegum.MaxPendingBloodTiles))
            {
                break;
            }

            var tile = targetTile + direction;
            if (!IsInsideCombatArea(bubblegum, tile) ||
                HasBloodAtTile(gridUid, grid, tile) ||
                HasPendingBloodTile(bubblegum, gridUid, tile))
            {
                continue;
            }

            bubblegum.PendingBloodTiles.Add(new BubblegumPendingBloodTile
            {
                Grid = gridUid,
                Tile = tile,
                SpawnAt = now + TimeSpan.FromSeconds(0.06 * (addedAdjacent + 1)),
            });
            addedAdjacent++;
            queued = true;
        }

        if (bubblegum.PendingHandAttacks.Count < 12)
        {
            var grabChance = urgent ? bubblegum.BloodGrabChanceBelowHalf : bubblegum.BloodGrabChance;
            var grab = _random.Prob(Math.Clamp(grabChance, 0f, 1f));
            QueueHandAttack(
                bubblegum,
                gridUid,
                grid,
                targetTile,
                now + (urgent ? TimeSpan.Zero : TimeSpan.FromSeconds(0.12)),
                grab,
                _random.Prob(0.5f));
            QueueCloneHandAttacks(boss, bubblegum, gridUid, grid, targetTile, now, grab);
            queued = true;
        }

        if (queued)
            _audio.PlayPvs(bubblegum.SplatSound, _map.GridTileToLocal(gridUid, grid, targetTile), AudioParams.Default.WithVolume(-3f));

        return queued;
    }

    internal bool TryBloodWarp(
        EntityUid boss,
        BubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        EntityUid target)
    {
        var bossTile = GetEntityTile(boss, gridUid, grid);
        var targetTile = GetEntityTile(target, gridUid, grid);
        if (bossTile == null ||
            targetTile == null ||
            ChebyshevDistance(bossTile.Value, targetTile.Value) <= 1)
        {
            return false;
        }

        GetPoolsAround(gridUid, grid, bossTile.Value, 1, _poolTiles);
        if (_poolTiles.Count == 0)
            return false;

        GetPoolsAround(gridUid, grid, targetTile.Value, 2, _poolTiles);
        for (var i = _poolTiles.Count - 1; i >= 0; i--)
        {
            if (ChebyshevDistance(_poolTiles[i], targetTile.Value) <= 1)
                _poolTiles.RemoveAt(i);
        }

        if (_poolTiles.Count == 0)
            return false;

        var destination = _random.Pick(_poolTiles);
        destination = ClampToCombatArea(bubblegum, destination);

        _audio.PlayPvs(bubblegum.EnterBloodSound, boss, AudioParams.Default.WithVolume(-2f));
        SpawnAnchored(bubblegum.BloodSplatterPrototype, gridUid, grid, bossTile.Value);
        _transform.SetCoordinates(boss, _map.GridTileToLocal(gridUid, grid, destination));
        SpawnAnchored(bubblegum.BloodSplatterPrototype, gridUid, grid, destination);
        _audio.PlayPvs(bubblegum.ExitBloodSound, boss, AudioParams.Default.WithVolume(-2f));
        StartEnrage(boss, bubblegum, gridUid, grid, bossTile.Value, destination);
        return true;
    }

    private bool TrySummonSlaughterlings(
        EntityUid boss,
        BubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now,
        out bool summonedFullWave)
    {
        summonedFullWave = false;
        if (now < bubblegum.NextSummon)
            return false;

        PruneTracked(bubblegum);
        var maxActive = Math.Max(0, bubblegum.MaxActiveSlaughterlings);
        if (maxActive == 0)
            return false;

        var active = bubblegum.Slaughterlings.Count;
        if (active >= maxActive)
            return false;

        var bossTile = GetEntityTile(boss, gridUid, grid);
        if (bossTile == null)
            return false;

        GetPoolsAround(gridUid, grid, bossTile.Value, 1, _poolTiles);
        _random.Shuffle(_poolTiles);

        var limit = Math.Min(
            Math.Max(0, bubblegum.MaxSummonsPerCast),
            Math.Max(0, maxActive - active));
        if (limit <= 0)
            return false;

        var spawned = 0;
        foreach (var tile in _poolTiles)
        {
            if (spawned >= limit)
                break;

            if (!IsInsideCombatArea(bubblegum, tile))
                continue;

            var summon = Spawn(bubblegum.SlaughterlingPrototype, _map.GridTileToLocal(gridUid, grid, tile));
            bubblegum.Slaughterlings.Add(summon);
            spawned++;
        }

        if (spawned <= 0)
            return false;

        summonedFullWave = spawned >= Math.Max(1, bubblegum.MaxSummonsPerCast);
        bubblegum.NextSummon = now + bubblegum.SummonCooldown;
        _audio.PlayPvs(bubblegum.SplatSound, _map.GridTileToLocal(gridUid, grid, bossTile.Value), AudioParams.Default.WithVolume(-1f));
        return true;
    }

    private void StartCharge(
        EntityUid boss,
        BubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        EntityUid target,
        int steps,
        int extraCharges,
        TimeSpan now)
    {
        var bossTile = GetEntityTile(boss, gridUid, grid);
        var targetTile = GetEntityTile(target, gridUid, grid);
        if (bossTile == null || targetTile == null)
            return;

        var destination = ClampToCombatArea(bubblegum, targetTile.Value);
        SpawnAnchored(bubblegum.LandingPrototype, gridUid, grid, destination);
        StartCloneCharges(boss, bubblegum, gridUid, grid, bossTile.Value, destination, steps, now);
        bossTile = GetEntityTile(boss, gridUid, grid);
        if (bossTile == null)
            return;

        bubblegum.Charging = true;
        bubblegum.ChargeTargetTile = destination;
        bubblegum.ChargeRemainingSteps = Math.Max(1, Math.Min(Math.Max(1, steps), Math.Max(1, ChebyshevDistance(bossTile.Value, destination))));
        bubblegum.NextChargeStep = now + bubblegum.ChargeWindup;
        bubblegum.PendingCharges = Math.Max(0, extraCharges);
        bubblegum.PendingChargeSteps = Math.Max(1, steps);
        bubblegum.NextQueuedCharge = TimeSpan.Zero;
        bubblegum.ChargeHitEntities.Clear();
        bubblegum.LastMovementAt = now;
        bubblegum.BusyUntil = now + bubblegum.ChargeWindup + TimeSpan.FromSeconds(bubblegum.ChargeStepDelay.TotalSeconds * bubblegum.ChargeRemainingSteps) + bubblegum.ChargeRecover;

        _movement.RefreshMovementSpeedModifiers(boss);
        EnableChargeTrail(boss, bubblegum);
    }

    private bool ProcessCharge(
        EntityUid boss,
        BubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        if (!bubblegum.Charging)
            return false;

        if (now < bubblegum.NextChargeStep)
            return true;

        var currentTile = GetEntityTile(boss, gridUid, grid);
        if (currentTile == null ||
            currentTile.Value == bubblegum.ChargeTargetTile ||
            bubblegum.ChargeRemainingSteps <= 0)
        {
            FinishCharge(boss, bubblegum, gridUid, grid, now);
            return true;
        }

        var nextTile = StepTowards(currentTile.Value, bubblegum.ChargeTargetTile);
        if (!IsInsideCombatArea(bubblegum, nextTile))
        {
            FinishCharge(boss, bubblegum, gridUid, grid, now);
            return true;
        }

        TrySpillBlood(bubblegum, gridUid, grid, currentTile.Value);
        TrySpillBlood(bubblegum, gridUid, grid, nextTile);
        var chargeDirection = nextTile - currentTile.Value;
        _transform.SetCoordinates(boss, _map.GridTileToLocal(gridUid, grid, nextTile));

        var hit = DamageChargeTile(boss, bubblegum, gridUid, grid, nextTile, chargeDirection);
        bubblegum.ChargeRemainingSteps--;
        bubblegum.NextChargeStep = now + bubblegum.ChargeStepDelay;

        if (hit ||
            nextTile == bubblegum.ChargeTargetTile ||
            bubblegum.ChargeRemainingSteps <= 0)
        {
            FinishCharge(boss, bubblegum, gridUid, grid, bubblegum.NextChargeStep);
        }

        return true;
    }

    private bool ProcessQueuedCharge(
        EntityUid boss,
        BubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        if (bubblegum.PendingCharges <= 0)
            return false;

        if (now < bubblegum.NextQueuedCharge)
            return true;

        var target = PickTarget(bubblegum, boss, gridUid, grid, now);
        if (target == null)
        {
            bubblegum.PendingCharges = 0;
            return false;
        }

        var remaining = Math.Max(0, bubblegum.PendingCharges - 1);
        StartCharge(boss, bubblegum, gridUid, grid, target.Value, bubblegum.PendingChargeSteps, remaining, now);
        MarkTargetPressure(bubblegum, target.Value, now);
        return true;
    }

    private bool DamageChargeTile(
        EntityUid boss,
        BubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        Vector2i chargeDirection)
    {
        var hit = false;
        var query = EntityQueryEnumerator<DamageableComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var damageable, out var mobState, out var xform))
        {
            if (uid == boss ||
                bubblegum.Slaughterlings.Contains(uid) ||
                bubblegum.ChargeHitEntities.Contains(uid) ||
                mobState.CurrentState == MobState.Dead ||
                xform.GridUid != gridUid ||
                _map.LocalToTile(gridUid, grid, xform.Coordinates) != tile)
            {
                continue;
            }

            _damageable.TryChangeDamage(uid, bubblegum.ChargeDamage, origin: boss);
            bubblegum.ChargeHitEntities.Add(uid);
            hit = true;

            var direction = new Vector2(chargeDirection.X, chargeDirection.Y);
            if (direction.LengthSquared() < 0.01f)
                direction = _random.NextVector2();

            _throwing.TryThrow(uid, direction.Normalized() * 2.5f, bubblegum.ChargeThrowSpeed, boss, playSound: false, doSpin: false);
        }

        if (hit)
            _audio.PlayPvs(bubblegum.ImpactSound, _map.GridTileToLocal(gridUid, grid, tile), AudioParams.Default.WithVolume(0f));

        return hit;
    }

    private void FinishCharge(
        EntityUid boss,
        BubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        var tile = GetEntityTile(boss, gridUid, grid) ?? Vector2i.Zero;
        _audio.PlayPvs(bubblegum.ImpactSound, _map.GridTileToLocal(gridUid, grid, tile), AudioParams.Default.WithVolume(-2f));

        bubblegum.Charging = false;
        bubblegum.ChargeHitEntities.Clear();
        _movement.RefreshMovementSpeedModifiers(boss);

        if (bubblegum.PendingCharges > 0)
        {
            bubblegum.NextQueuedCharge = now + bubblegum.ChainedChargeDelay;
            bubblegum.BusyUntil = bubblegum.NextQueuedCharge + bubblegum.ChargeWindup;
        }
        else
        {
            bubblegum.NextQueuedCharge = TimeSpan.Zero;
            bubblegum.BusyUntil = now + bubblegum.ChargeRecover;
            DisableChargeTrail(boss, bubblegum);
        }
    }

    private void ProcessActiveClones(BubblegumComponent bubblegum, TimeSpan now)
    {
        for (var i = bubblegum.ActiveClones.Count - 1; i >= 0; i--)
        {
            var clone = bubblegum.ActiveClones[i];
            if (clone.DespawnAt > now && Exists(clone.Entity))
                continue;

            if (Exists(clone.Entity))
                QueueDel(clone.Entity);

            bubblegum.ActiveClones.RemoveAt(i);
        }
    }

    private void ProcessCloneCharges(
        BubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        for (var i = bubblegum.CloneCharges.Count - 1; i >= 0; i--)
        {
            var charge = bubblegum.CloneCharges[i];
            if (!Exists(charge.Entity))
            {
                bubblegum.CloneCharges.RemoveAt(i);
                continue;
            }

            if (now < charge.NextStep)
                continue;

            var currentTile = GetEntityTile(charge.Entity, gridUid, grid);
            if (currentTile == null ||
                currentTile.Value == charge.TargetTile ||
                charge.RemainingSteps <= 0)
            {
                FinishCloneCharge(bubblegum, charge.Entity, gridUid, grid, currentTile ?? charge.TargetTile);
                bubblegum.CloneCharges.RemoveAt(i);
                continue;
            }

            var nextTile = StepTowards(currentTile.Value, charge.TargetTile);
            if (!IsInsideCombatArea(bubblegum, nextTile))
            {
                FinishCloneCharge(bubblegum, charge.Entity, gridUid, grid, currentTile.Value);
                bubblegum.CloneCharges.RemoveAt(i);
                continue;
            }

            if (_random.Prob(0.65f))
                SpawnAnchored(bubblegum.BloodSplatterPrototype, gridUid, grid, currentTile.Value);

            SpawnAnchored(bubblegum.BloodSplatterPrototype, gridUid, grid, nextTile);
            _transform.SetCoordinates(charge.Entity, _map.GridTileToLocal(gridUid, grid, nextTile));

            charge.RemainingSteps--;
            charge.NextStep = now + bubblegum.ChargeStepDelay;
        }
    }

    private void QueueCloneHandAttacks(
        EntityUid boss,
        BubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i targetTile,
        TimeSpan now,
        bool grab)
    {
        if (!ShouldUseClones(boss, bubblegum))
            return;

        var count = Math.Min(2, GetCloneCount(boss, bubblegum));
        PickCloneTiles(bubblegum, targetTile, count, _cloneTiles);

        var swapped = false;
        foreach (var cloneTile in _cloneTiles)
        {
            var fakeTarget = ClampToCombatArea(bubblegum, targetTile + new Vector2i(_random.Next(-3, 4), _random.Next(-3, 4)));
            if (fakeTarget == targetTile)
                fakeTarget = ClampToCombatArea(bubblegum, targetTile + _random.Pick(Cardinals));

            var clone = SpawnClone(bubblegum, gridUid, grid, cloneTile, now, GetBloodReactionWindow(bubblegum) + bubblegum.CloneLinger);
            if (clone == null)
                continue;

            if (!swapped)
                swapped = TrySwapWithClone(boss, bubblegum, gridUid, grid, clone.Value, now);

            SpawnFakeHandVisual(bubblegum, gridUid, grid, fakeTarget, grab, _random.Prob(0.5f));
        }
    }

    private void QueueCloneBloodSprays(
        EntityUid boss,
        BubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i sourceTile,
        Vector2i targetTile,
        float rage,
        TimeSpan now)
    {
        if (!ShouldUseClones(boss, bubblegum))
            return;

        var range = Math.Max(1, bubblegum.BloodSprayBaseRange + (int) MathF.Round(rage * bubblegum.BloodSprayRageRangeMultiplier));
        var duration = TimeSpan.FromSeconds(bubblegum.BloodSprayStepDelay.TotalSeconds * range) + bubblegum.CloneLinger + TimeSpan.FromSeconds(0.35);
        PickCloneTiles(bubblegum, sourceTile, GetCloneCount(boss, bubblegum), _cloneTiles);

        var swapped = false;
        foreach (var cloneTile in _cloneTiles)
        {
            var fakeTarget = ClampToCombatArea(bubblegum, targetTile + new Vector2i(_random.Next(-5, 6), _random.Next(-5, 6)));
            var clone = SpawnClone(bubblegum, gridUid, grid, cloneTile, now, duration);
            if (clone == null)
                continue;

            if (!swapped)
                swapped = TrySwapWithClone(boss, bubblegum, gridUid, grid, clone.Value, now);

            var currentCloneTile = GetEntityTile(clone.Value, gridUid, grid) ?? cloneTile;
            QueueFakeBloodSpray(bubblegum, gridUid, grid, currentCloneTile, fakeTarget, range, now);
        }
    }

    private void StartCloneCharges(
        EntityUid boss,
        BubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i sourceTile,
        Vector2i targetTile,
        int steps,
        TimeSpan now)
    {
        if (!ShouldUseClones(boss, bubblegum))
            return;

        GenerateSurroundTiles(bubblegum, targetTile, GetCloneCount(boss, bubblegum), _cloneTiles);
        var chargeDuration = bubblegum.ChargeWindup +
                             TimeSpan.FromSeconds(bubblegum.ChargeStepDelay.TotalSeconds * Math.Max(1, steps)) +
                             bubblegum.CloneLinger + TimeSpan.FromSeconds(0.5);

        var swapped = false;
        foreach (var cloneTile in _cloneTiles)
        {
            var fakeTarget = ClampToCombatArea(bubblegum, targetTile + new Vector2i(_random.Next(-7, 8), _random.Next(-7, 8)));
            if (fakeTarget == cloneTile)
                fakeTarget = ClampToCombatArea(bubblegum, cloneTile + _random.Pick(Cardinals) * Math.Max(1, bubblegum.CloneMinOffset));

            var clone = SpawnClone(bubblegum, gridUid, grid, cloneTile, now, chargeDuration);
            if (clone == null)
                continue;

            if (!swapped)
                swapped = TrySwapWithClone(boss, bubblegum, gridUid, grid, clone.Value, now);

            var currentCloneTile = GetEntityTile(clone.Value, gridUid, grid) ?? cloneTile;
            SpawnAnchored(bubblegum.LandingPrototype, gridUid, grid, fakeTarget);

            bubblegum.CloneCharges.Add(new BubblegumCloneCharge
            {
                Entity = clone.Value,
                TargetTile = fakeTarget,
                RemainingSteps = Math.Max(1, Math.Min(Math.Max(1, steps), Math.Max(1, ChebyshevDistance(currentCloneTile, fakeTarget)))),
                NextStep = now + bubblegum.ChargeWindup,
            });
        }
    }

    private void QueueFakeBloodSpray(
        BubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i sourceTile,
        Vector2i targetTile,
        int range,
        TimeSpan now)
    {
        var direction = StepTowards(sourceTile, targetTile) - sourceTile;
        if (direction == Vector2i.Zero)
            direction = _random.Pick(Cardinals);

        SpawnAnchored(bubblegum.BloodSplatterPrototype, gridUid, grid, sourceTile);

        for (var step = 1; step <= range; step++)
        {
            if (bubblegum.PendingBloodTiles.Count >= Math.Max(0, bubblegum.MaxPendingBloodTiles))
                break;

            var tile = sourceTile + direction * step;
            if (!IsInsideCombatArea(bubblegum, tile))
                break;

            if (HasPendingBloodTile(bubblegum, gridUid, tile))
                continue;

            bubblegum.PendingBloodTiles.Add(new BubblegumPendingBloodTile
            {
                Grid = gridUid,
                Tile = tile,
                SpawnAt = now + TimeSpan.FromSeconds(bubblegum.BloodSprayStepDelay.TotalSeconds * step),
                Fake = true,
            });
        }
    }

    private void SpawnFakeHandVisual(
        BubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        bool grab,
        bool rightHand)
    {
        if (grab)
        {
            SpawnAnchored(rightHand ? bubblegum.RightPawPrototype : bubblegum.LeftPawPrototype, gridUid, grid, tile);
            SpawnAnchored(rightHand ? bubblegum.RightThumbPrototype : bubblegum.LeftThumbPrototype, gridUid, grid, tile);
            return;
        }

        SpawnAnchored(rightHand ? bubblegum.RightSmackPrototype : bubblegum.LeftSmackPrototype, gridUid, grid, tile);
    }

    private EntityUid? SpawnClone(
        BubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        TimeSpan now,
        TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(bubblegum.ClonePrototype) ||
            !_prototype.HasIndex<EntityPrototype>(bubblegum.ClonePrototype))
        {
            return null;
        }

        var clone = Spawn(bubblegum.ClonePrototype, _map.GridTileToLocal(gridUid, grid, tile));
        bubblegum.ActiveClones.Add(new BubblegumActiveClone
        {
            Entity = clone,
            DespawnAt = now + duration,
        });

        return clone;
    }

    private bool TrySwapWithClone(
        EntityUid boss,
        BubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        EntityUid clone,
        TimeSpan now)
    {
        if (bubblegum.Charging ||
            now < bubblegum.NextCloneSwap ||
            !ShouldUseClones(boss, bubblegum) ||
            !_random.Prob(GetCloneSwapChance(boss, bubblegum)))
        {
            return false;
        }

        var bossTile = GetEntityTile(boss, gridUid, grid);
        var cloneTile = GetEntityTile(clone, gridUid, grid);
        if (bossTile == null ||
            cloneTile == null ||
            bossTile.Value == cloneTile.Value ||
            !IsInsideCombatArea(bubblegum, bossTile.Value) ||
            !IsInsideCombatArea(bubblegum, cloneTile.Value))
        {
            return false;
        }

        SpawnAnchored(bubblegum.BloodSplatterPrototype, gridUid, grid, bossTile.Value);
        SpawnAnchored(bubblegum.BloodSplatterPrototype, gridUid, grid, cloneTile.Value);

        _audio.PlayPvs(bubblegum.EnterBloodSound, _map.GridTileToLocal(gridUid, grid, bossTile.Value), AudioParams.Default.WithVolume(-3f));
        _transform.SetCoordinates(boss, _map.GridTileToLocal(gridUid, grid, cloneTile.Value));
        _transform.SetCoordinates(clone, _map.GridTileToLocal(gridUid, grid, bossTile.Value));
        _audio.PlayPvs(bubblegum.ExitBloodSound, _map.GridTileToLocal(gridUid, grid, cloneTile.Value), AudioParams.Default.WithVolume(-3f));

        bubblegum.NextCloneSwap = now + bubblegum.CloneSwapCooldown;
        return true;
    }

    private void FinishCloneCharge(
        BubblegumComponent bubblegum,
        EntityUid clone,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile)
    {
        SpawnAnchored(bubblegum.BloodSplatterPrototype, gridUid, grid, tile);
        _audio.PlayPvs(bubblegum.ImpactSound, _map.GridTileToLocal(gridUid, grid, tile), AudioParams.Default.WithVolume(-5f));
        DeleteClone(bubblegum, clone);
    }

    private void DeleteClone(BubblegumComponent bubblegum, EntityUid clone)
    {
        for (var i = bubblegum.ActiveClones.Count - 1; i >= 0; i--)
        {
            if (bubblegum.ActiveClones[i].Entity == clone)
                bubblegum.ActiveClones.RemoveAt(i);
        }

        if (Exists(clone))
            QueueDel(clone);
    }

    /// <summary>
    /// TG hallucination surround: clone tiles are spread evenly on a circle of
    /// radius 6-8 around the target instead of scattered near the boss.
    /// </summary>
    private void GenerateSurroundTiles(
        BubblegumComponent bubblegum,
        Vector2i targetTile,
        int count,
        List<Vector2i> output)
    {
        output.Clear();
        var radius = _random.Next(bubblegum.CloneSurroundMinRadius, bubblegum.CloneSurroundMaxRadius + 1);
        for (var i = 0; i < count; i++)
        {
            var angle = MathF.Tau * i / Math.Max(1, count);
            var offset = new Vector2i(
                (int) MathF.Round(MathF.Cos(angle) * radius),
                (int) MathF.Round(MathF.Sin(angle) * radius));
            output.Add(ClampToCombatArea(bubblegum, targetTile + offset));
        }
    }

    private void PickCloneTiles(
        BubblegumComponent bubblegum,
        Vector2i origin,
        int count,
        List<Vector2i> output)
    {
        output.Clear();
        count = Math.Max(0, count);
        if (count == 0)
            return;

        var minOffset = Math.Max(1, bubblegum.CloneMinOffset);
        var maxOffset = Math.Max(minOffset, bubblegum.CloneMaxOffset);

        for (var attempt = 0; attempt < count * 20 && output.Count < count; attempt++)
        {
            var offset = new Vector2i(_random.Next(-maxOffset, maxOffset + 1), _random.Next(-maxOffset, maxOffset + 1));
            if (offset == Vector2i.Zero ||
                Math.Max(Math.Abs(offset.X), Math.Abs(offset.Y)) < minOffset)
            {
                continue;
            }

            var tile = ClampToCombatArea(bubblegum, origin + offset);
            if (tile == origin || output.Contains(tile))
                continue;

            output.Add(tile);
        }

        foreach (var direction in Cardinals)
        {
            if (output.Count >= count)
                break;

            var tile = ClampToCombatArea(bubblegum, origin + direction * minOffset);
            if (tile != origin && !output.Contains(tile))
                output.Add(tile);
        }
    }

    private bool TrySpillBlood(
        BubblegumComponent bubblegum,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile)
    {
        if (!IsInsideCombatArea(bubblegum, tile) ||
            HasBloodAtTile(gridUid, grid, tile) ||
            bubblegum.BloodSpillVolume <= FixedPoint2.Zero ||
            string.IsNullOrWhiteSpace(bubblegum.BloodReagent))
        {
            return false;
        }

        PruneTracked(bubblegum);
        if (bubblegum.SpilledPuddles.Count >= Math.Max(1, bubblegum.MaxBloodPools))
        {
            var oldest = bubblegum.SpilledPuddles[0];
            bubblegum.SpilledPuddles.RemoveAt(0);
            if (Exists(oldest))
                QueueDel(oldest);
        }

        var solution = new Solution(bubblegum.BloodReagent, bubblegum.BloodSpillVolume);
        var tileRef = _map.GetTileRef(gridUid, grid, tile);
        if (!_puddle.TrySpillAt(tileRef, solution, out var puddleUid, sound: false))
            return false;

        if (!bubblegum.SpilledPuddles.Contains(puddleUid))
            bubblegum.SpilledPuddles.Add(puddleUid);

        return true;
    }

    private void SpawnAnchored(string prototype, EntityUid gridUid, MapGridComponent grid, Vector2i index)
    {
        if (string.IsNullOrWhiteSpace(prototype) ||
            !_prototype.HasIndex<EntityPrototype>(prototype))
        {
            return;
        }

        var uid = Spawn(prototype, _map.GridTileToLocal(gridUid, grid, index));
        if (!TryComp(uid, out TransformComponent? xform) || xform.Anchored)
            return;

        _transform.AnchorEntity((uid, xform), (gridUid, grid), index);
    }

    private void GetPoolsAround(EntityUid gridUid, MapGridComponent grid, Vector2i center, int range, List<Vector2i> output)
    {
        output.Clear();
        for (var x = -range; x <= range; x++)
        {
            for (var y = -range; y <= range; y++)
            {
                var tile = center + new Vector2i(x, y);
                if (HasBloodAtTile(gridUid, grid, tile) && !output.Contains(tile))
                    output.Add(tile);
            }
        }
    }

    private bool HasBloodAtTile(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var tileRef = _map.GetTileRef(gridUid, grid, tile);
        return _puddle.TryGetPuddle(tileRef, out var puddleUid) && IsBloodPuddle(puddleUid);
    }

    private bool HasBloodWithin(EntityUid gridUid, MapGridComponent grid, Vector2i tile, int range)
    {
        for (var x = -range; x <= range; x++)
        {
            for (var y = -range; y <= range; y++)
            {
                if (HasBloodAtTile(gridUid, grid, tile + new Vector2i(x, y)))
                    return true;
            }
        }

        return false;
    }

    private bool IsBloodPuddle(EntityUid puddleUid)
    {
        if (!TryComp<PuddleComponent>(puddleUid, out var puddle) ||
            !_solutionContainer.ResolveSolution(puddleUid, puddle.SolutionName, ref puddle.Solution, out var solution))
        {
            return false;
        }

        foreach (var reagent in BloodReagents)
        {
            if (solution.ContainsReagent(reagent, null))
                return true;
        }

        return false;
    }

    private bool HasPendingBloodTile(BubblegumComponent bubblegum, EntityUid gridUid, Vector2i tile)
    {
        foreach (var pending in bubblegum.PendingBloodTiles)
        {
            if (pending.Grid == gridUid && pending.Tile == tile)
                return true;
        }

        return false;
    }

    private int CollectParticipants(EntityUid boss, BubblegumComponent bubblegum, EntityUid gridUid, MapGridComponent grid)
    {
        _participants.Clear();

        if (!TryComp<AggressiveComponent>(boss, out var aggressive))
            return 0;

        foreach (var aggressor in aggressive.Aggressors)
        {
            if (!Exists(aggressor) ||
                IsDead(aggressor) ||
                !TryComp(aggressor, out TransformComponent? xform) ||
                xform.GridUid != gridUid)
            {
                continue;
            }

            var tile = GetEntityTile(aggressor, gridUid, grid);
            if (tile == null || !IsInsideCombatArea(bubblegum, tile.Value))
                continue;

            _participants.Add(aggressor);
        }

        return _participants.Count;
    }

    private EntityUid? PickTarget(
        BubblegumComponent bubblegum,
        EntityUid boss,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        if (_participants.Count == 0)
            return null;

        PruneTargetMemory(bubblegum);

        if (_participants.Count == 1)
        {
            SetPrimaryTarget(bubblegum, _participants[0], now);
            return _participants[0];
        }

        if (bubblegum.CurrentPrimaryTarget is { Valid: true } current &&
            _participants.Contains(current) &&
            now - bubblegum.LastTargetSwitchAt < bubblegum.TargetSwitchCooldown)
        {
            return current;
        }

        var bossTile = boss.Valid ? GetEntityTile(boss, gridUid, grid) : null;
        EntityUid? best = null;
        var bestScore = float.MinValue;
        foreach (var participant in _participants)
        {
            var tile = GetEntityTile(participant, gridUid, grid);
            if (tile == null)
                continue;

            var score = ScoreTarget(bubblegum, participant, bossTile, tile.Value, now, true);
            if (score <= bestScore)
                continue;

            best = participant;
            bestScore = score;
        }

        if (best == null)
            best = _random.Pick(_participants);

        SetPrimaryTarget(bubblegum, best.Value, now);
        return best;
    }

    private EntityUid? PickSecondaryTarget(
        BubblegumComponent bubblegum,
        EntityUid excluded,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        if (_participants.Count <= 1)
            return null;

        PruneTargetMemory(bubblegum);

        EntityUid? best = null;
        var bestScore = float.MinValue;
        foreach (var participant in _participants)
        {
            if (participant == excluded)
                continue;

            var tile = GetEntityTile(participant, gridUid, grid);
            if (tile == null)
                continue;

            var score = ScoreTarget(bubblegum, participant, null, tile.Value, now, false);
            if (score <= bestScore)
                continue;

            best = participant;
            bestScore = score;
        }

        return best;
    }

    private EntityUid? PickPressureTarget(
        BubblegumComponent bubblegum,
        EntityUid preferred,
        EntityUid gridUid,
        MapGridComponent grid,
        TimeSpan now)
    {
        if (_participants.Count == 0)
            return null;

        PruneTargetMemory(bubblegum);

        EntityUid? best = null;
        var bestScore = float.MinValue;
        foreach (var participant in _participants)
        {
            var tile = GetEntityTile(participant, gridUid, grid);
            if (tile == null)
                continue;

            var score = GetPressureSafeSeconds(bubblegum, participant, now) + _random.NextFloat(0f, 1.5f);
            if (!HasBloodWithin(gridUid, grid, tile.Value, 1))
                score += 18f;

            if (participant != preferred)
                score += 3f;

            if (score <= bestScore)
                continue;

            best = participant;
            bestScore = score;
        }

        return best;
    }

    private float ScoreTarget(
        BubblegumComponent bubblegum,
        EntityUid target,
        Vector2i? bossTile,
        Vector2i targetTile,
        TimeSpan now,
        bool applyCurrentPenalty)
    {
        var safeSeconds = bubblegum.TargetPressureMemory.TotalSeconds;
        if (bubblegum.LastPressureByTarget.TryGetValue(target, out var lastPressure))
            safeSeconds = Math.Clamp((now - lastPressure).TotalSeconds, 0, bubblegum.TargetPressureMemory.TotalSeconds);

        var distancePenalty = bossTile == null
            ? 0f
            : ChebyshevDistance(bossTile.Value, targetTile) * 0.2f;
        var score = (float) safeSeconds - distancePenalty + _random.NextFloat(0f, 1.5f);

        if (applyCurrentPenalty &&
            bubblegum.CurrentPrimaryTarget == target &&
            now - bubblegum.LastTargetSwitchAt >= bubblegum.TargetSwitchCooldown)
        {
            score -= 8f;
        }

        return score;
    }

    private static float GetPressureSafeSeconds(BubblegumComponent bubblegum, EntityUid target, TimeSpan now)
    {
        if (!bubblegum.LastPressureByTarget.TryGetValue(target, out var lastPressure))
            return (float) bubblegum.TargetPressureMemory.TotalSeconds;

        return (float) Math.Clamp(
            (now - lastPressure).TotalSeconds,
            0,
            bubblegum.TargetPressureMemory.TotalSeconds);
    }

    private void PruneTargetMemory(BubblegumComponent bubblegum)
    {
        if (bubblegum.CurrentPrimaryTarget is { Valid: true } current &&
            !_participants.Contains(current))
        {
            bubblegum.CurrentPrimaryTarget = null;
        }

        if (bubblegum.LastPressureByTarget.Count == 0)
            return;

        foreach (var target in new List<EntityUid>(bubblegum.LastPressureByTarget.Keys))
        {
            if (!_participants.Contains(target))
                bubblegum.LastPressureByTarget.Remove(target);
        }
    }

    private static void SetPrimaryTarget(BubblegumComponent bubblegum, EntityUid target, TimeSpan now)
    {
        if (bubblegum.CurrentPrimaryTarget == target)
            return;

        bubblegum.CurrentPrimaryTarget = target;
        bubblegum.LastTargetSwitchAt = now;
    }

    private void PruneTracked(BubblegumComponent bubblegum)
    {
        for (var i = bubblegum.SpilledPuddles.Count - 1; i >= 0; i--)
        {
            if (!Exists(bubblegum.SpilledPuddles[i]))
                bubblegum.SpilledPuddles.RemoveAt(i);
        }

        for (var i = bubblegum.Slaughterlings.Count - 1; i >= 0; i--)
        {
            var summon = bubblegum.Slaughterlings[i];
            if (!Exists(summon) || IsDead(summon))
                bubblegum.Slaughterlings.RemoveAt(i);
        }
    }

    private void ClearRuntimeState(EntityUid uid, BubblegumComponent bubblegum, bool refreshMovement)
    {
        DisableChargeTrail(uid, bubblegum);
        bubblegum.PendingBloodTiles.Clear();
        bubblegum.PendingHandAttacks.Clear();
        bubblegum.Charging = false;
        bubblegum.PendingCharges = 0;
        bubblegum.NextQueuedCharge = TimeSpan.Zero;
        bubblegum.ChargeHitEntities.Clear();
        bubblegum.CurrentPrimaryTarget = null;
        bubblegum.LastTargetSwitchAt = TimeSpan.Zero;
        bubblegum.LastPressureByTarget.Clear();
        bubblegum.CloneCharges.Clear();
        bubblegum.NextCloneSwap = TimeSpan.Zero;
        bubblegum.LastMovementAt = TimeSpan.Zero;
        RemComp<RandomSpriteComponent>(uid);
        bubblegum.Enraged = false;
        bubblegum.EnrageUntil = TimeSpan.Zero;

        bubblegum.SpilledPuddles.Clear();

        foreach (var summon in bubblegum.Slaughterlings)
        {
            if (Exists(summon))
                QueueDel(summon);
        }

        bubblegum.Slaughterlings.Clear();

        foreach (var clone in bubblegum.ActiveClones)
        {
            if (Exists(clone.Entity))
                QueueDel(clone.Entity);
        }

        bubblegum.ActiveClones.Clear();

        if (refreshMovement && Exists(uid))
            _movement.RefreshMovementSpeedModifiers(uid);
    }

    private static bool NeedsPressure(BubblegumComponent bubblegum, TimeSpan now)
    {
        return bubblegum.LastPressureAt == TimeSpan.Zero ||
               now - bubblegum.LastPressureAt >= bubblegum.ForcePressureAfter;
    }

    private static bool IsPressureStale(
        BubblegumComponent bubblegum,
        EntityUid target,
        TimeSpan now,
        TimeSpan staleAfter)
    {
        return !bubblegum.LastPressureByTarget.TryGetValue(target, out var lastPressure) ||
               now - lastPressure >= staleAfter;
    }

    private bool ShouldPrioritizeMovement(
        BubblegumComponent bubblegum,
        EntityUid boss,
        int targetDistance,
        TimeSpan now)
    {
        var healthFraction = GetHealthFraction(boss);
        var distance = healthFraction <= 0.25f
            ? Math.Max(1, bubblegum.MovementCriticalDistance)
            : Math.Max(1, bubblegum.MovementDistance);
        if (targetDistance < distance)
            return false;

        var cooldown = healthFraction <= 0.25f
            ? bubblegum.MovementCriticalCooldown
            : bubblegum.MovementCooldown;
        if (bubblegum.LastMovementAt != TimeSpan.Zero &&
            now - bubblegum.LastMovementAt < cooldown)
        {
            return false;
        }

        if (targetDistance >= distance + 5)
            return true;

        var chance = healthFraction <= 0.25f
            ? 0.65f
            : healthFraction <= 0.5f
                ? 0.5f
                : 0.35f;

        return _random.Prob(chance);
    }

    private static bool IsRecentPressureAttack(BubblegumComponent bubblegum)
    {
        return bubblegum.LastAttackKind is "blood-pressure" or "forced-blood-pressure";
    }

    private static bool IsRecentBloodHandAttack(BubblegumComponent bubblegum)
    {
        return bubblegum.LastAttackKind is "blood-hand" or "blood-reaction";
    }

    private static void MarkPressure(BubblegumComponent bubblegum, TimeSpan now, string attackKind, EntityUid target)
    {
        bubblegum.LastPressureAt = now;
        bubblegum.LastAttackKind = attackKind;
        MarkTargetPressure(bubblegum, target, now);
    }

    private static void MarkTargetPressure(BubblegumComponent bubblegum, EntityUid target, TimeSpan now)
    {
        if (!target.Valid)
            return;

        bubblegum.LastPressureByTarget[target] = now;
    }

    private float CalculateRage(EntityUid boss)
    {
        if (!TryComp<DamageableComponent>(boss, out var damageable))
            return 0f;

        return Math.Clamp(Math.Max(0f, damageable.TotalDamage.Float()) / 60f, 0f, 20f);
    }

    private int GetBloodHandAttackLimit(EntityUid boss)
    {
        var healthFraction = GetHealthFraction(boss);
        if (healthFraction <= 0.25f)
            return 4;

        return healthFraction <= 0.5f ? 3 : 2;
    }

    private bool ShouldUseClones(EntityUid boss, BubblegumComponent bubblegum)
    {
        return Math.Max(0, bubblegum.CloneCount) > 0 &&
               GetHealthFraction(boss) <= Math.Clamp(bubblegum.CloneHealthThreshold, 0f, 1f);
    }

    private int GetCloneCount(EntityUid boss, BubblegumComponent bubblegum)
    {
        var cloneCount = Math.Max(0, bubblegum.CloneCount);
        if (GetHealthFraction(boss) <= Math.Clamp(bubblegum.CloneCriticalHealthThreshold, 0f, 1f))
            cloneCount = Math.Max(cloneCount, bubblegum.CloneCriticalCount);

        return cloneCount;
    }

    private float GetCloneSwapChance(EntityUid boss, BubblegumComponent bubblegum)
    {
        var healthFraction = GetHealthFraction(boss);
        var chance = healthFraction <= Math.Clamp(bubblegum.CloneCriticalHealthThreshold, 0f, 1f)
            ? bubblegum.CloneSwapCriticalChance
            : bubblegum.CloneSwapChance;

        return Math.Clamp(chance, 0f, 1f);
    }

    private float GetHealthFraction(EntityUid boss)
    {
        if (!TryComp<DamageableComponent>(boss, out var damageable) ||
            !TryComp<MobThresholdsComponent>(boss, out var thresholds) ||
            !_mobThreshold.TryGetThresholdForState(boss, MobState.Dead, out var maxHealth, thresholds) ||
            maxHealth <= FixedPoint2.Zero)
        {
            return 1f;
        }

        var max = maxHealth.Value.Float();
        return Math.Clamp((max - damageable.TotalDamage.Float()) / max, 0f, 1f);
    }

    private bool IsBelowHalfHealth(EntityUid boss)
    {
        if (!TryComp<DamageableComponent>(boss, out var damageable) ||
            !TryComp<MobThresholdsComponent>(boss, out var thresholds) ||
            !_mobThreshold.TryGetThresholdForState(boss, MobState.Dead, out var maxHealth, thresholds))
        {
            return false;
        }

        return damageable.TotalDamage.Float() >= maxHealth.Value.Float() * 0.5f;
    }

    private static TimeSpan GetScaledCooldown(TimeSpan baseCooldown, float rage)
    {
        return TimeSpan.FromSeconds(Math.Max(2.0, baseCooldown.TotalSeconds - rage * 0.045));
    }

    private static TimeSpan GetBloodReactionWindow(BubblegumComponent bubblegum)
    {
        return TimeSpan.FromSeconds(Math.Max(bubblegum.BloodSmackDelay.TotalSeconds, bubblegum.BloodGrabDelay.TotalSeconds)) +
               bubblegum.BloodHandRecover;
    }

    private Vector2i? GetEntityTile(EntityUid uid, EntityUid gridUid, MapGridComponent grid)
    {
        if (!uid.Valid ||
            !TryComp(uid, out TransformComponent? xform) ||
            xform.GridUid != gridUid)
        {
            return null;
        }

        return _map.LocalToTile(gridUid, grid, xform.Coordinates);
    }

    private bool IsDead(EntityUid uid)
    {
        return TryComp(uid, out MobStateComponent? mobState) && mobState.CurrentState == MobState.Dead;
    }

    private static Vector2i StepTowards(Vector2i from, Vector2i to)
    {
        return new Vector2i(
            from.X + Math.Sign(to.X - from.X),
            from.Y + Math.Sign(to.Y - from.Y));
    }

    private static int ChebyshevDistance(Vector2i a, Vector2i b)
    {
        return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    }

    private void OnMapInit(EntityUid uid, BubblegumComponent component, MapInitEvent args)
    {
        if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid is not { } gridUid)
            return;

        component.CombatGrid = gridUid;
        if (TryComp(gridUid, out MapGridComponent? grid))
        {
            var tile = GetEntityTile(uid, gridUid, grid);
            if (tile != null)
                component.CombatOrigin = tile.Value;
        }

        PrepareFight(uid, component);
    }

    private static bool IsInsideCombatArea(BubblegumComponent bubblegum, Vector2i tile)
    {
        return ChebyshevDistance(bubblegum.CombatOrigin, tile) <= bubblegum.CombatRadius;
    }

    private static Vector2i ClampToCombatArea(BubblegumComponent bubblegum, Vector2i tile)
    {
        var delta = tile - bubblegum.CombatOrigin;
        var clamped = new Vector2i(
            Math.Clamp(delta.X, -bubblegum.CombatRadius, bubblegum.CombatRadius),
            Math.Clamp(delta.Y, -bubblegum.CombatRadius, bubblegum.CombatRadius));
        return bubblegum.CombatOrigin + clamped;
    }

    private void EnableChargeTrail(EntityUid uid, BubblegumComponent bubblegum)
    {
        if (bubblegum.ChargeTrail != null)
            return;

        var trail = EnsureComp<TrailComponent>(uid);
        trail.RenderedEntity = uid;
        trail.LerpTime = 0.1f;
        trail.LerpDelay = TimeSpan.FromSeconds(4);
        trail.Lifetime = 10f;
        trail.Frequency = 0.07f;
        trail.AlphaLerpAmount = 0.2f;
        trail.MaxParticleAmount = 25;
        trail.SpawnRemainingTrail = true;
        trail.Color = Color.FromHex("#ff4d4dcc");
        bubblegum.ChargeTrail = trail;
        Dirty(uid, trail);
    }

    private void DisableChargeTrail(EntityUid uid, BubblegumComponent bubblegum)
    {
        if (bubblegum.ChargeTrail == null)
            return;

        RemComp(uid, bubblegum.ChargeTrail);
        bubblegum.ChargeTrail = null;
    }
}
