// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Linq;
using System.Numerics;
using Content.Goobstation.Shared.Fishing;
using Content.Goobstation.Shared.Fishing.Components;
using Content.Goobstation.Shared.Fishing.Events;
using Content.Goobstation.Shared.Fishing.Prototypes;
using Content.Goobstation.Shared.Fishing.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Goobstation.Server.Fishing;

/// <summary>
/// Server side of the tg fishing loop: casting the lure, waiting for a bite and the minigame.
/// </summary>
public sealed partial class FishingSystem : EntitySystem
{
    private const float MinigameTick = 0.05f;
    private const float MinDifficulty = 6f;
    private const float MinigameArea = 1000f;
    private const float BiteWindow = 4f;
    private const float MissedBitePenalty = 4f;
    private const float MaxWaitPenalty = 30f;
    private const float FishFriction = 0.9f;
    private const float FishTargetMinDistance = 6f;

    [Dependency] private FishingSourceSystem _sources = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishingRodComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<FishingRodComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<FishingRodComponent, FishingMinigameReelMessage>(OnReelMessage);
        SubscribeLocalEvent<FishingRodComponent, FishingMinigameStopMessage>(OnStopMessage);
        SubscribeLocalEvent<FishingRodComponent, BoundUIClosedEvent>(OnUiClosed);
        SubscribeLocalEvent<FishingChallengeComponent, ComponentShutdown>(OnChallengeShutdown);
    }

    #region Cast

    private void OnAfterInteract(Entity<FishingRodComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        var user = args.User;
        if (args.Target == user || !_hands.IsHolding(user, ent.Owner))
            return;

        if (ent.Comp.FishingLure != null || HasComp<FishingChallengeComponent>(user))
            return;

        var coords = args.ClickLocation;
        if (args.Target is { } target)
            coords = Transform(target).Coordinates;

        if (!_sources.TryGetSource(coords, args.Target, out var source))
            return;

        var userPos = _xform.GetWorldPosition(user);
        var castPos = _xform.ToWorldPosition(coords);
        if ((userPos - castPos).Length() > ent.Comp.CastRange + 0.5f)
            return;

        var lure = Spawn(ent.Comp.LurePrototype, coords);
        if (!TryComp<FishingLureComponent>(lure, out var lureComp))
            lureComp = EnsureComp<FishingLureComponent>(lure);

        lureComp.Rod = ent.Owner;
        lureComp.SourceEntity = args.Target;
        ent.Comp.FishingLure = lure;
        Dirty(ent, ent.Comp);

        var challenge = EnsureComp<FishingChallengeComponent>(user);
        challenge.Rod = ent.Owner;
        challenge.Lure = lure;
        challenge.Source = source;
        challenge.SourceEntity = args.Target;
        challenge.CastCoordinates = coords;
        challenge.Phase = FishingPhase.Waiting;
        challenge.NextPhaseTime = _timing.CurTime + TimeSpan.FromSeconds(_sources.GetWaitTime(source));
        challenge.IsFinishing = false;
        challenge.ReelDirection = 0;

        if (_proto.TryIndex(source, out FishingSourcePrototype? sourceProto))
            challenge.Background = sourceProto.Background;

        challenge.FrameState = ent.Comp.FrameState;
        Dirty(user, challenge);

        _popup.PopupEntity(Loc.GetString("fishing-start"), user, user);

        EnsureComp<UserInterfaceComponent>(ent);
        if (TryComp<ActorComponent>(user, out var actor))
            _ui.OpenUi(ent.Owner, FishingMinigameUiKey.Key, actor.PlayerSession);

        UpdateUi(ent.Owner, user, challenge);
        args.Handled = true;
    }

    private void OnUseInHand(Entity<FishingRodComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<FishingChallengeComponent>(args.User, out var challenge))
            return;

        // Using the rod in hand reels the line back in.
        StopChallenge(args.User, challenge, false, Loc.GetString("fishing-reeled-in"));
        args.Handled = true;
    }

    #endregion

    #region Update loop

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FishingChallengeComponent>();
        while (query.MoveNext(out var user, out var challenge))
        {
            if (challenge.IsFinishing)
                continue;

            if (challenge.Phase == FishingPhase.Idle)
            {
                RemCompDeferred<FishingChallengeComponent>(user);
                continue;
            }

            if (challenge.Rod is not { } rodUid ||
                TerminatingOrDeleted(rodUid) ||
                !TryComp<FishingRodComponent>(rodUid, out var rod) ||
                !_hands.IsHolding(user, rodUid))
            {
                StopChallenge(user, challenge, false, Loc.GetString("fishing-line-snapped"));
                continue;
            }

            if (challenge.Lure is { } lureUid && TerminatingOrDeleted(lureUid))
            {
                StopChallenge(user, challenge, false, Loc.GetString("fishing-line-snapped"));
                continue;
            }

            switch (challenge.Phase)
            {
                case FishingPhase.Waiting:
                    if (_timing.CurTime >= challenge.NextPhaseTime)
                        StartBiting(user, challenge, rod);
                    break;
                case FishingPhase.Biting:
                    if (_timing.CurTime >= challenge.NextPhaseTime)
                        StartWaiting(user, challenge, true);
                    break;
                case FishingPhase.Minigame:
                    challenge.TickAccumulator += frameTime;
                    while (challenge.TickAccumulator >= MinigameTick && !challenge.IsFinishing)
                    {
                        challenge.TickAccumulator -= MinigameTick;
                        StepMinigame(user, challenge, rodUid);
                    }

                    UpdateUi(rodUid, user, challenge);
                    break;
            }
        }
    }

    #endregion

    #region Phases

    private void StartWaiting(EntityUid user, FishingChallengeComponent challenge, bool penalty)
    {
        challenge.Phase = FishingPhase.Waiting;
        challenge.Reward = null;

        var wait = _sources.GetWaitTime(challenge.Source);
        if (penalty)
            wait = MathF.Min(wait + _random.NextFloat(3f, 5f), MaxWaitPenalty);

        challenge.NextPhaseTime = _timing.CurTime + TimeSpan.FromSeconds(wait);
        Dirty(user, challenge);

        if (challenge.Rod is { } rodUid)
            UpdateUi(rodUid, user, challenge);
    }

    private void StartBiting(EntityUid user, FishingChallengeComponent challenge, FishingRodComponent rod)
    {
        challenge.Phase = FishingPhase.Biting;
        challenge.Reward = null;
        challenge.RewardEntity = null;

        var host = challenge.SourceEntity ?? rod.Owner;
        _sources.TryRollReward(host, challenge.Source, user, rod.Owner, out var reward, out var existing);
        challenge.Reward = reward;
        challenge.RewardEntity = existing;

        challenge.NextPhaseTime = _timing.CurTime + TimeSpan.FromSeconds(BiteWindow);
        Dirty(user, challenge);

        _popup.PopupEntity(Loc.GetString("fishing-bite"), user, user);
        UpdateUi(rod.Owner, user, challenge);
    }

    private void StartMinigame(EntityUid user, FishingChallengeComponent challenge, FishingRodComponent rod)
    {
        challenge.Phase = FishingPhase.Minigame;

        if (!_proto.TryIndex(challenge.Source, out FishingSourcePrototype? source))
        {
            StopChallenge(user, challenge, false, Loc.GetString("fishing-nothing-caught"));
            return;
        }

        ProtoId<FishPrototype>? fishProtoId = null;

        if (challenge.RewardEntity is { } existingReward &&
            TryComp<FishComponent>(existingReward, out var existingFish))
        {
            fishProtoId = existingFish.Fish;
        }
        else if (challenge.Reward is { } rewardProto &&
                 _proto.TryIndex(rewardProto, out EntityPrototype? rewardEnt) &&
                 rewardEnt.TryGetComponent<FishComponent>(out var rewardFish, EntityManager.ComponentFactory))
        {
            fishProtoId = rewardFish.Fish;
        }

        var difficulty = MathF.Max(MinDifficulty, _sources.CalculateDifficulty(challenge.Source, fishProtoId, rod.Owner, user));
        challenge.Difficulty = difficulty;

        var mods = new FishFightModifiersEvent(difficulty);
        RaiseLocalEvent(rod.Owner, ref mods);

        challenge.Rules = mods.Rules;
        challenge.Completion = 30f;
        if (difficulty > 15f)
            challenge.Completion -= 15f * difficulty * 0.01f;

        challenge.FishHeight = 50f;
        challenge.BaitHeight = Math.Clamp(360f * mods.BaitHeight - difficulty * 1.3f, 100f, 500f);
        challenge.CompletionGain = mods.CompletionGain;
        challenge.CompletionLoss = mods.CompletionLoss;
        challenge.ReelingSpeed = 1200f * mods.BaitSpeed;
        challenge.Gravity = -800f * mods.Gravity;
        challenge.Bounciness = 0.6f * mods.Bounciness;
        challenge.Deceleration = 1.8f * mods.Deceleration;

        challenge.FishPosition = _random.NextFloat(0f, (MinigameArea - challenge.FishHeight) * 0.8f);
        var diffDist = 100f + difficulty;
        challenge.BaitPosition = Math.Clamp(
            challenge.FishPosition + _random.NextFloat(-diffDist, diffDist) - challenge.BaitHeight * 0.5f,
            0f,
            MinigameArea - challenge.BaitHeight);

        challenge.BaitVelocity = 0f;
        challenge.FishVelocity = 0f;
        challenge.FishTarget = 0f;
        challenge.HasFishTarget = false;
        challenge.TicksFired = 0;
        challenge.IsPlunging = true;
        challenge.ReelDirection = 0;
        challenge.Movement = fishProtoId is { } id && _proto.TryIndex(id, out FishPrototype? fish) ? fish.Movement : null;

        challenge.NextPhaseTime = TimeSpan.MaxValue;
        Dirty(user, challenge);

        _popup.PopupEntity(Loc.GetString("fishing-minigame-start"), user, user);
        UpdateUi(rod.Owner, user, challenge);
    }

    #endregion

    #region Minigame simulation

    private void StepMinigame(EntityUid user, FishingChallengeComponent challenge, EntityUid rodUid)
    {
        MoveFish(challenge);

        var fishOnBait = (challenge.FishPosition + challenge.FishHeight >= challenge.BaitPosition) &&
                         (challenge.BaitPosition + challenge.BaitHeight >= challenge.FishPosition);

        var shouldBounce = MathF.Abs(challenge.BaitVelocity) > 150f;
        challenge.BaitPosition += challenge.BaitVelocity * MinigameTick;

        if (challenge.BaitPosition > MinigameArea - challenge.BaitHeight)
        {
            challenge.BaitPosition = MinigameArea - challenge.BaitHeight;
            challenge.BaitVelocity = challenge.ReelDirection == 1 || !shouldBounce
                ? 0f
                : -challenge.BaitVelocity * challenge.Bounciness;
        }
        else if (challenge.BaitPosition < 0f)
        {
            challenge.BaitPosition = 0f;
            challenge.BaitVelocity = challenge.ReelDirection == -1 || !shouldBounce
                ? 0f
                : -challenge.BaitVelocity * challenge.Bounciness;
        }

        var bidirectional = (challenge.Rules & FishingMinigameRules.Bidirectional) != 0;

        float velocityChange;
        switch (challenge.ReelDirection)
        {
            case 1:
                velocityChange = challenge.ReelingSpeed;
                break;
            case -1:
                velocityChange = -challenge.ReelingSpeed;
                break;
            default:
                if (bidirectional && challenge.BaitVelocity > 0f)
                    velocityChange = -challenge.Gravity;
                else
                    velocityChange = challenge.Gravity;
                break;
        }

        velocityChange *= (fishOnBait ? challenge.OverlapVelocityMult : 1f) * MinigameTick;

        if (challenge.BaitVelocity > 0f && velocityChange < 0f)
            challenge.BaitVelocity += MathF.Max(-challenge.BaitVelocity, velocityChange * challenge.Deceleration);
        else if (challenge.BaitVelocity < 0f && velocityChange > 0f)
            challenge.BaitVelocity += MathF.Min(-challenge.BaitVelocity, velocityChange * challenge.Deceleration);

        if (bidirectional && challenge.ReelDirection == 0)
        {
            if (velocityChange < 0f)
                challenge.BaitVelocity = MathF.Max(challenge.BaitVelocity + velocityChange, 0f);
            else if (velocityChange > 0f)
                challenge.BaitVelocity = MathF.Min(challenge.BaitVelocity + velocityChange, 0f);
        }
        else
        {
            challenge.BaitVelocity += velocityChange;
        }

        if (fishOnBait)
        {
            challenge.Completion += challenge.CompletionGain * MinigameTick;
            if (challenge.Completion >= 100f)
            {
                CompleteMinigame(user, challenge, rodUid);
                return;
            }
        }
        else
        {
            challenge.Completion -= challenge.CompletionLoss * MinigameTick;
            if (challenge.Completion <= 0f && (challenge.Rules & FishingMinigameRules.NoEscape) == 0)
            {
                StopChallenge(user, challenge, false, Loc.GetString("fishing-got-away"));
                return;
            }
        }

        challenge.Completion = Math.Clamp(challenge.Completion, 0f, 100f);
        Dirty(user, challenge);
    }

    private void MoveFish(FishingChallengeComponent challenge)
    {
        challenge.TicksFired++;

        _proto.TryIndex(challenge.Movement, out FishMovementPrototype? movement);
        var difficulty = challenge.Difficulty;

        var area = MinigameArea - challenge.FishHeight;
        var idleVelocity = movement?.FishIdleVelocity ?? 0f;
        var shortLimit = (movement?.ShortJumpVelocityLimit ?? 400f) * (1f + difficulty * 0.01f);
        var longLimit = (movement?.LongJumpVelocityLimit ?? 200f) * (1f + difficulty * 0.01f);

        if ((movement?.Type ?? FishMovementType.Slow) == FishMovementType.Plunger)
        {
            if (challenge.IsPlunging)
            {
                if (challenge.HasFishTarget && challenge.FishTarget > challenge.FishPosition)
                    challenge.HasFishTarget = false;

                if (challenge.FishPosition / area <= 0.04f)
                {
                    idleVelocity -= movement?.PlungingSpeed ?? -22f;
                    challenge.IsPlunging = false;
                }
            }
            else if ((area - challenge.FishPosition) / area <= 0.04f)
            {
                idleVelocity += movement?.PlungingSpeed ?? -22f;
                challenge.IsPlunging = true;
            }
        }

        if (challenge.HasFishTarget && MathF.Abs(challenge.FishTarget - challenge.FishPosition) < FishTargetMinDistance)
            challenge.HasFishTarget = false;

        var canRoll = challenge.TicksFired % 2 == 1;
        if (canRoll)
        {
            var shortChance = (movement?.ShortJumpChance ?? 2.25f) * difficulty * 0.1f;
            var longChance = (movement?.LongJumpChance ?? 0.0625f) * difficulty * 0.1f;

            if (challenge.HasFishTarget && !(movement?.CanInterruptMove ?? true))
            {
                // The fish must reach the current target first.
            }
            else if (_random.NextFloat() * 100f < longChance)
            {
                var distanceFromTop = area - challenge.FishPosition - challenge.FishHeight;
                var distanceFromBottom = challenge.FishPosition;
                var goUp = distanceFromTop > distanceFromBottom
                    ? _random.NextFloat() < distanceFromTop / MathF.Max(distanceFromBottom, 1f)
                    : _random.NextFloat() >= distanceFromBottom / MathF.Max(distanceFromTop, 1f);

                challenge.FishTarget = goUp
                    ? challenge.FishPosition + distanceFromTop * _random.NextFloat(0.75f, 1f)
                    : challenge.FishPosition - distanceFromBottom * _random.NextFloat(0.75f, 1f);
                challenge.HasFishTarget = true;
                challenge.FishVelocityLimit = longLimit;
            }
            else if (_random.NextFloat() * 100f < shortChance)
            {
                var direction = _random.Next(2) == 0 ? -1f : 1f;
                challenge.FishTarget = Math.Clamp(challenge.FishPosition + direction * _random.NextFloat(6f, 60f), 0f, area);
                challenge.HasFishTarget = true;
                challenge.FishVelocityLimit = shortLimit;
            }
        }

        var acceleration = 2.5f + difficulty * 0.15f;
        if ((movement?.Type ?? FishMovementType.Slow) == FishMovementType.Accelerando)
            acceleration += MathF.Min(acceleration, acceleration * challenge.TicksFired * MinigameTick / MathF.Max(movement?.AccelerationTimeCap ?? 10f, 0.1f));

        if (challenge.HasFishTarget)
        {
            var distance = challenge.FishTarget - challenge.FishPosition;
            challenge.FishVelocity += distance * acceleration * MinigameTick;
        }
        else
        {
            challenge.FishVelocity += idleVelocity * MinigameTick;
            challenge.FishVelocity *= FishFriction;
        }

        var limit = challenge.FishVelocityLimit > 0f ? challenge.FishVelocityLimit : shortLimit;
        if ((movement?.Type ?? FishMovementType.Slow) == FishMovementType.Zippy)
            limit *= 1.6f;

        challenge.FishVelocity = Math.Clamp(challenge.FishVelocity, -limit, limit);
        challenge.FishPosition += challenge.FishVelocity * MinigameTick;

        if (challenge.FishPosition < 0f)
        {
            challenge.FishPosition = 0f;
            challenge.FishVelocity = -challenge.FishVelocity * FishFriction;
        }
        else if (challenge.FishPosition > area)
        {
            challenge.FishPosition = area;
            challenge.FishVelocity = -challenge.FishVelocity * FishFriction;
        }

        if ((movement?.Type ?? FishMovementType.Slow) == FishMovementType.Choppy &&
            challenge.TicksFired * MinigameTick % 0.5f > MinigameTick)
        {
            // Choppy only redraws every half a second. The position is still the authoritative one.
        }
    }

    private void CompleteMinigame(EntityUid user, FishingChallengeComponent challenge, EntityUid rodUid)
    {
        challenge.Completion = 100f;

        var reward = challenge.Reward;
        var rewardEntity = challenge.RewardEntity;
        var source = challenge.Source;
        var coords = challenge.CastCoordinates;

        StopChallenge(user, challenge, false, null);

        if (reward == null && rewardEntity == null)
        {
            _popup.PopupEntity(Loc.GetString("fishing-nothing-caught"), user, user);
            return;
        }

        if (_sources.CatchFish(user, rodUid, reward, coords, source, rewardEntity) != null)
        {
            var caughtEv = new FishCaughtWithRodEvent(user, reward);
            RaiseLocalEvent(rodUid, ref caughtEv);
        }
    }

    #endregion

    #region Cleanup

    private void StopChallenge(EntityUid user, FishingChallengeComponent challenge, bool win, string? message)
    {
        if (challenge.IsFinishing)
            return;

        challenge.IsFinishing = true;

        var rodUid = challenge.Rod;
        var lureUid = challenge.Lure;

        if (message != null)
            _popup.PopupEntity(message, user, user);

        if (rodUid is { } rod && TryComp<FishingRodComponent>(rod, out var rodComp))
        {
            rodComp.FishingLure = null;
            Dirty(rod, rodComp);

            if (TryComp<UserInterfaceComponent>(rod, out _))
                _ui.CloseUi(rod, FishingMinigameUiKey.Key, user);
        }

        if (lureUid is { } lure && !TerminatingOrDeleted(lure))
            QueueDel(lure);

        RemCompDeferred<FishingChallengeComponent>(user);
    }

    private void OnChallengeShutdown(Entity<FishingChallengeComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Lure is { } lure && !TerminatingOrDeleted(lure))
            QueueDel(lure);

        if (ent.Comp.Rod is { } rod && TryComp<FishingRodComponent>(rod, out var rodComp))
        {
            rodComp.FishingLure = null;
            Dirty(rod, rodComp);
        }
    }

    private void OnUiClosed(Entity<FishingRodComponent> ent, ref BoundUIClosedEvent args)
    {
        var user = args.Actor;
        if (args.UiKey is not FishingMinigameUiKey)
            return;

        if (!TryComp<FishingChallengeComponent>(user, out var challenge) || challenge.IsFinishing)
            return;

        if (challenge.Rod != ent.Owner)
            return;

        StopChallenge(user, challenge, false, Loc.GetString("fishing-gave-up"));
    }

    #endregion

    #region UI

    private void OnReelMessage(Entity<FishingRodComponent> ent, ref FishingMinigameReelMessage args)
    {
        if (!TryComp<ActorComponent>(args.Actor, out var actor))
            return;

        var user = args.Actor;
        if (!TryComp<FishingChallengeComponent>(user, out var challenge) || challenge.IsFinishing)
            return;

        if (challenge.Rod != ent.Owner)
            return;

        switch (challenge.Phase)
        {
            case FishingPhase.Biting when args.Direction != 0:
                StartMinigame(user, challenge, ent.Comp);
                break;
            case FishingPhase.Minigame:
                challenge.ReelDirection = args.Direction;
                Dirty(user, challenge);
                break;
        }

        // Kept to make the actor variable purposeful for BUI session resolution.
        _ = actor;
    }

    private void OnStopMessage(Entity<FishingRodComponent> ent, ref FishingMinigameStopMessage args)
    {
        var user = args.Actor;
        if (!TryComp<FishingChallengeComponent>(user, out var challenge) || challenge.IsFinishing)
            return;

        if (challenge.Rod != ent.Owner)
            return;

        StopChallenge(user, challenge, false, Loc.GetString("fishing-gave-up"));
    }

    private void UpdateUi(EntityUid rodUid, EntityUid user, FishingChallengeComponent challenge)
    {
        if (!TryComp<UserInterfaceComponent>(rodUid, out _))
            return;

        _ui.SetUiState(rodUid, FishingMinigameUiKey.Key, new FishingMinigameBoundUserInterfaceState(
            challenge.Phase,
            challenge.Completion,
            challenge.BaitPosition,
            challenge.FishPosition,
            challenge.BaitHeight,
            challenge.FishHeight,
            challenge.Background,
            challenge.FishIcon,
            challenge.FrameState,
            challenge.Rules));
    }

    #endregion
}