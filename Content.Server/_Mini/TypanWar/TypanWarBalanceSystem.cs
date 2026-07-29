// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._TT.StationHandleJob;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.GameTicking.Rules;
using Content.Shared._Mini.TypanWar;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Mini.TypanWar;

/// <summary>
/// Regulates NT vs Typan player counts during Typan Station War (max faction ratio).
/// </summary>
public sealed class TypanWarBalanceSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly TTStationHandleJobSystem _typanJobs = default!;
    [Dependency] private readonly TypanWarRespawnSystem _respawn = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IsRoleAllowedEvent>(OnIsRoleAllowed);
        SubscribeLocalEvent<GameRuleAddedEvent>(OnGameRuleAdded);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<GhostComponent, MindAddedMessage>(OnGhostMindAdded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeNetworkEvent<TypanWarBalanceStatusRequestEvent>(OnBalanceStatusRequest);

        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    /// <summary>
    /// Called when combat ends or is cancelled so late-join UI stops blocking factions.
    /// </summary>
    public void NotifyCombatPhaseEnded()
    {
        RaiseNetworkEvent(CreateInactiveBalanceEvent(), Filter.Broadcast());
    }

    /// <summary>
    /// Filters roundstart job assignments so overpopulated faction players remain in lobby.
    /// Call after station job fixups and overflow assignment, before spawning.
    /// </summary>
    public void BalanceRoundstartAssignments(ref Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs)
    {
        if (!TryGetActiveRule(out var rule) || rule.MaxFactionRatio < 1)
            return;

        var ratio = rule.MaxFactionRatio;
        var ntCount = 0;
        var typanCount = 0;

        var keys = assignedJobs.Keys.ToList();
        _random.Shuffle(keys);

        foreach (var userId in keys)
        {
            var (job, _) = assignedJobs[userId];
            if (job == null)
                continue;

            var side = GetJobSide(job.Value);
            if (!CanJoinSide(side, ntCount, typanCount, ratio))
            {
                assignedJobs[userId] = (null, EntityUid.Invalid);

                if (_playerManager.TryGetSessionById(userId, out var session))
                {
                    var locKey = side == TypanWarSide.Nanotrasen
                        ? "typan-war-balance-lobby-wait"
                        : "typan-war-balance-lobby-wait-typan";
                    _chatManager.DispatchServerMessage(session, Loc.GetString(locKey));
                }

                continue;
            }

            if (side == TypanWarSide.Typan)
                typanCount++;
            else
                ntCount++;
        }
    }

    private void OnGameRuleAdded(ref GameRuleAddedEvent ev)
    {
        if (!HasComp<TypanStationWarRuleComponent>(ev.RuleEntity))
            return;

        BroadcastStatus();
    }

    private void OnIsRoleAllowed(ref IsRoleAllowedEvent ev)
    {
        if (ev.Cancelled || ev.Jobs is not { } jobs)
            return;

        if (!TryGetActiveRule(out var rule) || rule.MaxFactionRatio < 1)
            return;

        var ratio = rule.MaxFactionRatio;
        var (nt, typan) = CountAlive();

        foreach (var job in jobs)
        {
            var side = GetJobSide(job);
            if (CanJoinSide(side, nt, typan, ratio))
                continue;

            ev.Cancelled = true;
            _chatManager.DispatchServerMessage(ev.Player, Loc.GetString("typan-war-balance-job-denied"));
            return;
        }
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        BroadcastStatus();
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        if (!TryGetActiveRule(out _))
            return;

        if (ev.OldMobState != MobState.Dead && ev.NewMobState != MobState.Dead)
            return;

        if (!TryComp<ActorComponent>(ev.Target, out _))
            return;

        BroadcastStatus();
    }

    private void OnGhostMindAdded(EntityUid uid, GhostComponent component, MindAddedMessage args)
    {
        if (!TryGetActiveRule(out _))
            return;

        var mind = args.Mind.Comp;
        if (mind.UserId is not { } userId)
            return;

        if (!_gameTicker.PlayerGameStatuses.TryGetValue(userId, out var status)
            || status != PlayerGameStatus.JoinedGame)
        {
            return;
        }

        BroadcastStatus();
        _respawn.HandleGhostMindAdded(uid, args);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (!TryGetActiveRule(out _))
            return;

        if (e.NewStatus is SessionStatus.Disconnected or SessionStatus.InGame)
            BroadcastStatus();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        NotifyCombatPhaseEnded();
    }

    private void OnBalanceStatusRequest(TypanWarBalanceStatusRequestEvent ev, EntitySessionEventArgs args)
    {
        if (!TryGetActiveRule(out _))
        {
            RaiseNetworkEvent(CreateInactiveBalanceEvent(), args.SenderSession);
            return;
        }

        var (nt, typan) = CountAlive();
        RaiseNetworkEvent(BuildStatusEvent(nt, typan), args.SenderSession);
    }

    private void BroadcastStatus()
    {
        if (!TryGetActiveRule(out var rule))
        {
            RaiseNetworkEvent(CreateInactiveBalanceEvent(), Filter.Broadcast());
            return;
        }

        var (nt, typan) = CountAlive();
        RaiseNetworkEvent(BuildStatusEvent(nt, typan, rule), Filter.Broadcast());
    }

    private static TypanWarBalanceStatusEvent CreateInactiveBalanceEvent()
    {
        return new TypanWarBalanceStatusEvent(
            active: false,
            allowNanotrasen: true,
            allowTypan: true,
            ntJoined: 0,
            typanJoined: 0);
    }

    private TypanWarBalanceStatusEvent BuildStatusEvent(int nt, int typan, TypanStationWarRuleComponent? rule = null)
    {
        rule ??= TryGetActiveRule(out var r) ? r : null;
        if (rule == null)
            return CreateInactiveBalanceEvent();

        var ratio = Math.Max(rule.MaxFactionRatio, 1);
        return new TypanWarBalanceStatusEvent(
            active: true,
            allowNanotrasen: CanJoinSide(TypanWarSide.Nanotrasen, nt, typan, ratio),
            allowTypan: CanJoinSide(TypanWarSide.Typan, nt, typan, ratio),
            ntJoined: nt,
            typanJoined: typan);
    }

    /// <summary>
    /// Mirrors <see cref="TypanStationWarRuleSystem.CountFactionAlive"/> (includes silicons).
    /// </summary>
    private (int Nt, int Typan) CountAlive()
    {
        var nt = 0;
        var typan = 0;

        var minds = EntityQueryEnumerator<MindComponent>();
        while (minds.MoveNext(out var mindId, out var mind))
        {
            if (!IsMindAlive(mind))
                continue;

            if (_typanJobs.MindHasTypanFactionJob(mindId))
            {
                typan++;
                continue;
            }

            if (_jobs.MindTryGetJobId(mindId, out var jobId) && jobId != null && !_typanJobs.IsCentCommJob(jobId.Value))
                nt++;
        }

        return (nt, typan);
    }

    private bool IsMindAlive(MindComponent mind)
    {
        var entity = mind.CurrentEntity;
        if (entity == null || !entity.Value.IsValid())
            return false;

        if (HasComp<GhostComponent>(entity))
            return false;

        if (TryComp<MobStateComponent>(entity, out var mobState))
            return mobState.CurrentState != MobState.Dead;

        return true;
    }

    private TypanWarSide GetJobSide(ProtoId<JobPrototype> job)
    {
        return _typanJobs.IsTypanJob(job) ? TypanWarSide.Typan : TypanWarSide.Nanotrasen;
    }

    private static bool CanJoinSide(TypanWarSide side, int ntCount, int typanCount, int ratio)
    {
        return side switch
        {
            TypanWarSide.Nanotrasen => ntCount + 1 <= ratio * Math.Max(typanCount, 1),
            TypanWarSide.Typan => typanCount + 1 <= ratio * Math.Max(ntCount, 1),
            _ => false,
        };
    }

    private bool TryGetActiveRule([NotNullWhen(true)] out TypanStationWarRuleComponent? component)
    {
        var query = EntityQueryEnumerator<TypanStationWarRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var comp, out var gameRule))
        {
            if (!_gameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            if (comp.Phase is not (TypanWarPhase.Pending or TypanWarPhase.Active))
                continue;

            component = comp;
            return true;
        }

        component = null;
        return false;
    }
}
