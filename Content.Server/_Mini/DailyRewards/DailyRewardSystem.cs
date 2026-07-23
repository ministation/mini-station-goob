// SPDX-FileCopyrightText: 2026 Casha
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._Mini.AntagTokens;
using Content.Server._Mini.DailyQuests;
using Content.Shared._Mini.AntagTokens;
using Content.Shared._Mini.DailyQuests;
using Content.Shared._Mini.DailyRewards;
using Content.Shared._Mini.GhostRolePurchase;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Players;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Mini.DailyRewards;

public sealed class DailyRewardSystem : EntitySystem
{
    private const string StreakRewardIconPath = "/Textures/_Mini/DailyRewards/streak.png";
    /// <summary>How often to push UI state to players who opened the rewards menu.</summary>
    private const float StateRefreshInterval = 5f;
    /// <summary>How often to flush playtime / ticket milestones (not every sim tick).</summary>
    private const float PlaytimeGrantInterval = 5f;

    private float _stateRefreshAccumulator;
    private float _playtimeGrantAccumulator;
    private readonly HashSet<NetUserId> _uiWatchers = new();

    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly UserDbDataManager _userDb = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly AntagTokenSystem _antagTokens = default!;
    [Dependency] private readonly AntagTokenListingSystem _antagListings = default!;
    [Dependency] private readonly DailyQuestSystem _dailyQuests = default!;

    private readonly Dictionary<NetUserId, SessionRewardState> _states = new();
    private readonly DailyRewardComponent _defaultComponent = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeNetworkEvent<DailyRewardOpenRequestEvent>(OnOpenRequest);
        SubscribeNetworkEvent<DailyRewardClaimRequestEvent>(OnClaimRequest);
        SubscribeNetworkEvent<DailyQuestReplaceRequestEvent>(OnQuestReplaceRequest);

        _userDb.AddOnLoadPlayer(LoadPlayerData);
        _userDb.AddOnFinishLoad(OnPlayerDataLoaded);
        _userDb.AddOnPlayerDisconnect(OnPlayerDisconnect);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        SaveAll();
    }

    public bool TryGetDebugState(NetUserId userId, [NotNullWhen(true)] out DailyRewardProgress? progress)
    {
        var state = EnsureStateExists(userId);
        if (state != null)
        {
            progress = state.Progress;
            EnsureCurrentDay(progress, DateTime.UtcNow);
            return true;
        }

        progress = null;
        return false;
    }

    public bool SetTodayActiveTime(NetUserId userId, TimeSpan activeTime)
    {
        var state = EnsureStateExists(userId);
        if (state == null)
            return false;

        EnsureCurrentDay(state.Progress, DateTime.UtcNow);
        state.Progress.PendingActiveTime = activeTime < TimeSpan.Zero ? TimeSpan.Zero : activeTime;
        state.MarkMutated();
        _ = _db.UpsertDailyRewardProgress(state.Progress);
        return true;
    }

    public bool SetStreak(NetUserId userId, int streak)
    {
        var state = EnsureStateExists(userId);
        if (state == null)
            return false;

        var component = TryGetComponentFor(userId) ?? _defaultComponent;
        state.Progress.CurrentStreak = Math.Clamp(streak, 0, component.MaxStreak);
        state.MarkMutated();
        _ = _db.UpsertDailyRewardProgress(state.Progress);
        return true;
    }

    public async Task<bool> SetStreakForPlayerAsync(Guid playerId, int streak, CancellationToken cancel = default)
    {
        var maxStreak = _defaultComponent.MaxStreak;
        streak = Math.Clamp(streak, 0, maxStreak);

        var progress = await _db.GetDailyRewardProgress(playerId, cancel);
        if (progress == null)
        {
            progress = new DailyRewardProgress
            {
                PlayerId = playerId,
                CurrentStreak = streak,
                PendingActiveDate = DateTime.UtcNow.Date,
                PendingActiveTime = TimeSpan.Zero,
            };
        }
        else
        {
            progress.CurrentStreak = streak;
        }

        await _db.UpsertDailyRewardProgress(progress);

        var netUserId = new NetUserId(playerId);
        if (_states.TryGetValue(netUserId, out var state))
        {
            state.Progress.CurrentStreak = streak;
            state.MarkMutated();
            if (_playerManager.TryGetSessionById(netUserId, out var session))
                SendState(session);
        }

        return true;
    }

    public bool SetLastClaimTime(NetUserId userId, DateTime? lastClaimTimeUtc)
    {
        var state = EnsureStateExists(userId);
        if (state == null)
            return false;

        state.Progress.LastClaimTime = lastClaimTimeUtc;
        state.MarkMutated();
        _ = _db.UpsertDailyRewardProgress(state.Progress);
        return true;
    }

    public bool MakeReadyToClaim(NetUserId userId, DailyRewardComponent? component = null)
    {
        var state = EnsureStateExists(userId);
        if (state == null)
            return false;

        component ??= TryGetComponentFor(userId) ?? _defaultComponent;

        EnsureCurrentDay(state.Progress, DateTime.UtcNow);
        state.Progress.PendingActiveTime = component.MinimumActiveTime;
        state.Progress.LastClaimTime = DateTime.UtcNow - component.ClaimCooldown - TimeSpan.FromMinutes(1);
        state.MarkMutated();
        _ = _db.UpsertDailyRewardProgress(state.Progress);
        return true;
    }

    public bool ResetProgress(NetUserId userId)
    {
        var state = EnsureStateExists(userId);
        if (state == null)
            return false;

        state.Progress.CurrentStreak = 0;
        state.Progress.LastClaimTime = null;
        state.Progress.PendingActiveDate = DateTime.UtcNow.Date;
        state.Progress.PendingActiveTime = TimeSpan.Zero;
        state.ActiveSince = null;
        state.ActiveStartedAtUtc = null;
        state.MarkMutated();
        _ = _db.UpsertDailyRewardProgress(state.Progress);
        return true;
    }

    public bool TryOpenForSession(ICommonSession session)
    {
        EnsureStateExists(session.UserId);
        _uiWatchers.Add(session.UserId);
        SendState(session);
        return true;
    }

    public void RefreshUi(NetUserId userId)
    {
        _uiWatchers.Add(userId);
        if (_playerManager.TryGetSessionById(userId, out var session))
            SendState(session);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _playtimeGrantAccumulator += frameTime;
        if (_playtimeGrantAccumulator >= PlaytimeGrantInterval)
        {
            _playtimeGrantAccumulator = 0f;

            foreach (var session in _playerManager.Sessions)
            {
                if (session.Status == SessionStatus.Disconnected)
                    continue;

                if (!_states.ContainsKey(session.UserId))
                    continue;

                GrantTicketsForPlaytime(session);
            }
        }

        _stateRefreshAccumulator += frameTime;
        if (_stateRefreshAccumulator < StateRefreshInterval)
            return;

        _stateRefreshAccumulator = 0f;

        foreach (var userId in _uiWatchers)
        {
            if (!_playerManager.TryGetSessionById(userId, out var session))
                continue;

            if (session.Status == SessionStatus.Disconnected)
                continue;

            if (!_states.ContainsKey(userId))
                continue;

            SendState(session);
        }
    }

    private async Task LoadPlayerData(ICommonSession player, CancellationToken cancel)
    {
        var progress = await _db.GetDailyRewardProgress(player.UserId.UserId, cancel);
        if (_states.TryGetValue(player.UserId, out var existingState))
        {
            // Keep local/admin changes that happened before async DB load completed.
            if (existingState.LocalMutationCount > 0)
                return;
        }

        _states[player.UserId] = new SessionRewardState(progress ?? new DailyRewardProgress
        {
            PlayerId = player.UserId.UserId,
            CurrentStreak = 0,
            PendingActiveDate = DateTime.UtcNow.Date,
            PendingActiveTime = TimeSpan.Zero,
        });
    }

    private void OnPlayerDisconnect(ICommonSession player)
    {
        FlushActiveSegment(player);

        if (_states.TryGetValue(player.UserId, out var state))
            _ = _db.UpsertDailyRewardProgress(state.Progress);

        _states.Remove(player.UserId);
        _uiWatchers.Remove(player.UserId);
    }

    private void OnPlayerDataLoaded(ICommonSession player)
    {
        if (!_states.ContainsKey(player.UserId))
            EnsureStateExists(player.UserId);

        StartTracking(player);
        SendState(player);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        EnsureComp<DailyRewardComponent>(ev.Mob);
        StartTracking(ev.Player);
        SendState(ev.Player);
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        StartTracking(ev.Player);

        if (ev.Entity is not { Valid: true } uid)
            return;

        EnsureComp<DailyRewardComponent>(uid);
        SendState(ev.Player);
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        FlushActiveSegment(ev.Player);
    }

    private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent ev)
    {
        FlushActiveSegment(ev.PlayerSession);
        StartTracking(ev.PlayerSession);

        if (ev.PlayerSession.AttachedEntity is { Valid: true } uid)
            EnsureComp<DailyRewardComponent>(uid);

        SendState(ev.PlayerSession);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent _)
    {
        SaveAll();
    }

    private void OnOpenRequest(DailyRewardOpenRequestEvent _, EntitySessionEventArgs args)
    {
        TryOpenForSession(args.SenderSession);
    }

    private void OnClaimRequest(DailyRewardClaimRequestEvent _, EntitySessionEventArgs args)
    {
        ClaimReward(args.SenderSession);
    }

    private void OnQuestReplaceRequest(DailyQuestReplaceRequestEvent ev, EntitySessionEventArgs args)
    {
        Log.Info($"Daily quest replace requested by {args.SenderSession.Name}: quest={ev.QuestId}, slot={ev.SlotIndex}");
        _dailyQuests.TryReplaceQuest(args.SenderSession, ev.QuestId, ev.SlotIndex);
    }

    private void StartTracking(ICommonSession session)
    {
        if (!_states.TryGetValue(session.UserId, out var state))
            return;

        var previousDate = state.Progress.PendingActiveDate;
        EnsureCurrentDay(state.Progress, DateTime.UtcNow);

        // Reset ticket milestones if day changed
        if (previousDate != state.Progress.PendingActiveDate)
            ResetTicketMilestones(session);

        if (state.ActiveSince != null || session.AttachedEntity == null)
            return;

        state.ActiveSince = _timing.CurTime;
        state.ActiveStartedAtUtc = DateTime.UtcNow;
    }

    private void FlushActiveSegment(ICommonSession session)
    {
        if (!_states.TryGetValue(session.UserId, out var state) || state.ActiveSince == null)
            return;

        var nowUtc = DateTime.UtcNow;
        var startedAtUtc = state.ActiveStartedAtUtc ?? nowUtc;
        AccumulateActiveTime(state.Progress, startedAtUtc, nowUtc);
        state.ActiveSince = null;
        state.ActiveStartedAtUtc = null;
    }

    private void SaveAll()
    {
        foreach (var session in _playerManager.Sessions)
        {
            FlushActiveSegment(session);
        }

        foreach (var state in _states.Values)
        {
            _ = _db.UpsertDailyRewardProgress(state.Progress);
        }
    }

    private void ClaimReward(ICommonSession session)
    {
        if (!_states.TryGetValue(session.UserId, out var state))
            return;

        FlushActiveSegment(session);
        var component = GetConfigFor(session);

        var now = DateTime.UtcNow;
        if (!CanClaim(state.Progress, component, now, out var nextDay))
        {
            if (session.AttachedEntity is { Valid: true } uid)
                _popup.PopupEntity("Ежедневная награда пока недоступна.", uid, uid);

            SendState(session);
            return;
        }

        if (IsStreakExpired(state.Progress.LastClaimTime, component, now))
        {
            state.Progress.CurrentStreak = 0;
        }

        var reward = GetRewardPreview(component, nextDay);
        if (reward.TokenAmount > 0)
        {
            _antagTokens.AddBalance(session.UserId, reward.TokenAmount, out var grantedAmount, out var note);

            if (session.AttachedEntity is { Valid: true } uid)
            {
                var message = grantedAmount > 0
                    ? $"Получено токенов: {grantedAmount}."
                    : "Токены по этой награде не начислены.";

                if (!string.IsNullOrWhiteSpace(note))
                    message = $"{message} {note}";

                _popup.PopupEntity(message, uid, uid);
            }
        }

        if (reward.RoleUnlockRoleId != null)
        {
            _antagTokens.AddRoleCredit(session.UserId, reward.RoleUnlockRoleId, 1, out var totalCredits);

            if (session.AttachedEntity is { Valid: true } uid)
            {
                _popup.PopupEntity(
                    $"Получен бесплатный жетон на роль \"{reward.DisplayName}\". Доступно: {totalCredits}.",
                    uid,
                    uid);
            }
        }

        if (reward.TokenAmount <= 0 && reward.RoleUnlockRoleId == null)
        {
            if (session.AttachedEntity is { Valid: true } uid)
                _popup.PopupEntity($"Ежедневная награда за день {nextDay} получена.", uid, uid);
        }

        state.Progress.CurrentStreak = nextDay;
        state.Progress.LastClaimTime = now;
        state.Progress.PendingActiveTime = TimeSpan.Zero;
        state.MarkMutated();

        // Grant tickets for streak milestones
        GrantTicketsForStreak(session, nextDay);

        _ = _db.UpsertDailyRewardProgress(state.Progress);

        StartTracking(session);
        SendState(session);
    }

    private bool CanClaim(DailyRewardProgress progress, DailyRewardComponent component, DateTime now, out int nextDay)
    {
        EnsureCurrentDay(progress, now);

        var currentStreak = progress.CurrentStreak;
        if (IsStreakExpired(progress.LastClaimTime, component, now))
            currentStreak = 0;

        nextDay = Math.Clamp(currentStreak + 1, 1, component.MaxStreak);

        if (progress.LastClaimTime != null && now - progress.LastClaimTime.Value < component.ClaimCooldown)
            return false;

        return progress.PendingActiveTime >= component.MinimumActiveTime;
    }

    private void SendState(ICommonSession session)
    {
        if (session.Status == SessionStatus.Disconnected)
            return;

        if (!_states.TryGetValue(session.UserId, out var state))
            return;

        var component = GetConfigFor(session);
        var now = DateTime.UtcNow;
        EnsureCurrentDay(state.Progress, now);

        var pending = state.Progress.PendingActiveTime;
        if (state.ActiveSince != null)
            pending = GetCurrentDayActiveTime(state.Progress, state.ActiveStartedAtUtc, now);

        var lastClaim = state.Progress.LastClaimTime;
        var visibleStreak = state.Progress.CurrentStreak;
        if (IsStreakExpired(lastClaim, component, now))
            visibleStreak = 0;

        var nextDay = Math.Clamp(visibleStreak + 1, 1, component.MaxStreak);
        var timeUntilExpiration = lastClaim == null
            ? component.ExpirationWindow
            : MaxZero(component.ExpirationWindow - (now - lastClaim.Value));
        var timeUntilNextClaim = lastClaim == null
            ? TimeSpan.Zero
            : MaxZero(component.ClaimCooldown - (now - lastClaim.Value));
        var canClaim = pending >= component.MinimumActiveTime && timeUntilNextClaim == TimeSpan.Zero;

        var rewards = new List<DailyRewardEntry>(component.MaxStreak);
        for (var day = 1; day <= component.MaxStreak; day++)
        {
            var reward = GetRewardPreview(component, day);
            rewards.Add(new DailyRewardEntry(
                day,
                reward.DisplayName,
                reward.TokenAmount > 0 || reward.RoleUnlockRoleId != null,
                reward.IconPath,
                day <= visibleStreak,
                day == nextDay));
        }

        TimeSpan onlineElapsed = TimeSpan.Zero;
        var onlineGranted = new List<TimeSpan>();
        if (_antagTokens.TryGetOnlineRewardUiState(session.UserId, now, out var onlineEl, out var onlineGt))
        {
            onlineElapsed = onlineEl;
            onlineGranted = onlineGt;
        }

        RaiseNetworkEvent(new DailyRewardStateEvent(new DailyRewardUpdateMessage(
            visibleStreak,
            nextDay,
            canClaim,
            state.ActiveSince != null,
            lastClaim != null,
            timeUntilExpiration,
            timeUntilNextClaim,
            pending,
            component.MinimumActiveTime,
            rewards,
            onlineElapsed,
            onlineGranted,
            _dailyQuests.BuildQuestEntries(session))), session);
    }

    private RewardDefinition GetRewardPreview(DailyRewardComponent component, int day)
    {
        var tokenAmount = GetRewardAmount(component, day);
        component.BonusRoleUnlockRewards.TryGetValue(day, out var roleUnlockRoleId);

        if (roleUnlockRoleId != null &&
            _antagListings.TryGetListing(roleUnlockRoleId, out var role))
        {
            return new RewardDefinition(Loc.GetString(role.NameLocKey), tokenAmount, role.IconPath, roleUnlockRoleId);
        }

        var displayName = tokenAmount > 0
            ? $"+{tokenAmount}"
            : "Прогресс стрика";

        return new RewardDefinition(displayName, tokenAmount, StreakRewardIconPath, null);
    }

    private static int GetRewardAmount(DailyRewardComponent component, int day)
    {
        var amount = 0;

        if (component.BaseRewardEveryDays > 0 && day % component.BaseRewardEveryDays == 0)
            amount += component.BaseRewardAmount;

        if (component.BonusTokenRewards.TryGetValue(day, out var bonus))
            amount += bonus;

        return amount;
    }

    private static TimeSpan MaxZero(TimeSpan span)
    {
        return span < TimeSpan.Zero ? TimeSpan.Zero : span;
    }

    private static bool IsStreakExpired(DateTime? lastClaimTime, DailyRewardComponent component, DateTime nowUtc)
    {
        if (lastClaimTime == null)
            return false;

        var lastClaim = lastClaimTime.Value;
        if (nowUtc - lastClaim <= component.ExpirationWindow)
            return false;

        return nowUtc.Date > lastClaim.Date.AddDays(1 + component.StreakMissGraceDays);
    }

    private DailyRewardComponent? TryGetComponentFor(NetUserId userId)
    {
        if (!_playerManager.TryGetSessionById(userId, out var session) ||
            session.AttachedEntity is not { Valid: true } uid)
        {
            return null;
        }

        return EnsureComp<DailyRewardComponent>(uid);
    }

    private DailyRewardComponent GetConfigFor(ICommonSession session)
    {
        if (session.AttachedEntity is { Valid: true } uid)
            return EnsureComp<DailyRewardComponent>(uid);

        return _defaultComponent;
    }

    private SessionRewardState? EnsureStateExists(NetUserId userId)
    {
        if (_states.TryGetValue(userId, out var existing))
            return existing;

        if (!_playerManager.TryGetSessionById(userId, out var session))
            return null;

        // Don't create placeholder progress before DB callbacks complete.
        // Otherwise a quick disconnect/restart can persist a zeroed streak over real data.
        if (!_userDb.IsLoadComplete(session))
            return null;

        var state = new SessionRewardState(new DailyRewardProgress
        {
            PlayerId = session.UserId.UserId,
            CurrentStreak = 0,
            PendingActiveDate = DateTime.UtcNow.Date,
            PendingActiveTime = TimeSpan.Zero,
        });

        _states[userId] = state;
        return state;
    }

    private static void EnsureCurrentDay(DailyRewardProgress progress, DateTime nowUtc)
    {
        var today = nowUtc.Date;
        if (progress.PendingActiveDate?.Date == today)
            return;

        progress.PendingActiveDate = today;
        progress.PendingActiveTime = TimeSpan.Zero;
    }

    /// <summary>
    /// Resets ticket milestones for a new day.
    /// Should be called when the day changes.
    /// </summary>
    private void ResetTicketMilestones(ICommonSession session)
    {
        if (session.AttachedEntity is not { Valid: true } uid)
            return;

        if (!TryComp<GhostRoleTicketComponent>(uid, out var tickets))
            return;

        tickets.TicketMilestones.Clear();
        Dirty(uid, tickets);
    }

    private static void AccumulateActiveTime(DailyRewardProgress progress, DateTime startedAtUtc, DateTime endedAtUtc)
    {
        if (endedAtUtc <= startedAtUtc)
            return;

        var current = startedAtUtc;
        while (current < endedAtUtc)
        {
            var dayStart = current.Date;
            var nextDay = dayStart.AddDays(1);
            var segmentEnd = endedAtUtc < nextDay ? endedAtUtc : nextDay;

            EnsureCurrentDay(progress, current);
            progress.PendingActiveTime += segmentEnd - current;

            current = segmentEnd;
        }

        EnsureCurrentDay(progress, endedAtUtc);
    }

    private static TimeSpan GetCurrentDayActiveTime(DailyRewardProgress progress, DateTime? activeStartedAtUtc, DateTime nowUtc)
    {
        EnsureCurrentDay(progress, nowUtc);

        if (activeStartedAtUtc == null || activeStartedAtUtc >= nowUtc)
            return progress.PendingActiveTime;

        var todayStart = nowUtc.Date;
        var effectiveStart = activeStartedAtUtc.Value < todayStart ? todayStart : activeStartedAtUtc.Value;
        return progress.PendingActiveTime + (nowUtc - effectiveStart);
    }

    /// <summary>
    /// Ensures the player has a ticket component.
    /// </summary>
    private void EnsureTicketComponent(EntityUid uid)
    {
        EnsureComp<GhostRoleTicketComponent>(uid);
    }

    /// <summary>
    /// Grants tickets for playtime milestones.
    /// </summary>
    private void GrantTicketsForPlaytime(ICommonSession session)
    {
        if (session.AttachedEntity is not { Valid: true } uid)
            return;

        if (!_states.TryGetValue(session.UserId, out var state))
            return;

        EnsureTicketComponent(uid);
        var tickets = Comp<GhostRoleTicketComponent>(uid);

        FlushActiveSegment(session);
        var activeTime = state.Progress.PendingActiveTime;

        var milestones = new[]
        {
            (TimeSpan.FromMinutes(30), 1),
            (TimeSpan.FromHours(3), 1),
        };

        foreach (var (threshold, amount) in milestones)
        {
            if (activeTime >= threshold && !tickets.TicketMilestones.Contains(threshold))
            {
                tickets.TicketMilestones.Add(threshold);
                tickets.Tickets += amount;
                Dirty(uid, tickets);

                // _popup.PopupEntity($"Получено билетов: {amount}", uid, uid);
                RaiseNetworkEvent(new GhostRoleTicketUpdateEvent(tickets.Tickets), session);
            }
        }

        StartTracking(session);
    }

    /// <summary>
    /// Grants tickets for streak milestones.
    /// </summary>
    private void GrantTicketsForStreak(ICommonSession session, int streak)
    {
        if (session.AttachedEntity is not { Valid: true } uid)
            return;

        EnsureTicketComponent(uid);
        var tickets = Comp<GhostRoleTicketComponent>(uid);

        // Define streak milestones: 15 days, 30 days
        var milestones = new[]
        {
            (15, 3),
            (30, 4)
        };

        foreach (var (threshold, amount) in milestones)
        {
            if (streak >= threshold && !tickets.StreakMilestones.Contains(threshold))
            {
                tickets.StreakMilestones.Add(threshold);
                tickets.Tickets += amount;
                Dirty(uid, tickets);

                _popup.PopupEntity($"Получено билетов за стрик {threshold} дней: {amount}", uid, uid);
                RaiseNetworkEvent(new GhostRoleTicketUpdateEvent(tickets.Tickets), session);
            }
        }
    }

    private sealed class SessionRewardState(DailyRewardProgress progress)
    {
        public DailyRewardProgress Progress { get; private set; } = progress;
        public TimeSpan? ActiveSince { get; set; }
        public DateTime? ActiveStartedAtUtc { get; set; }
        public int LocalMutationCount { get; private set; }

        public void MarkMutated()
        {
            LocalMutationCount++;
        }
    }

    private readonly record struct RewardDefinition(string? DisplayName, int TokenAmount, string IconPath, string? RoleUnlockRoleId);
}
