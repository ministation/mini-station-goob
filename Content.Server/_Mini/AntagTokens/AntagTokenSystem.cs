// SPDX-FileCopyrightText: 2026 Casha
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._Mini.AntagUnlock;
using Content.Server._Mini.JobUnlock;
using Content.Server._Mini.TypanWar;
using Content.Server.Popups;
using Content.Server.Sponsors;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Mind;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Preferences.Managers;
using Content.Server.Roles;
using Content.Server.Roles.Jobs;
using Content.Shared._Mini.AntagUnlock;
using Content.Shared._Mini.AntagTokens;
using Content.Shared._Mini.JobUnlock;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Content.Shared.Chat;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Server.Chat.Systems;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Asynchronous;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Mini.AntagTokens;

public sealed class AntagTokenSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly UserDbDataManager _userDb = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly AntagSelectionSystem _antagSelection = default!;
    [Dependency] private readonly JobSystem _jobs = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly AntagTokenListingSystem _listings = default!;
    [Dependency] private readonly GhostRoleSystem _ghostRoles = default!;
    [Dependency] private readonly ITaskManager _taskManager = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IServerPreferencesManager _preferences = default!;
    [Dependency] private readonly TypanStationWarRuleSystem _typanWar = default!;

    private readonly Dictionary<NetUserId, PlayerTokenState> _states = new();
    private readonly Dictionary<NetUserId, int?> _sponsorLevelOverrides = new();
    private readonly Dictionary<NetUserId, OnlineRewardState> _onlineRewards = new();
    private readonly HashSet<NetUserId> _roundGrantedLobbyAntag = new();
    private readonly HashSet<NetUserId> _roundGrantedGhostRule = new();
    private readonly HashSet<string> _globallyClaimedGhostRoles = new(StringComparer.Ordinal);
    private readonly HashSet<string> _currentRoundPurchasedRoles = new(StringComparer.Ordinal);
    private readonly HashSet<string> _lastRoundPurchasedRoles = new(StringComparer.Ordinal);
    private float _databaseSyncAccumulator;
    private float _lobbyDepositCapacityEnforceAccumulator;
    private bool _databaseSyncPassRunning;
    private bool _storeEnabled = true;
    private const float DatabaseSyncInterval = 15f;
    private const int SharedTokenSlotsPlayersPerSlot = 8;
    private bool _enforcingSharedTokenSlotCapacity;
    private readonly Dictionary<string, int> _ghostMinimumTimeRandomBonusByRole = new();
    private static readonly HashSet<string> BlockedRoundstartRolePresets = new(StringComparer.OrdinalIgnoreCase)
    {
        "Extended",
        "SecretExtended",
        "Greenshift",
        "SecretGreenshift",
        "TheGhost",
        "Nukeops",
        "NukeTraitor",
        "NukeLing",
        "Honkops",
        "TypanStationWar",
    };

    private static int EncodeUtcDayNumber(DateTime utc)
    {
        return utc.Year * 10000 + utc.Month * 100 + utc.Day;
    }

    private void EvaluateFreePurchaseFlags(
        AntagRoleDefinition role,
        NetUserId userId,
        PlayerTokenState state,
        bool useRoleCredit,
        out bool useDonorDailyFree,
        out bool usePublicRoundFree)
    {
        useDonorDailyFree = false;
        usePublicRoundFree = false;

        if (useRoleCredit || role.FreeMinimumSponsorLevel < 0)
            return;

        if (role.FreeMinimumSponsorLevel == 0)
        {
            usePublicRoundFree = true;
            return;
        }

        var sponsorLevel = GetEffectiveSponsorLevel(userId);
        if (sponsorLevel < role.FreeMinimumSponsorLevel)
            return;

        var today = EncodeUtcDayNumber(DateTime.UtcNow);
        if (state.LastDonorDailyFreeAntagDay == today)
            return;

        useDonorDailyFree = true;
    }
    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<AntagTokenOpenRequestEvent>(OnOpenRequest);
        SubscribeNetworkEvent<AntagTokenPurchaseRequestEvent>(OnPurchaseRequest);
        SubscribeNetworkEvent<AntagTokenClearRequestEvent>(OnClearRequest);

        SubscribeLocalEvent<AntagSelectionComponent, AntagSelectionExcludeSessionEvent>(OnExcludeReservedSession);
        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnJoinedLobby);
        SubscribeLocalEvent<RulePlayerJobsAssignedEvent>(OnRoundstartJobsAssigned, after: new[] { typeof(AntagSelectionSystem) });
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeLocalEvent<GhostRoleComponent, GhostRoleRegisteredEvent>(OnGhostRoleRegistered);
        SubscribeLocalEvent<GhostRoleComponent, TakeGhostRoleEvent>(OnGhostRoleTakenForToken, after: new[] { typeof(GhostRoleSystem) });

        _userDb.AddOnLoadPlayer(LoadPlayerData);
        _userDb.AddOnPlayerDisconnect(OnPlayerDisconnect);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        SaveAll();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = DateTime.UtcNow;
        foreach (var session in _playerManager.Sessions)
        {
            if (!_onlineRewards.TryGetValue(session.UserId, out var rewardState))
                continue;

            rewardState.EnsureCurrentCycle(now);

            foreach (var (threshold, rewardAmount) in AntagTokenCatalog.OnlineRewardMilestones)
            {
                if (rewardState.GrantedThresholds.Contains(threshold))
                    continue;

                if (rewardState.GetElapsed(now) < threshold)
                    continue;

                rewardState.GrantedThresholds.Add(threshold);
                AddBalance(session.UserId, rewardAmount, out var granted, out _);

                if (granted > 0)
                {
                    var hours = (int) threshold.TotalHours;
                    var message = Loc.GetString("antag-tokens-online-reward", ("amount", granted), ("hours", hours));

                    if (session.AttachedEntity is { Valid: true } uid)
                    {
                        _popup.PopupEntity(message, uid, uid);
                    }
                }
            }
        }

        _databaseSyncAccumulator -= frameTime;
        if (_databaseSyncAccumulator <= 0f)
        {
            _databaseSyncAccumulator = DatabaseSyncInterval;
            _ = RunPeriodicDatabaseSyncAsync();
        }

        _lobbyDepositCapacityEnforceAccumulator -= frameTime;
        if (_lobbyDepositCapacityEnforceAccumulator <= 0f)
        {
            _lobbyDepositCapacityEnforceAccumulator = 2f;
            EnforceSharedTokenSlotCapacity();
        }
    }

    public bool AddBalance(NetUserId userId, int amount, out int grantedAmount, out string? note)
    {
        grantedAmount = 0;
        note = null;

        if (amount <= 0)
            return false;

        var state = EnsureStateExists(userId);
        if (state == null)
            return false;

        NormalizeMonthlyState(state, DateTime.UtcNow, userId);

        var cap = GetMonthlyCap(userId);
        var available = cap.HasValue ? Math.Max(0, cap.Value - state.MonthlyEarned) : amount;
        grantedAmount = Math.Min(amount, available);

        if (grantedAmount > 0)
        {
            state.Balance += grantedAmount;
            if (cap.HasValue)
                state.MonthlyEarned += grantedAmount;
        }

        if (grantedAmount < amount)
            note = Loc.GetString("antag-tokens-error-monthly-cap-note");

        PersistState(userId, state);
        SendState(userId);
        return grantedAmount > 0 || note != null;
    }

    public bool TrySpendBalance(NetUserId userId, int amount, out string? error)
    {
        error = null;

        if (amount <= 0)
            return true;

        var state = EnsureStateExists(userId);
        if (state == null)
        {
            error = Loc.GetString("antag-tokens-error-currency-not-loaded");
            return false;
        }

        if (state.Balance < amount)
        {
            error = Loc.GetString("antag-tokens-error-not-enough-tokens", ("required", amount));
            return false;
        }

        state.Balance -= amount;
        PersistState(userId, state);
        SendState(userId);
        return true;
    }

    public bool HasJobUnlock(NetUserId userId, ProtoId<JobPrototype> jobId)
    {
        return _states.TryGetValue(userId, out var state) && state.JobUnlocks.Contains(jobId);
    }

    public HashSet<ProtoId<JobPrototype>> GetJobUnlocks(NetUserId userId)
    {
        if (_states.TryGetValue(userId, out var state))
            return new HashSet<ProtoId<JobPrototype>>(state.JobUnlocks);

        return new HashSet<ProtoId<JobPrototype>>();
    }

    public bool TryUnlockJob(NetUserId userId, ProtoId<JobPrototype> jobId, int cost, out string? error)
    {
        error = null;

        var state = EnsureStateExists(userId);
        if (state == null)
        {
            error = Loc.GetString("antag-tokens-error-currency-not-loaded");
            return false;
        }

        if (state.JobUnlocks.Contains(jobId))
        {
            error = Loc.GetString("job-unlock-error-already-unlocked");
            return false;
        }

        if (!TrySpendBalance(userId, cost, out error))
            return false;

        state = EnsureStateExists(userId);
        if (state == null)
            return false;

        state.JobUnlocks.Add(jobId);
        PersistState(userId, state);
        EntityManager.System<JobUnlockSystem>().SendJobUnlocksIfOnline(userId);
        return true;
    }

    public bool HasAntagUnlock(NetUserId userId, ProtoId<AntagPrototype> antagId)
    {
        return _states.TryGetValue(userId, out var state) && state.AntagUnlocks.Contains(antagId);
    }

    public HashSet<ProtoId<AntagPrototype>> GetAntagUnlocks(NetUserId userId)
    {
        if (_states.TryGetValue(userId, out var state))
            return new HashSet<ProtoId<AntagPrototype>>(state.AntagUnlocks);

        return new HashSet<ProtoId<AntagPrototype>>();
    }

    public bool TryUnlockAntag(NetUserId userId, ProtoId<AntagPrototype> antagId, int cost, out string? error)
    {
        error = null;

        var state = EnsureStateExists(userId);
        if (state == null)
        {
            error = Loc.GetString("antag-tokens-error-currency-not-loaded");
            return false;
        }

        if (state.AntagUnlocks.Contains(antagId))
        {
            error = Loc.GetString("antag-unlock-error-already-unlocked");
            return false;
        }

        if (!TrySpendBalance(userId, cost, out error))
            return false;

        state = EnsureStateExists(userId);
        if (state == null)
            return false;

        state.AntagUnlocks.Add(antagId);
        PersistState(userId, state);
        EntityManager.System<AntagUnlockSystem>().SendAntagUnlocksIfOnline(userId);
        return true;
    }

    public bool HasAntagPlaytimeAccess(ICommonSession session, ProtoId<AntagPrototype> antagId)
    {
        if (GetEffectiveSponsorLevel(session.UserId) > 0)
            return true;

        if (HasAntagUnlock(session.UserId, antagId))
            return true;

        return EntityManager.System<PlayTimeTrackingSystem>().IsAllowed(session, antagId);
    }

    public int GetBalance(NetUserId userId)
    {
        return EnsureStateExists(userId)?.Balance ?? 0;
    }

    public bool SetBalance(NetUserId userId, int amount)
    {
        if (amount < 0)
            return false;

        var state = EnsureStateExists(userId);
        if (state == null)
            return false;

        state.Balance = amount;
        PersistState(userId, state);
        SendState(userId);
        return true;
    }

    public bool SetMonthlyEarned(NetUserId userId, int amount)
    {
        if (amount < 0)
            return false;

        var state = EnsureStateExists(userId);
        if (state == null)
            return false;

        NormalizeMonthlyState(state, DateTime.UtcNow, userId);
        state.MonthlyEarned = amount;
        PersistState(userId, state);
        SendState(userId);
        return true;
    }

    public bool AddRoleCredit(NetUserId userId, string roleId, int amount, out int newAmount)
    {
        newAmount = 0;

        if (amount <= 0 || !_listings.TryGetListing(roleId, out _))
            return false;

        var state = EnsureStateExists(userId);
        if (state == null)
            return false;

        state.RoleCredits.TryGetValue(roleId, out var current);
        newAmount = current + amount;
        state.RoleCredits[roleId] = newAmount;
        PersistState(userId, state);
        SendState(userId);
        return true;
    }

    public bool TryOpenForSession(ICommonSession session)
    {
        if (!_storeEnabled)
            return false;

        if (EnsureStateExists(session.UserId) == null)
            return false;

        EntityManager.System<AntagUnlockSystem>().SendRoleBypassState(session);
        SendState(session.UserId);
        return true;
    }

    public bool TryGetDebugState(NetUserId userId, [NotNullWhen(true)] out PlayerTokenState? state)
    {
        state = EnsureStateExists(userId);
        if (state == null)
            return false;

        NormalizeMonthlyState(state, DateTime.UtcNow, userId);
        return true;
    }

    public void SetStoreEnabled(bool enabled)
    {
        _storeEnabled = enabled;
    }

    public bool IsStoreEnabled()
    {
        return _storeEnabled;
    }

    public bool TryPurchaseForSession(ICommonSession session, string roleId, out string? error)
    {
        error = null;

        if (!_storeEnabled)
        {
            error = Loc.GetString("antag-tokens-error-store-disabled");
            return false;
        }

        if (!_listings.TryGetListing(roleId, out var role))
        {
            error = Loc.GetString("antag-tokens-error-role-not-in-store");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(role.AntagId) &&
            _prototype.TryIndex<AntagPrototype>(role.AntagId, out var antagProto) &&
            !HasAntagPlaytimeAccess(session, antagProto.ID))
        {
            error = Loc.GetString("antag-unlock-error-playtime-required");
            return false;
        }

        var state = EnsureStateExists(session.UserId);
        if (state == null)
        {
            error = Loc.GetString("antag-tokens-error-currency-not-loaded");
            return false;
        }

        if (state.PendingGhostAutoRoleId != null && _roundGrantedLobbyAntag.Contains(session.UserId))
        {
            _globallyClaimedGhostRoles.Remove(state.PendingGhostAutoRoleId);
            ClearPendingGhostAuto(state);
            PersistState(session.UserId, state);
            SendState(session.UserId);
            BroadcastAntagTokenUiRefresh();
        }

        if (HasConsumedTokenAntagGrantThisRound(session.UserId))
        {
            error = Loc.GetString("antag-tokens-error-one-antag-per-round");
            return false;
        }

        if (role.Mode == AntagPurchaseMode.LobbyDeposit && state.PendingGhostAutoRoleId != null)
        {
            error = Loc.GetString("antag-tokens-error-round-limit");
            return false;
        }

        if (role.Mode == AntagPurchaseMode.GhostRule && state.PendingDepositRoleId != null)
        {
            error = Loc.GetString("antag-tokens-error-round-limit");
            return false;
        }

        var cache = BuildSendStateCache(session.UserId);
        var holdsThisCap = (role.Mode == AntagPurchaseMode.LobbyDeposit && state.PendingDepositRoleId == role.Id)
            || (role.Mode == AntagPurchaseMode.GhostRule && state.PendingGhostAutoRoleId == role.Id);
        if (!TryGetRoleAvailability(role, session.UserId, holdsThisCap, out var statusLocKey, in cache))
        {
            error = statusLocKey == null ? Loc.GetString("antag-tokens-error-role-unavailable-generic") : Loc.GetString(statusLocKey);
            return false;
        }

        if (IsRoleBlockedBySpecies(session, role))
        {
            error = Loc.GetString("antag-store-status-species-blocked");
            return false;
        }

        var useRoleCredit = state.RoleCredits.GetValueOrDefault(role.Id) > 0;
        EvaluateFreePurchaseFlags(role, session.UserId, state, useRoleCredit, out var useDonorDailyFree,
            out var usePublicRoundFree);

        if (!useRoleCredit && !useDonorDailyFree && !usePublicRoundFree && state.Balance < role.Cost)
        {
            error = Loc.GetString("antag-tokens-error-not-enough-tokens-short");
            return false;
        }

        if (role.Mode == AntagPurchaseMode.GhostRule &&
            !IsSessionEligibleForGhostRolePurchase(session))
        {
            error = Loc.GetString("antag-tokens-error-ghost-only");
            return false;
        }

        if (role.Mode == AntagPurchaseMode.GhostRule)
        {
            if (state.PendingGhostAutoRoleId != null)
            {
                if (!TryResolveGhostAutoPendingBeforePurchase(session.UserId, state))
                {
                    error = Loc.GetString("antag-tokens-error-ghost-auto-pending-other");
                    return false;
                }

                PersistState(session.UserId, state);
                SendState(session.UserId);
            }

            if (string.IsNullOrWhiteSpace(role.GhostAutoJoinEntityProto))
            {
                error = Loc.GetString("antag-tokens-error-ghost-auto-missing-proto");
                return false;
            }
        }

        if (role.Mode == AntagPurchaseMode.LobbyDeposit)
        {
            if (state.PendingDepositRoleId == role.Id)
            {
                error = Loc.GetString("antag-tokens-error-deposit-same-role");
                return false;
            }

            if (state.PendingDepositRoleId != null)
            {
                error = Loc.GetString("antag-tokens-error-deposit-other-active");
                return false;
            }

            SpendForRole(state, role, useRoleCredit, useDonorDailyFree, usePublicRoundFree);
            state.PendingDepositRoleId = role.Id;
            state.PendingDepositQueuedAtUtc = DateTime.UtcNow;
            state.PendingDepositUsedRoleCredit = useRoleCredit;
            state.PendingDepositUsedDonorDailyFree = useDonorDailyFree;
            PersistState(session.UserId, state);
            SendState(session.UserId);
            return true;
        }

        if (role.Mode == AntagPurchaseMode.GhostRule)
        {
            if (_globallyClaimedGhostRoles.Contains(role.Id))
            {
                error = Loc.GetString("antag-store-status-ghost-taken-by-other");
                return false;
            }

            _globallyClaimedGhostRoles.Add(role.Id);

            SpendForRole(state, role, useRoleCredit, useDonorDailyFree, usePublicRoundFree);
            state.PendingGhostAutoRoleId = role.Id;
            state.PendingGhostAutoQueuedAtUtc = DateTime.UtcNow;
            state.PendingGhostAutoUsedRoleCredit = useRoleCredit;
            state.PendingGhostAutoUsedDonorDailyFree = useDonorDailyFree;
        }

        if (role.GameRuleId == null || !_gameTicker.StartGameRule(role.GameRuleId, out _))
        {
            error = Loc.GetString("antag-tokens-error-event-start-failed");
            if (role.Mode == AntagPurchaseMode.GhostRule)
            {
                _globallyClaimedGhostRoles.Remove(role.Id);
                RefundRolePurchase(state, role, useRoleCredit, useDonorDailyFree);
                ClearPendingGhostAuto(state);
                PersistState(session.UserId, state);
                SendState(session.UserId);
                BroadcastAntagTokenUiRefresh();
            }

            return false;
        }

        if (role.Mode != AntagPurchaseMode.GhostRule)
            SpendForRole(state, role, useRoleCredit, useDonorDailyFree, usePublicRoundFree);

        PersistState(session.UserId, state);
        SendState(session.UserId);
        if (role.Mode == AntagPurchaseMode.GhostRule)
            BroadcastAntagTokenUiRefresh();
        return true;
    }

    public bool ClearDeposit(NetUserId userId, out string? error)
    {
        error = null;
        var state = EnsureStateExists(userId);
        if (state == null)
        {
            error = Loc.GetString("antag-tokens-error-currency-not-loaded");
            return false;
        }

        if (state.PendingDepositRoleId == null)
        {
            error = Loc.GetString("antag-tokens-error-no-deposit");
            return false;
        }

        if (StillOccupiesAntagTrack(userId))
        {
            error = Loc.GetString("antag-tokens-error-deposit-consumed");
            return false;
        }

        RefundPendingDeposit(userId, state);
        PersistState(userId, state);
        SendState(userId);
        return true;
    }

    public void SetSponsorLevelOverride(NetUserId userId, int? sponsorLevel)
    {
        if (sponsorLevel is <= 0)
            _sponsorLevelOverrides.Remove(userId);
        else
            _sponsorLevelOverrides[userId] = sponsorLevel;

        SendState(userId);
    }

    public int GetEffectiveSponsorLevel(NetUserId userId)
    {
        if (_sponsorLevelOverrides.TryGetValue(userId, out var overrideLevel) &&
            overrideLevel is > 0)
        {
            return overrideLevel.Value;
        }

        return EntitySystem.Get<SponsorSystem>().Sponsors
            .FirstOrDefault(s => s.Uid == userId.UserId.ToString()).Level;
    }

    public bool TryGetOnlineRewardUiState(
        NetUserId userId,
        DateTime nowUtc,
        out TimeSpan elapsed,
        out List<TimeSpan> grantedThresholds)
    {
        elapsed = TimeSpan.Zero;
        grantedThresholds = new List<TimeSpan>();
        if (!_onlineRewards.TryGetValue(userId, out var rewardState))
            return false;

        rewardState.EnsureCurrentCycle(nowUtc);
        elapsed = rewardState.GetElapsed(nowUtc);
        foreach (var t in rewardState.GrantedThresholds)
            grantedThresholds.Add(t);
        return true;
    }

    private async Task LoadPlayerData(ICommonSession player, CancellationToken cancel)
    {
        var tokenEntries = await _db.GetPlayerAntagTokens(player.UserId.UserId, cancel);
        var selection = await _db.GetPlayerAntagTokenSelection(player.UserId.UserId, cancel);

        var state = BuildPlayerTokenStateFromRows(tokenEntries, selection);

        var prevMonthlyYear = state.MonthlyYear;
        var prevMonthlyMonth = state.MonthlyMonth;
        NormalizeMonthlyState(state, DateTime.UtcNow, player.UserId);

        if (state.GhostAntagConsumedMark && state.PendingGhostAutoRoleId != null)
        {
            Logger.InfoS("AntagTokens",
                $"Removing stale ghost-auto pending for {player.UserId.UserId} (consumption mark indicates role was taken)");
            ClearPendingGhostAuto(state);
            PersistState(player.UserId, state);
        }

        _states[player.UserId] = state;
        if (state.PendingGhostAutoRoleId is { } ghostPendingId &&
            _listings.TryGetListing(ghostPendingId, out var ghostListing) &&
            ghostListing.Mode == AntagPurchaseMode.GhostRule &&
            _globallyClaimedGhostRoles.Add(ghostPendingId))
        {
            BroadcastAntagTokenUiRefresh();
        }

        var now = DateTime.UtcNow;
        _onlineRewards.TryAdd(player.UserId, new OnlineRewardState());
        _onlineRewards[player.UserId].Resume(now);
        if (prevMonthlyYear != state.MonthlyYear || prevMonthlyMonth != state.MonthlyMonth)
            PersistState(player.UserId, state);

        EntityManager.System<JobUnlockSystem>().SendJobUnlocksIfOnline(player.UserId);
        EntityManager.System<AntagUnlockSystem>().SendRoleBypassStateIfOnline(player.UserId);
    }

    private PlayerTokenState BuildPlayerTokenStateFromRows(
        List<PlayerAntagToken> tokenEntries,
        PlayerAntagTokenSelection? selection)
    {
        var state = new PlayerTokenState();
        foreach (var token in tokenEntries)
        {
            switch (token.TokenId)
            {
                case AntagTokenCatalog.BalanceEntryId:
                    state.Balance = Math.Max(0, token.Amount);
                    break;
                case AntagTokenCatalog.MonthlyEarnedEntryId:
                    state.MonthlyEarned = Math.Max(0, token.Amount);
                    break;
                case AntagTokenCatalog.MonthlyYearEntryId:
                    state.MonthlyYear = token.Amount;
                    break;
                case AntagTokenCatalog.MonthlyMonthEntryId:
                    state.MonthlyMonth = token.Amount;
                    break;
                case AntagTokenCatalog.DepositUsedRoleCreditEntryId:
                    state.PendingDepositUsedRoleCredit = token.Amount > 0;
                    break;
                case AntagTokenCatalog.LastDonorBonusClaimEntryId:
                    state.LastDonorBonusClaimUtc = DecodeUnixSeconds(token.Amount);
                    break;
                case AntagTokenCatalog.GhostAutoPendingUsedRoleCreditEntryId:
                    state.PendingGhostAutoUsedRoleCredit = token.Amount > 0;
                    break;
                case AntagTokenCatalog.GhostAntagConsumedMarkEntryId:
                    state.GhostAntagConsumedMark = token.Amount > 0;
                    break;
                case AntagTokenCatalog.LastDonorDailyFreeAntagDayEntryId:
                    state.LastDonorDailyFreeAntagDay = token.Amount;
                    break;
                case AntagTokenCatalog.DepositUsedDonorDailyFreeEntryId:
                    state.PendingDepositUsedDonorDailyFree = token.Amount > 0;
                    break;
                case AntagTokenCatalog.GhostAutoPendingUsedDonorDailyFreeEntryId:
                    state.PendingGhostAutoUsedDonorDailyFree = token.Amount > 0;
                    break;
                default:
                    if (token.TokenId.StartsWith("ghost-auto-pending:", StringComparison.Ordinal) &&
                        token.Amount > 0)
                    {
                        state.PendingGhostAutoRoleId = token.TokenId["ghost-auto-pending:".Length..];
                        state.PendingGhostAutoQueuedAtUtc = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
                    }
                    else if (token.TokenId.StartsWith(AntagUnlockCatalog.EntryPrefix, StringComparison.Ordinal) &&
                             token.Amount > 0)
                    {
                        var antagId = token.TokenId[AntagUnlockCatalog.EntryPrefix.Length..];
                        state.AntagUnlocks.Add(new ProtoId<AntagPrototype>(antagId));
                    }
                    else if (token.TokenId.StartsWith(JobUnlockCatalog.EntryPrefix, StringComparison.Ordinal) &&
                             token.Amount > 0)
                    {
                        var jobId = token.TokenId[JobUnlockCatalog.EntryPrefix.Length..];
                        state.JobUnlocks.Add(new ProtoId<JobPrototype>(jobId));
                    }
                    else if (token.TokenId.StartsWith("role-credit:", StringComparison.Ordinal) &&
                        token.Amount > 0)
                    {
                        var roleId = token.TokenId["role-credit:".Length..];
                        state.RoleCredits[roleId] = token.Amount;
                    }
                    break;
            }
        }

        if (selection?.TokenId == AntagTokenCatalog.DepositSelectionTokenId &&
            selection.AntagId is { Length: > 0 } selectedRoleId &&
            _listings.TryGetListing(selectedRoleId, out var role) &&
            role.Mode == AntagPurchaseMode.LobbyDeposit)
        {
            state.PendingDepositRoleId = selectedRoleId;
            state.PendingDepositQueuedAtUtc = selection.SelectedAt;
        }

        return state;
    }

    private static bool TokenStatesEqual(PlayerTokenState a, PlayerTokenState b)
    {
        if (a.Balance != b.Balance)
            return false;
        if (a.MonthlyEarned != b.MonthlyEarned)
            return false;
        if (a.MonthlyYear != b.MonthlyYear)
            return false;
        if (a.MonthlyMonth != b.MonthlyMonth)
            return false;
        if (a.LastDonorBonusClaimUtc != b.LastDonorBonusClaimUtc)
            return false;
        if (a.PendingDepositRoleId != b.PendingDepositRoleId)
            return false;
        if (a.PendingDepositQueuedAtUtc != b.PendingDepositQueuedAtUtc)
            return false;
        if (a.PendingDepositUsedRoleCredit != b.PendingDepositUsedRoleCredit)
            return false;
        if (a.PendingGhostAutoRoleId != b.PendingGhostAutoRoleId)
            return false;
        if (a.PendingGhostAutoQueuedAtUtc != b.PendingGhostAutoQueuedAtUtc)
            return false;
        if (a.PendingGhostAutoUsedRoleCredit != b.PendingGhostAutoUsedRoleCredit)
            return false;
        if (a.GhostAntagConsumedMark != b.GhostAntagConsumedMark)
            return false;
        if (a.LastDonorDailyFreeAntagDay != b.LastDonorDailyFreeAntagDay)
            return false;
        if (a.PendingDepositUsedDonorDailyFree != b.PendingDepositUsedDonorDailyFree)
            return false;
        if (a.PendingGhostAutoUsedDonorDailyFree != b.PendingGhostAutoUsedDonorDailyFree)
            return false;
        if (a.RoleCredits.Count != b.RoleCredits.Count)
            return false;

        foreach (var (key, amount) in a.RoleCredits)
        {
            if (!b.RoleCredits.TryGetValue(key, out var other) || other != amount)
                return false;
        }

        if (a.JobUnlocks.Count != b.JobUnlocks.Count)
            return false;

        foreach (var jobId in a.JobUnlocks)
        {
            if (!b.JobUnlocks.Contains(jobId))
                return false;
        }

        if (a.AntagUnlocks.Count != b.AntagUnlocks.Count)
            return false;

        foreach (var antagId in a.AntagUnlocks)
        {
            if (!b.AntagUnlocks.Contains(antagId))
                return false;
        }

        return true;
    }

    private void ApplyDbSnapshotToOnlineUser(
        NetUserId userId,
        List<PlayerAntagToken> tokenEntries,
        PlayerAntagTokenSelection? selection)
    {
        if (!_states.ContainsKey(userId))
            return;

        if (!_playerManager.TryGetSessionById(userId, out _))
            return;

        var newState = BuildPlayerTokenStateFromRows(tokenEntries, selection);
        var prevMonthlyYear = newState.MonthlyYear;
        var prevMonthlyMonth = newState.MonthlyMonth;

        if (_roundGrantedGhostRule.Contains(userId) || newState.GhostAntagConsumedMark)
            ClearPendingGhostAuto(newState);

        if (_roundGrantedLobbyAntag.Contains(userId))
        {
            newState.PendingDepositRoleId = null;
            newState.PendingDepositQueuedAtUtc = null;
            newState.PendingDepositUsedRoleCredit = false;
            newState.PendingDepositUsedDonorDailyFree = false;
        }

        NormalizeMonthlyState(newState, DateTime.UtcNow, userId);

        if (_states.TryGetValue(userId, out var oldState) && TokenStatesEqual(oldState, newState))
            return;

        _states[userId] = newState;
        if (prevMonthlyYear != newState.MonthlyYear || prevMonthlyMonth != newState.MonthlyMonth)
            PersistState(userId, newState);
        SendState(userId);
    }

    private Task WaitMainThreadApplyAsync(
        NetUserId userId,
        List<PlayerAntagToken> tokenEntries,
        PlayerAntagTokenSelection? selection)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _taskManager.RunOnMainThread(() =>
        {
            try
            {
                ApplyDbSnapshotToOnlineUser(userId, tokenEntries, selection);
                tcs.SetResult();
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
        });

        return tcs.Task;
    }

    private async Task RunPeriodicDatabaseSyncAsync()
    {
        if (_databaseSyncPassRunning)
            return;

        _databaseSyncPassRunning = true;
        try
        {
            foreach (var session in _playerManager.Sessions.ToArray())
            {
                if (!_states.ContainsKey(session.UserId))
                    continue;

                if (!_userDb.IsLoadComplete(session))
                    continue;

                List<PlayerAntagToken> tokenEntries;
                PlayerAntagTokenSelection? selection;

                try
                {
                    tokenEntries = await _db.GetPlayerAntagTokens(session.UserId.UserId, default);
                    selection = await _db.GetPlayerAntagTokenSelection(session.UserId.UserId, default);
                }
                catch (Exception e)
                {
                    Logger.ErrorS("AntagTokens", $"Periodic antag token DB sync read failed for {session.UserId}: {e}");
                    continue;
                }

                await WaitMainThreadApplyAsync(session.UserId, tokenEntries, selection);
            }
        }
        finally
        {
            _databaseSyncPassRunning = false;
        }
    }

    public void RequestAntagTokenDatabaseSync(ICommonSession session)
    {
        if (!_states.ContainsKey(session.UserId))
            return;

        _ = RequestAntagTokenDatabaseSyncAsync(session);
    }

    private async Task RequestAntagTokenDatabaseSyncAsync(ICommonSession session)
    {
        var userId = session.UserId;

        List<PlayerAntagToken> tokenEntries;
        PlayerAntagTokenSelection? selection;

        try
        {
            tokenEntries = await _db.GetPlayerAntagTokens(userId.UserId, default);
            selection = await _db.GetPlayerAntagTokenSelection(userId.UserId, default);
        }
        catch (Exception e)
        {
            Logger.ErrorS("AntagTokens", $"Manual antag token DB sync read failed for {userId}: {e}");
            return;
        }

        await WaitMainThreadApplyAsync(userId, tokenEntries, selection);
    }

    private async void OnPlayerDisconnect(ICommonSession player)
    {
        if (_states.TryGetValue(player.UserId, out var state))
        {
             await PersistStateAsync(player.UserId, state);
        }

        if (_onlineRewards.TryGetValue(player.UserId, out var rewardState))
            rewardState.Pause(DateTime.UtcNow);

        _states.Remove(player.UserId);
    }
    private void PersistState(NetUserId userId, PlayerTokenState state)
    {
        _ = PersistStateAsync(userId, state).ContinueWith(t =>
        {
            if (t.IsFaulted)
            Logger.ErrorS("AntagTokens", $"Failed to save state for {userId}: {t.Exception}");
        });
}
    private void OnJoinedLobby(PlayerJoinedLobbyEvent ev)
    {
        var now = DateTime.UtcNow;
        _onlineRewards.TryAdd(ev.PlayerSession.UserId, new OnlineRewardState());
        _onlineRewards[ev.PlayerSession.UserId].Resume(now);
    }

    internal void TestSetLastDonorBonusClaimUtc(NetUserId userId, DateTime? utc)
    {
        if (!_states.TryGetValue(userId, out var state))
            throw new InvalidOperationException("Antag token state is not loaded for this user.");

        state.LastDonorBonusClaimUtc = utc;
    }

    private void OnRoundStarting(RoundStartingEvent _)
    {
        _ghostMinimumTimeRandomBonusByRole.Clear();
        foreach (var role in _listings.ListingsOrdered)
        {
            if (role.Mode != AntagPurchaseMode.GhostRule || role.MinimumTimeFromRoundStart <= 0)
                continue;

            _ghostMinimumTimeRandomBonusByRole[role.Id] = _random.Next(-300, 901);
        }
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent _)
    {
        _ghostMinimumTimeRandomBonusByRole.Clear();
        _globallyClaimedGhostRoles.Clear();
        _lastRoundPurchasedRoles.Clear();
        foreach (var roleId in _currentRoundPurchasedRoles)
            _lastRoundPurchasedRoles.Add(roleId);
        _currentRoundPurchasedRoles.Clear();

        var grantedGhost = new HashSet<NetUserId>(_roundGrantedGhostRule);
        _roundGrantedLobbyAntag.Clear();
        _roundGrantedGhostRule.Clear();

        foreach (var (userId, state) in _states)
        {
            if (state.PendingGhostAutoRoleId == null)
                continue;

            if (grantedGhost.Contains(userId) || state.GhostAntagConsumedMark)
            {
                ClearPendingGhostAuto(state);
                PersistState(userId, state);
                SendState(userId);
                continue;
            }

            RefundPendingGhostAuto(userId, state);
            PersistState(userId, state);
            SendState(userId);
        }

        foreach (var (userId, state) in _states)
        {
            if (!state.GhostAntagConsumedMark)
                continue;

            state.GhostAntagConsumedMark = false;
            PersistState(userId, state);
        }

        SaveAll();
    }

    private void OnRoundstartJobsAssigned(RulePlayerJobsAssignedEvent ev)
    {
        foreach (var session in ev.Players)
        {
            if (!TryGetPendingLobbyRole(session.UserId, out var role))
                continue;

            var state = EnsureStateExists(session.UserId);
            if (state == null)
                continue;

            var cache = BuildSendStateCache(session.UserId);
            if (!TryGetRoleAvailability(role, session.UserId, purchased: true, out var statusLocKey, in cache))
            {
                RefundPendingDeposit(session.UserId, state);
                PersistState(session.UserId, state);
                SendState(session.UserId);
                ShowPopup(session, statusLocKey == null
                    ? Loc.GetString("antag-tokens-popup-deposit-cancelled-refund")
                    : Loc.GetString("antag-tokens-popup-deposit-cancelled-reason-refund", ("reason", Loc.GetString(statusLocKey))));
                continue;
            }

            if (IsReservedRoleBlockedByCurrentJob(session, role))
            {
                RefundPendingDeposit(session.UserId, state);
                PersistState(session.UserId, state);
                SendState(session.UserId);
                ShowPopup(session, Loc.GetString("antag-tokens-popup-job-blocks-queued"));
                continue;
            }

            if (!TryAssignReservedRoundstartRole(session, role, out var assignError))
            {
                RefundPendingDeposit(session.UserId, state);
                PersistState(session.UserId, state);
                SendState(session.UserId);
                ShowPopup(session, assignError ?? Loc.GetString("antag-tokens-error-assign-failed-refund"));
                continue;
            }

            state.PendingDepositRoleId = null;
            state.PendingDepositQueuedAtUtc = null;
            state.PendingDepositUsedRoleCredit = false;
            state.PendingDepositUsedDonorDailyFree = false;
            MarkLobbyTokenAntagGranted(session.UserId, role.Id);
            PersistState(session.UserId, state);
            SendState(session.UserId);
            ShowPopup(session, Loc.GetString("antag-tokens-popup-role-assigned", ("role", Loc.GetString(role.NameLocKey))));
        }
    }

    private void OnOpenRequest(AntagTokenOpenRequestEvent _, EntitySessionEventArgs args)
    {
        if (!_storeEnabled)
        {
            ShowPopup(args.SenderSession, Loc.GetString("antag-tokens-error-store-disabled"));
            return;
        }

        SendState(args.SenderSession.UserId);
    }

    private void OnPurchaseRequest(AntagTokenPurchaseRequestEvent ev, EntitySessionEventArgs args)
    {
        if (!TryPurchaseForSession(args.SenderSession, ev.RoleId, out var error))
        {
            ShowPopup(args.SenderSession, error ?? Loc.GetString("antag-tokens-error-purchase-unavailable"));
            SendState(args.SenderSession.UserId);
            return;
        }

        if (!_listings.TryGetListing(ev.RoleId, out var role))
            return;

        var message = role.Mode == AntagPurchaseMode.GhostRule
            ? Loc.GetString("antag-tokens-popup-purchase-ghost")
            : Loc.GetString("antag-tokens-popup-purchase-deposit");

        ShowPopup(args.SenderSession, message);
        SendState(args.SenderSession.UserId);
    }

    private void OnClearRequest(AntagTokenClearRequestEvent _, EntitySessionEventArgs args)
    {
        if (!ClearDeposit(args.SenderSession.UserId, out var error))
        {
            ShowPopup(args.SenderSession, error ?? Loc.GetString("antag-tokens-error-clear-deposit-failed"));
            return;
        }

        ShowPopup(args.SenderSession, Loc.GetString("antag-tokens-popup-deposit-cleared"));
        SendState(args.SenderSession.UserId);
    }

    private void OnExcludeReservedSession(Entity<AntagSelectionComponent> _, ref AntagSelectionExcludeSessionEvent args)
    {
        args.Excluded = HasPendingLobbyDeposit(args.Session.UserId);
    }

    private bool TryGetPendingLobbyRole(NetUserId userId, [NotNullWhen(true)] out AntagRoleDefinition? role)
    {
        role = null;

        if (!_states.TryGetValue(userId, out var state) ||
            state.PendingDepositRoleId == null ||
            !_listings.TryGetListing(state.PendingDepositRoleId, out var selectedRole) ||
            selectedRole.Mode != AntagPurchaseMode.LobbyDeposit ||
            selectedRole.AntagId == null ||
            selectedRole.GameRuleId == null)
        {
            return false;
        }

        role = selectedRole;
        return true;
    }

    private static bool MatchesDefinition(string antagId, AntagSelectionDefinition definition)
    {
        return definition.PrefRoles.Contains(antagId) || definition.FallbackRoles.Contains(antagId);
    }

    private bool HasPendingLobbyDeposit(NetUserId userId)
    {
        return TryGetPendingLobbyRole(userId, out _);
    }

    private bool IsSessionEligibleForGhostRolePurchase(ICommonSession session)
    {
        if (TypanStationWarRuleSystem.IsModeActive)
            return false;

        if (session.AttachedEntity is not { Valid: true } attached)
            return false;

        return TryComp<GhostComponent>(attached, out var ghost) && ghost.CanTakeGhostRoles;
    }

    private bool HasConsumedTokenAntagGrantThisRound(NetUserId userId)
    {
        return _roundGrantedLobbyAntag.Contains(userId) || _roundGrantedGhostRule.Contains(userId);
    }

    private bool StillOccupiesAntagTrack(NetUserId userId)
    {
        if (!_roundGrantedLobbyAntag.Contains(userId))
            return false;

        if (!_playerManager.TryGetSessionById(userId, out var session))
            return false;

        if (!_mind.TryGetMind(session, out var mindId, out var mindComp))
            return false;

        if (!_role.MindIsAntagonist(mindId))
            return false;

        return !_mind.IsCharacterDeadIc(mindComp);
    }

    private bool StillOccupiesGhostRuleGrant(NetUserId userId)
    {
        if (!_roundGrantedGhostRule.Contains(userId))
            return false;

        if (!_playerManager.TryGetSessionById(userId, out var session))
            return false;

        if (!_mind.TryGetMind(session, out _, out var mindComp))
            return false;

        return !_mind.IsCharacterDeadIc(mindComp);
    }

    private bool CountsTowardAntagTrackSlot(NetUserId userId)
    {
        if (_states.TryGetValue(userId, out var state))
        {
            if (state.PendingDepositRoleId != null &&
                _listings.TryGetListing(state.PendingDepositRoleId, out var depListing) &&
                depListing.Mode == AntagPurchaseMode.LobbyDeposit)
            {
                return true;
            }
        }

        return StillOccupiesAntagTrack(userId);
    }

    private bool CountsTowardGhostTrackSlot(NetUserId userId)
    {
        if (_states.TryGetValue(userId, out var state) && state.PendingGhostAutoRoleId != null)
            return true;

        return StillOccupiesGhostRuleGrant(userId);
    }

    private void MarkLobbyTokenAntagGranted(NetUserId userId, string roleId)
    {
        _roundGrantedLobbyAntag.Add(userId);
        _currentRoundPurchasedRoles.Add(roleId);
    }

    private void MarkGhostRuleTokenGranted(NetUserId userId, string roleId)
    {
        _roundGrantedGhostRule.Add(userId);
        _currentRoundPurchasedRoles.Add(roleId);
        if (_states.TryGetValue(userId, out var state))
            state.GhostAntagConsumedMark = true;
    }

    private bool TryAssignReservedRoundstartRole(ICommonSession session, AntagRoleDefinition role, out string? error)
    {
        error = null;

        if (session.Status is SessionStatus.Disconnected or SessionStatus.Zombie)
        {
            error = Loc.GetString("antag-tokens-error-session-invalid");
            return false;
        }

        if (session.AttachedEntity is not { Valid: true })
        {
            error = Loc.GetString("antag-tokens-error-no-entity");
            return false;
        }

        if (_mind.TryGetMind(session, out var mindId, out _) && _role.MindIsAntagonist(mindId))
        {
            error = Loc.GetString("antag-tokens-error-already-antag");
            return false;
        }

        if (StillOccupiesAntagTrack(session.UserId))
        {
            error = Loc.GetString("antag-tokens-error-round-limit");
            return false;
        }

        var ruleEntity = TryResolveGameRuleForTokenRole(role, out var existingRule)
            ? existingRule
            : _gameTicker.AddGameRule(role.GameRuleId!);
        if (!TryComp<AntagSelectionComponent>(ruleEntity, out var selection))
        {
            error = Loc.GetString("antag-tokens-error-rule-missing-antag-selection");
            return false;
        }

        if (!TryFindMatchingDefinition(selection, role.AntagId!, out var definition))
        {
            error = Loc.GetString("antag-tokens-error-no-matching-definition");
            return false;
        }

        var chosenDefinition = definition ?? throw new InvalidOperationException("Matching antag definition was null after successful lookup.");
        _antagSelection.MakeAntag((ruleEntity, selection), session, chosenDefinition);

        if (IsXenomorphTokenRole(role) &&
            !EnsureSessionHasXenomorphBody(session, out error))
        {
            return false;
        }

        return true;
    }

    private static bool TryFindMatchingDefinition(
        AntagSelectionComponent selection,
        string antagId,
        [NotNullWhen(true)] out AntagSelectionDefinition? definition)
    {
        foreach (var def in selection.Definitions)
        {
            if (!MatchesDefinition(antagId, def))
                continue;

            definition = def;
            return true;
        }

        definition = null;
        return false;
    }

    private static bool IsXenomorphTokenRole(AntagRoleDefinition role)
    {
        return role.Id == "xenomorph" ||
               role.AntagId == "XenomorphsInfestationRoundstart";
    }

    private bool EnsureSessionHasXenomorphBody(ICommonSession session, out string? error)
    {
        error = null;

        if (session.AttachedEntity is { Valid: true } attached &&
            HasComp<Content.Shared._White.Xenomorphs.Xenomorph.XenomorphComponent>(attached))
        {
            return true;
        }

        if (!_mind.TryGetMind(session, out var mindId, out _))
        {
            error = Loc.GetString("antag-tokens-error-no-entity");
            return false;
        }

        EntityCoordinates coords;
        EntityUid? oldBody = null;
        if (session.AttachedEntity is { Valid: true } body)
        {
            oldBody = body;
            coords = Transform(body).Coordinates;
        }
        else if (!TryGetXenomorphSpawnCoordinates(out coords))
        {
            error = Loc.GetString("antag-tokens-error-assign-failed-refund");
            return false;
        }

        var larva = Spawn("MobXenomorphLarva", coords);
        _transform.AttachToGridOrMap(larva);
        _mind.TransferTo(mindId, larva, ghostCheckOverride: true);

        if (oldBody is { } uid && uid != larva)
            QueueDel(uid);

        return true;
    }

    private bool TryGetXenomorphSpawnCoordinates(out EntityCoordinates coordinates)
    {
        var query = EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out _, out var meta, out var xform))
        {
            if (meta.EntityPrototype?.ID != "SpawnPointGhostXenomorph")
                continue;

            coordinates = xform.Coordinates;
            return true;
        }

        coordinates = default;
        return false;
    }

    private bool IsReservedRoleBlockedByCurrentJob(ICommonSession session, AntagRoleDefinition role)
    {
        if (!_mind.TryGetMind(session, out var mindId, out _) ||
            !_jobs.MindTryGetJobId(mindId, out var jobId) ||
            jobId == null)
        {
            return false;
        }

        if (role.JobBlacklist is { Count: > 0 } && role.JobBlacklist.Contains(jobId.Value))
            return true;

        if (!_jobs.TryGetAllDepartments(jobId.Value, out var departments))
            return false;

        return departments.Any(d => d.ID is "Command" or "Security" or "Silicon" or "Typan" or "Typan2");
    }

    private bool IsRoleBlockedBySpecies(ICommonSession session, AntagRoleDefinition role)
    {
        if (role.SpeciesBlacklist is not { Count: > 0 })
            return false;

        var species = TryGetSessionSpecies(session);
        return species != null && role.SpeciesBlacklist.Contains(species.Value);
    }

    private ProtoId<SpeciesPrototype>? TryGetSessionSpecies(ICommonSession session)
    {
        if (session.AttachedEntity is { Valid: true } attached &&
            TryComp<HumanoidAppearanceComponent>(attached, out var humanoid))
        {
            return humanoid.Species;
        }

        if (_preferences.TryGetCachedPreferences(session.UserId, out var prefs) &&
            prefs.SelectedCharacter is HumanoidCharacterProfile profile)
        {
            return profile.Species;
        }

        return null;
    }

    private bool TryGetRoleAvailability(AntagRoleDefinition role, NetUserId userId, bool purchased, out string? statusLocKey)
    {
        var cache = BuildSendStateCache(userId);
        return TryGetRoleAvailability(role, userId, purchased, out statusLocKey, in cache);
    }

    private bool TryGetRoleAvailability(AntagRoleDefinition role, NetUserId userId, bool purchased, out string? statusLocKey, in AntagSendStateCache cache)
    {
        statusLocKey = null;

        if (role.Mode == AntagPurchaseMode.Unavailable)
        {
            statusLocKey = role.UnavailableReasonLocKey ?? "antag-store-status-unavailable";
            return false;
        }

        if (!purchased && HasConsumedTokenAntagGrantThisRound(userId))
        {
            statusLocKey = "antag-tokens-error-round-limit";
            return false;
        }

        if (!purchased &&
            _states.TryGetValue(userId, out var selState))
        {
            if (role.Mode == AntagPurchaseMode.LobbyDeposit && selState.PendingGhostAutoRoleId != null)
            {
                statusLocKey = "antag-tokens-error-round-limit";
                return false;
            }

            if (role.Mode == AntagPurchaseMode.GhostRule && selState.PendingDepositRoleId != null)
            {
                statusLocKey = "antag-tokens-error-round-limit";
                return false;
            }
        }

        if (role.MinimumPlayers > 0 && cache.PlayerCount < role.MinimumPlayers)
        {
            statusLocKey = "antag-store-status-min-players";
            return false;
        }

        if (GetCooldownRemaining(role, in cache) > 0)
        {
            statusLocKey = null;
            return false;
        }

        if (role.RequiresInRound && !cache.InRound)
        {
            statusLocKey = "antag-store-status-round-only";
            return false;
        }

        if (role.RequiresPreRoundLobby && !cache.InPreRoundLobby)
        {
            statusLocKey = "antag-store-status-lobby-only";
            return false;
        }

        if (cache.RoundstartBlockedByPreset &&
            role.Mode is AntagPurchaseMode.LobbyDeposit or AntagPurchaseMode.GhostRule)
        {
            statusLocKey = "antag-store-status-unavailable";
            return false;
        }

        if (!string.IsNullOrEmpty(role.RequiresPresetGameRuleId))
        {
            if (cache.InRound)
            {
                if (!_gameTicker.IsGameRuleAdded(role.RequiresPresetGameRuleId))
                {
                    statusLocKey = "antag-store-status-unavailable";
                    return false;
                }
            }
            else if (IsPresetMissingRequiredGameRule(role.RequiresPresetGameRuleId))
            {
                statusLocKey = "antag-store-status-unavailable";
                return false;
            }
        }

        if (role.Mode == AntagPurchaseMode.LobbyDeposit && !purchased && IsAntagTrackGloballySaturated(in cache))
        {
            statusLocKey = "antag-store-status-saturated";
            return false;
        }

        if (role.Mode == AntagPurchaseMode.GhostRule && !purchased && IsGhostTrackGloballySaturated(in cache))
        {
            statusLocKey = "antag-store-status-saturated";
            return false;
        }

        if (role.Mode == AntagPurchaseMode.GhostRule && !purchased &&
            _globallyClaimedGhostRoles.Contains(role.Id))
        {
            statusLocKey = "antag-store-status-ghost-taken-by-other";
            return false;
        }

        if (!purchased &&
            !AntagTokenCatalog.IsExemptFromLastRoundPurchaseRepeat(role.Id, role.Mode) &&
            _lastRoundPurchasedRoles.Contains(role.Id))
        {
            statusLocKey = "antag-store-status-last-round-purchased";
            return false;
        }

        if (!purchased &&
            _playerManager.TryGetSessionById(userId, out var session))
        {
            if (IsRoleBlockedBySpecies(session, role))
            {
                statusLocKey = "antag-store-status-species-blocked";
                return false;
            }

            if (IsReservedRoleBlockedByCurrentJob(session, role))
            {
                statusLocKey = "antag-store-status-job-blocked";
                return false;
            }
        }

        return true;
    }

    private AntagSendStateCache BuildSendStateCache(NetUserId userId)
    {
        EnforceSharedTokenSlotCapacity();

        var playerCount = _playerManager.PlayerCount;
        var inRound = _gameTicker.RunLevel == GameRunLevel.InRound;
        var inPreRound = _gameTicker.RunLevel == GameRunLevel.PreRoundLobby;
        var elapsed = inRound ? (int) _gameTicker.RoundDuration().TotalSeconds : 0;
        var roundstartBlocked = IsRoundstartRoleBlockedByPreset();
        var maxSlots = Math.Max(1, playerCount / SharedTokenSlotsPlayersPerSlot);
        var depositCounts = new Dictionary<string, int>();
        var externalAntagSlotTotal = 0;
        var externalGhostSlotTotal = 0;
        foreach (var kv in _states)
        {
            if (kv.Key == userId)
                continue;

            if (CountsTowardAntagTrackSlot(kv.Key))
                externalAntagSlotTotal++;

            if (CountsTowardGhostTrackSlot(kv.Key))
                externalGhostSlotTotal++;

            var rid = kv.Value.PendingDepositRoleId;
            if (rid == null)
                continue;

            if (!_listings.TryGetListing(rid, out var listed) || listed.Mode != AntagPurchaseMode.LobbyDeposit)
                continue;

            depositCounts[rid] = depositCounts.GetValueOrDefault(rid) + 1;
        }

        return new AntagSendStateCache(playerCount, roundstartBlocked, elapsed, inRound, inPreRound, maxSlots, externalAntagSlotTotal, externalGhostSlotTotal, depositCounts);
    }

    private void EnforceSharedTokenSlotCapacity()
    {
        if (_enforcingSharedTokenSlotCapacity)
            return;

        _enforcingSharedTokenSlotCapacity = true;
        try
        {
            var max = Math.Max(1, _playerManager.PlayerCount / SharedTokenSlotsPlayersPerSlot);
            EnforceAntagTrackPending(max);
            EnforceGhostTrackPending(max);
        }
        finally
        {
            _enforcingSharedTokenSlotCapacity = false;
        }
    }

    private void EnforceAntagTrackPending(int max)
    {
        var living = 0;
        foreach (var uid in _roundGrantedLobbyAntag)
        {
            if (StillOccupiesAntagTrack(uid))
                living++;
        }

        var pending = new List<(NetUserId UserId, DateTime QueuedAt)>();
        foreach (var (userId, state) in _states)
        {
            if (state.PendingDepositRoleId == null)
                continue;

            if (!_listings.TryGetListing(state.PendingDepositRoleId, out var depRole) ||
                depRole.Mode != AntagPurchaseMode.LobbyDeposit)
            {
                continue;
            }

            pending.Add((userId, state.PendingDepositQueuedAtUtc ?? DateTime.MinValue));
        }

        pending.Sort(static (a, b) => a.QueuedAt.CompareTo(b.QueuedAt));
        var slotForPending = max - living;
        if (slotForPending < 0)
            slotForPending = 0;

        if (pending.Count <= slotForPending)
            return;

        for (var i = slotForPending; i < pending.Count; i++)
        {
            var uid = pending[i].UserId;
            if (!_states.TryGetValue(uid, out var state) || state.PendingDepositRoleId == null)
                continue;

            RefundPendingDeposit(uid, state);
            PersistState(uid, state);
            SendState(uid);

            if (_playerManager.TryGetSessionById(uid, out var session))
            {
                ShowPopup(session, Loc.GetString("antag-tokens-popup-deposit-refunded-queue-cap"));
            }
        }
    }

    private void EnforceGhostTrackPending(int max)
    {
        var living = 0;
        foreach (var uid in _roundGrantedGhostRule)
        {
            if (StillOccupiesGhostRuleGrant(uid))
                living++;
        }

        var pending = new List<(NetUserId UserId, DateTime QueuedAt)>();
        foreach (var (userId, state) in _states)
        {
            if (state.PendingGhostAutoRoleId == null)
                continue;

            pending.Add((userId, state.PendingGhostAutoQueuedAtUtc ?? DateTime.MinValue));
        }

        pending.Sort(static (a, b) => a.QueuedAt.CompareTo(b.QueuedAt));
        var slotForPending = max - living;
        if (slotForPending < 0)
            slotForPending = 0;

        if (pending.Count <= slotForPending)
            return;

        for (var i = slotForPending; i < pending.Count; i++)
        {
            var uid = pending[i].UserId;
            if (!_states.TryGetValue(uid, out var state) || state.PendingGhostAutoRoleId == null)
                continue;

            RefundPendingGhostAuto(uid, state);
            PersistState(uid, state);
            SendState(uid);

            if (_playerManager.TryGetSessionById(uid, out var session))
            {
                ShowPopup(session, Loc.GetString("antag-tokens-popup-deposit-refunded-queue-cap"));
            }
        }
    }

    private int GetCooldownRemaining(AntagRoleDefinition role, in AntagSendStateCache cache)
    {
        if (role.MinimumTimeFromRoundStart <= 0 || !cache.InRound)
            return 0;

        var minimum = role.MinimumTimeFromRoundStart;
        if (role.Mode == AntagPurchaseMode.GhostRule &&
            _ghostMinimumTimeRandomBonusByRole.TryGetValue(role.Id, out var bonus))
            minimum += bonus;

        return Math.Max(0, minimum - cache.RoundElapsedSeconds);
    }

    private static bool IsAntagTrackGloballySaturated(in AntagSendStateCache cache)
    {
        return cache.ExternalAntagSlotTotal >= cache.MaxSharedTokenSlots;
    }

    private static bool IsGhostTrackGloballySaturated(in AntagSendStateCache cache)
    {
        return cache.ExternalGhostSlotTotal >= cache.MaxSharedTokenSlots;
    }

    private readonly struct AntagSendStateCache
    {
        public readonly int PlayerCount;
        public readonly bool RoundstartBlockedByPreset;
        public readonly int RoundElapsedSeconds;
        public readonly bool InRound;
        public readonly bool InPreRoundLobby;
        public readonly int MaxSharedTokenSlots;
        public readonly int ExternalAntagSlotTotal;
        public readonly int ExternalGhostSlotTotal;
        public readonly Dictionary<string, int> ExternalDepositCountByRole;

        public AntagSendStateCache(
            int playerCount,
            bool roundstartBlockedByPreset,
            int roundElapsedSeconds,
            bool inRound,
            bool inPreRoundLobby,
            int maxSharedTokenSlots,
            int externalAntagSlotTotal,
            int externalGhostSlotTotal,
            Dictionary<string, int> externalDepositCountByRole)
        {
            PlayerCount = playerCount;
            RoundstartBlockedByPreset = roundstartBlockedByPreset;
            RoundElapsedSeconds = roundElapsedSeconds;
            InRound = inRound;
            InPreRoundLobby = inPreRoundLobby;
            MaxSharedTokenSlots = maxSharedTokenSlots;
            ExternalAntagSlotTotal = externalAntagSlotTotal;
            ExternalGhostSlotTotal = externalGhostSlotTotal;
            ExternalDepositCountByRole = externalDepositCountByRole;
        }
    }

    private void SendState(NetUserId userId)
    {
        if (!_playerManager.TryGetSessionById(userId, out var session) ||
            !_states.TryGetValue(userId, out var state))
        {
            return;
        }

        if (ReconcileGhostAutoPending(userId, session, state))
            PersistState(userId, state);

        NormalizeMonthlyState(state, DateTime.UtcNow, userId);

        var cache = BuildSendStateCache(userId);
        var roles = new List<AntagTokenRoleEntry>(_listings.ListingCount);
        foreach (var role in _listings.ListingsOrdered)
        {
            var purchased = (role.Mode == AntagPurchaseMode.LobbyDeposit && state.PendingDepositRoleId == role.Id)
                || (role.Mode == AntagPurchaseMode.GhostRule && state.PendingGhostAutoRoleId == role.Id);
            var holdsCapForAvailability = purchased;
            var freeUnlocks = state.RoleCredits.GetValueOrDefault(role.Id);
            var useRoleCredit = freeUnlocks > 0;
            EvaluateFreePurchaseFlags(role, userId, state, useRoleCredit, out var donorDailyFree, out var publicRoundFree);
            var freePurchaseAvailable = !useRoleCredit && (donorDailyFree || publicRoundFree);
            var canAfford = useRoleCredit || freePurchaseAvailable || state.Balance >= role.Cost;
            var available = TryGetRoleAvailability(role, userId, holdsCapForAvailability, out var statusLocKey, in cache);
            var saturated = role.Mode == AntagPurchaseMode.LobbyDeposit && !holdsCapForAvailability &&
                IsAntagTrackGloballySaturated(in cache)
                || role.Mode == AntagPurchaseMode.GhostRule && !holdsCapForAvailability &&
                IsGhostTrackGloballySaturated(in cache);
            var cooldownRemaining = GetCooldownRemaining(role, in cache);

            if (purchased)
                statusLocKey = "antag-store-status-deposited";
            else if (state.PendingDepositRoleId != null && role.Mode == AntagPurchaseMode.LobbyDeposit)
                statusLocKey ??= "antag-store-status-has-other-deposit";
            else if (state.PendingGhostAutoRoleId != null && role.Mode == AntagPurchaseMode.GhostRule && state.PendingGhostAutoRoleId != role.Id)
                statusLocKey ??= "antag-store-status-has-other-deposit";
            else if (!canAfford)
                statusLocKey ??= "antag-store-status-not-enough";

            roles.Add(new AntagTokenRoleEntry(
                role.Id,
                role.Cost,
                role.Mode,
                purchased,
                freeUnlocks,
                canAfford,
                saturated,
                available,
                role.TagLocKey,
                statusLocKey,
                cooldownRemaining,
                freePurchaseAvailable));
        }

        var payload = new AntagTokenState(
            state.Balance,
            state.MonthlyEarned,
            GetMonthlyCap(userId),
            state.PendingDepositRoleId,
            roles);

        RaiseNetworkEvent(new AntagTokenStateEvent(payload), session);
    }

    private bool ReconcileGhostAutoPending(NetUserId userId, ICommonSession session, PlayerTokenState state)
    {
        if (state.PendingGhostAutoRoleId == null)
            return false;

        if (!_listings.TryGetListing(state.PendingGhostAutoRoleId, out var pendingRole))
        {
            ClearPendingGhostAuto(state);
            return true;
        }

        // Ghost token queue is only valid during an active round.
        // If the round has ended (or server restarted into lobby), stale pending must be refunded.
        // Ghost token queue is only valid during an active round.
        // If the round has ended (or server restarted into lobby), just clean up the pending state
        // without refunding. Refunds for truly unused tokens are handled in OnRoundRestartCleanup.
        if (_gameTicker.RunLevel != GameRunLevel.InRound)
        {
            ClearPendingGhostAuto(state);
            return true;
        }

        if (session.AttachedEntity is not { Valid: true } attached ||
            string.IsNullOrWhiteSpace(pendingRole.GhostAutoJoinEntityProto))
        {
            return false;
        }

        if (!IsGhostAutoJoinPrototypeMatch(MetaData(attached).EntityPrototype?.ID, pendingRole.GhostAutoJoinEntityProto))
            return false;

        // Recovery path for missed TakeGhostRoleEvent: consume pending if the player already controls the target role entity.
        var grantedRoleId = pendingRole.Id;
        ClearPendingGhostAuto(state);
        MarkGhostRuleTokenGranted(userId, grantedRoleId);
        return true;
    }

private async Task PersistStateAsync(NetUserId userId, PlayerTokenState state)
{
    var tasks = new List<Task>
    {
        _db.SetPlayerAntagTokenAmount(userId.UserId, AntagTokenCatalog.BalanceEntryId, state.Balance),
        _db.SetPlayerAntagTokenAmount(userId.UserId, AntagTokenCatalog.MonthlyEarnedEntryId, state.MonthlyEarned),
        _db.SetPlayerAntagTokenAmount(userId.UserId, AntagTokenCatalog.MonthlyYearEntryId, state.MonthlyYear),
        _db.SetPlayerAntagTokenAmount(userId.UserId, AntagTokenCatalog.MonthlyMonthEntryId, state.MonthlyMonth),
        _db.SetPlayerAntagTokenAmount(userId.UserId, AntagTokenCatalog.LastDonorBonusClaimEntryId, EncodeUnixSeconds(state.LastDonorBonusClaimUtc)),
        _db.SetPlayerAntagTokenAmount(userId.UserId, AntagTokenCatalog.DepositUsedRoleCreditEntryId, state.PendingDepositUsedRoleCredit ? 1 : 0),
        _db.SetPlayerAntagTokenAmount(userId.UserId, AntagTokenCatalog.LastDonorDailyFreeAntagDayEntryId, state.LastDonorDailyFreeAntagDay),
        _db.SetPlayerAntagTokenAmount(userId.UserId, AntagTokenCatalog.DepositUsedDonorDailyFreeEntryId, state.PendingDepositUsedDonorDailyFree ? 1 : 0)
    };

    foreach (var (role, amount) in state.RoleCredits)
    {
        tasks.Add(_db.SetPlayerAntagTokenAmount(userId.UserId, AntagTokenCatalog.GetRoleCreditEntryId(role), amount));
    }

    foreach (var jobId in state.JobUnlocks)
    {
        tasks.Add(_db.SetPlayerAntagTokenAmount(userId.UserId, JobUnlockCatalog.GetEntryId(jobId), 1));
    }

    foreach (var antagId in state.AntagUnlocks)
    {
        tasks.Add(_db.SetPlayerAntagTokenAmount(userId.UserId, AntagUnlockCatalog.GetEntryId(antagId), 1));
    }

    foreach (var listing in _listings.ListingsOrdered)
    {
        if (listing.Mode != AntagPurchaseMode.GhostRule || string.IsNullOrWhiteSpace(listing.GhostAutoJoinEntityProto))
            continue;

        var entryId = AntagTokenCatalog.GetGhostAutoPendingEntryId(listing.Id);
        var amount = state.PendingGhostAutoRoleId == listing.Id ? 1 : 0;
        tasks.Add(_db.SetPlayerAntagTokenAmount(userId.UserId, entryId, amount));
    }

    tasks.Add(_db.SetPlayerAntagTokenAmount(userId.UserId, AntagTokenCatalog.GhostAutoPendingUsedRoleCreditEntryId,
        state.PendingGhostAutoRoleId != null && state.PendingGhostAutoUsedRoleCredit ? 1 : 0));

    tasks.Add(_db.SetPlayerAntagTokenAmount(userId.UserId, AntagTokenCatalog.GhostAutoPendingUsedDonorDailyFreeEntryId,
        state.PendingGhostAutoRoleId != null && state.PendingGhostAutoUsedDonorDailyFree ? 1 : 0));

    tasks.Add(_db.SetPlayerAntagTokenAmount(userId.UserId, AntagTokenCatalog.GhostAntagConsumedMarkEntryId,
        state.GhostAntagConsumedMark ? 1 : 0));

    if (state.PendingDepositRoleId == null)
        tasks.Add(_db.ClearPlayerAntagTokenSelection(userId.UserId));
    else
        tasks.Add(_db.SetPlayerAntagTokenSelection(userId.UserId, AntagTokenCatalog.DepositSelectionTokenId, state.PendingDepositRoleId));

    await Task.WhenAll(tasks);
}
    private void SaveAll()
    {
        foreach (var (userId, state) in _states)
        {
            PersistState(userId, state);
        }
    }

    private PlayerTokenState? EnsureStateExists(NetUserId userId)
    {
        if (_states.TryGetValue(userId, out var state))
            return state;

        if (!_playerManager.TryGetSessionById(userId, out _))
            return null;

        state = new PlayerTokenState();
        NormalizeMonthlyState(state, DateTime.UtcNow, userId);
        _states[userId] = state;
        var now = DateTime.UtcNow;
        _onlineRewards.TryAdd(userId, new OnlineRewardState());
        _onlineRewards[userId].Resume(now);
        return state;
    }

private void NormalizeMonthlyState(PlayerTokenState state, DateTime nowUtc, NetUserId? userId = null)
{
    if (state.MonthlyYear == nowUtc.Year && state.MonthlyMonth == nowUtc.Month)
        return;

    state.MonthlyYear = nowUtc.Year;
    state.MonthlyMonth = nowUtc.Month;
    state.MonthlyEarned = 0;
}

    private int? GetMonthlyCap(NetUserId userId)
    {
        var sponsorLevel = GetEffectiveSponsorLevel(userId);
        return sponsorLevel > 0 ? null : 100;
    }

    private bool IsRoundstartRoleBlockedByPreset()
    {
        if (_typanWar.IsTypanWarBlocking())
            return true;

        var preset = _gameTicker.RunLevel == GameRunLevel.PreRoundLobby
            ? _gameTicker.Preset
            : _gameTicker.CurrentPreset ?? _gameTicker.Preset;

        return preset != null && BlockedRoundstartRolePresets.Contains(preset.ID);
    }

    private bool IsPresetMissingRequiredGameRule(string requiredRuleId)
    {
        var preset = _gameTicker.RunLevel == GameRunLevel.PreRoundLobby
            ? _gameTicker.Preset
            : _gameTicker.CurrentPreset ?? _gameTicker.Preset;

        if (preset == null)
            return true;

        foreach (var rule in preset.Rules)
        {
            if (rule.Id == requiredRuleId)
                return false;
        }

        return true;
    }

    private bool TryFindAddedGameRule(string ruleId, out EntityUid ruleEntity)
    {
        foreach (var rule in _gameTicker.GetAddedGameRules())
        {
            if (MetaData(rule).EntityPrototype?.ID == ruleId)
            {
                ruleEntity = rule;
                return true;
            }
        }

        ruleEntity = EntityUid.Invalid;
        return false;
    }

    private bool TryResolveGameRuleForTokenRole(AntagRoleDefinition role, out EntityUid ruleEntity)
    {
        ruleEntity = EntityUid.Invalid;

        if (string.IsNullOrEmpty(role.RequiresPresetGameRuleId))
            return false;

        return TryFindAddedGameRule(role.RequiresPresetGameRuleId, out ruleEntity);
    }

    private static void SpendForRole(PlayerTokenState state, AntagRoleDefinition role, bool useRoleCredit, bool useDonorDailyFree,
        bool usePublicRoundFree)
    {
        if (useRoleCredit)
            state.RoleCredits[role.Id] = Math.Max(0, state.RoleCredits.GetValueOrDefault(role.Id) - 1);
        else if (!useDonorDailyFree && !usePublicRoundFree)
            state.Balance -= role.Cost;

        if (useDonorDailyFree)
            state.LastDonorDailyFreeAntagDay = EncodeUtcDayNumber(DateTime.UtcNow);
    }

    private static void RefundRolePurchase(PlayerTokenState state, AntagRoleDefinition role, bool usedRoleCredit, bool usedDonorDailyFree)
    {
        if (usedRoleCredit)
            state.RoleCredits[role.Id] = state.RoleCredits.GetValueOrDefault(role.Id) + 1;
        else if (usedDonorDailyFree)
            state.LastDonorDailyFreeAntagDay = 0;
        else
            state.Balance += role.Cost;
    }

    private void RefundPendingDeposit(NetUserId userId, PlayerTokenState state)
    {
        if (state.PendingDepositRoleId == null)
        {
            state.PendingDepositQueuedAtUtc = null;
            state.PendingDepositUsedRoleCredit = false;
            state.PendingDepositUsedDonorDailyFree = false;
            return;
        }

        if (!_listings.TryGetListing(state.PendingDepositRoleId, out var role))
        {
            state.PendingDepositRoleId = null;
            state.PendingDepositQueuedAtUtc = null;
            state.PendingDepositUsedRoleCredit = false;
            state.PendingDepositUsedDonorDailyFree = false;
            return;
        }

        RefundRolePurchase(state, role, state.PendingDepositUsedRoleCredit, state.PendingDepositUsedDonorDailyFree);
        state.PendingDepositRoleId = null;
        state.PendingDepositQueuedAtUtc = null;
        state.PendingDepositUsedRoleCredit = false;
        state.PendingDepositUsedDonorDailyFree = false;
    }

    private static int EncodeUnixSeconds(DateTime? value)
    {
        if (value == null)
            return 0;

        var unix = new DateTimeOffset(value.Value).ToUnixTimeSeconds();
        return unix <= 0 ? 0 : (int)Math.Min(unix, int.MaxValue);
    }

    private static DateTime? DecodeUnixSeconds(int value)
    {
        if (value <= 0)
            return null;

        return DateTimeOffset.FromUnixTimeSeconds(value).UtcDateTime;
    }

    private void ShowPopup(ICommonSession session, string message)
    {
        if (session.AttachedEntity is { Valid: true } uid)
            _popup.PopupEntity(message, uid, uid);
    }

    private sealed class OnlineRewardState
    {
        private static readonly TimeSpan RewardCycleLength = TimeSpan.FromHours(24);
        public HashSet<TimeSpan> GrantedThresholds { get; private set; } = new();
        private TimeSpan _accumulatedOnline = TimeSpan.Zero;
        private DateTime? _onlineSinceUtc;
        private DateTime _cycleStartUtc = DateTime.MinValue;

        public void EnsureCurrentCycle(DateTime nowUtc)
        {
            if (_cycleStartUtc == DateTime.MinValue)
            {
                _cycleStartUtc = nowUtc;
                return;
            }

            if (nowUtc - _cycleStartUtc < RewardCycleLength)
                return;

            _accumulatedOnline = TimeSpan.Zero;
            GrantedThresholds.Clear();
            _cycleStartUtc = nowUtc;

            if (_onlineSinceUtc != null)
                _onlineSinceUtc = nowUtc;
        }

        public void Resume(DateTime nowUtc)
        {
            EnsureCurrentCycle(nowUtc);
            if (_onlineSinceUtc != null)
                return;

            _onlineSinceUtc = nowUtc;
        }

        public void Pause(DateTime nowUtc)
        {
            EnsureCurrentCycle(nowUtc);
            if (_onlineSinceUtc == null)
                return;

            var delta = nowUtc - _onlineSinceUtc.Value;
            if (delta > TimeSpan.Zero)
                _accumulatedOnline += delta;

            _onlineSinceUtc = null;
        }

        public TimeSpan GetElapsed(DateTime nowUtc)
        {
            if (_onlineSinceUtc == null)
                return _accumulatedOnline;

            var delta = nowUtc - _onlineSinceUtc.Value;
            if (delta <= TimeSpan.Zero)
                return _accumulatedOnline;

            return _accumulatedOnline + delta;
        }
    }

    public sealed class PlayerTokenState
    {
        public int Balance { get; set; }
        public int MonthlyEarned { get; set; }
        public int MonthlyYear { get; set; }
        public int MonthlyMonth { get; set; }
        public DateTime? LastDonorBonusClaimUtc { get; set; }
        public string? PendingDepositRoleId { get; set; }
        public DateTime? PendingDepositQueuedAtUtc { get; set; }
        public bool PendingDepositUsedRoleCredit { get; set; }
        public bool PendingDepositUsedDonorDailyFree { get; set; }
        public string? PendingGhostAutoRoleId { get; set; }
        public DateTime? PendingGhostAutoQueuedAtUtc { get; set; }
        public bool PendingGhostAutoUsedRoleCredit { get; set; }
        public bool PendingGhostAutoUsedDonorDailyFree { get; set; }

        public int LastDonorDailyFreeAntagDay { get; set; }

        /// <summary>
        /// Persisted: ghost-rule token role was taken this station round (cleared every round cleanup).
        /// </summary>
        public bool GhostAntagConsumedMark { get; set; }

        public Dictionary<string, int> RoleCredits { get; private set; } = new();

        public HashSet<ProtoId<JobPrototype>> JobUnlocks { get; private set; } = new();

        public HashSet<ProtoId<AntagPrototype>> AntagUnlocks { get; private set; } = new();
    }

    private void OnGhostRoleTakenForToken(EntityUid uid, GhostRoleComponent _, ref TakeGhostRoleEvent args)
    {
        if (!args.TookRole)
            return;

        var session = args.Player;
        if (!_states.TryGetValue(session.UserId, out var state))
            return;

        if (state.PendingGhostAutoRoleId == null)
            return;

        if (!_listings.TryGetListing(state.PendingGhostAutoRoleId, out var listing) ||
            listing.Mode != AntagPurchaseMode.GhostRule ||
            string.IsNullOrEmpty(listing.GhostAutoJoinEntityProto))
        {
            return;
        }

        if (!IsGhostAutoJoinPrototypeMatch(MetaData(uid).EntityPrototype?.ID, listing.GhostAutoJoinEntityProto))
            return;

        var grantedRoleId = listing.Id;
        ClearPendingGhostAuto(state);
        MarkGhostRuleTokenGranted(session.UserId, grantedRoleId);
        PersistState(session.UserId, state);
        SendState(session.UserId);
    }

    private void OnGhostRoleRegistered(Entity<GhostRoleComponent> ent, ref GhostRoleRegisteredEvent args)
    {
        if (MetaData(ent).EntityPrototype?.ID is not { } protoId)
            return;

        var uid = ent.Owner;
        Robust.Shared.Timing.Timer.Spawn(0, () =>
        {
            if (Deleted(uid) || Terminating(uid))
                return;

            if (!TryComp<GhostRoleComponent>(uid, out var ghostRole))
                return;

            ProcessGhostAutoJoinForProto(uid, ghostRole, protoId);
        });
    }

    private void ProcessGhostAutoJoinForProto(EntityUid uid, GhostRoleComponent ghostRole, string protoId)
    {
        var candidates = new List<ICommonSession>();
        foreach (var session in _playerManager.Sessions)
        {
            if (!_states.TryGetValue(session.UserId, out var state))
                continue;

            if (state.PendingGhostAutoRoleId == null)
                continue;

            if (!_listings.TryGetListing(state.PendingGhostAutoRoleId, out var roleDef))
                continue;

            if (roleDef.GhostAutoJoinEntityProto != protoId)
                continue;

            if (!IsSessionEligibleForGhostRolePurchase(session))
                continue;

            candidates.Add(session);
        }

        candidates.Sort((a, b) =>
        {
            var ta = _states.TryGetValue(a.UserId, out var sa) ? sa.PendingGhostAutoQueuedAtUtc : null;
            var tb = _states.TryGetValue(b.UserId, out var sb) ? sb.PendingGhostAutoQueuedAtUtc : null;
            return (ta ?? DateTime.MinValue).CompareTo(tb ?? DateTime.MinValue);
        });

        foreach (var session in candidates)
        {
            if (!_states.TryGetValue(session.UserId, out var state))
                continue;

            if (state.PendingGhostAutoRoleId == null)
                continue;

            if (!_listings.TryGetListing(state.PendingGhostAutoRoleId, out var roleDef))
                continue;

            if (roleDef.GhostAutoJoinEntityProto != protoId)
                continue;

            TryGhostAutoJoin(session, state, ghostRole);
        }
    }

    private void TryGhostAutoJoin(ICommonSession session, PlayerTokenState state, GhostRoleComponent ghostRole)
    {
        if (ghostRole.Taken)
            return;

        Logger.InfoS("AntagTokens",
            $"Ghost auto-join attempt: user={session.Name} protoRole={state.PendingGhostAutoRoleId} ghostId={ghostRole.Identifier} raffle={(ghostRole.RaffleConfig != null)}");

        if (_ghostRoles.Takeover(session, ghostRole.Identifier))
        {
            var grantedRoleId = state.PendingGhostAutoRoleId!;
            ClearPendingGhostAuto(state);
            MarkGhostRuleTokenGranted(session.UserId, grantedRoleId);
            PersistState(session.UserId, state);
            SendState(session.UserId);
            Logger.InfoS("AntagTokens", $"Ghost auto-join instant takeover ok: user={session.Name}");
            return;
        }

        Logger.WarningS("AntagTokens", $"Ghost auto-join instant takeover failed: user={session.Name}");
        RefundPendingGhostAuto(session.UserId, state);
        PersistState(session.UserId, state);
        SendState(session.UserId);
    }

    private static void ClearPendingGhostAuto(PlayerTokenState state)
    {
        state.PendingGhostAutoRoleId = null;
        state.PendingGhostAutoQueuedAtUtc = null;
        state.PendingGhostAutoUsedRoleCredit = false;
        state.PendingGhostAutoUsedDonorDailyFree = false;
    }

    private bool IsGhostAutoJoinPrototypeMatch(string? actualProtoId, string? expectedProtoId)
    {
        if (string.IsNullOrWhiteSpace(actualProtoId) || string.IsNullOrWhiteSpace(expectedProtoId))
            return false;

        if (actualProtoId == expectedProtoId)
            return true;

        if (_prototype.HasIndex<EntityPrototype>(actualProtoId))
        {
            foreach (var parent in _prototype.EnumerateParents<EntityPrototype>(actualProtoId))
            {
                if (parent.ID == expectedProtoId)
                    return true;
            }
        }

        if (_prototype.HasIndex<EntityPrototype>(expectedProtoId))
        {
            foreach (var parent in _prototype.EnumerateParents<EntityPrototype>(expectedProtoId))
            {
                if (parent.ID == actualProtoId)
                    return true;
            }
        }

        return false;
    }

    private bool TryResolveGhostAutoPendingBeforePurchase(NetUserId userId, PlayerTokenState state)
    {
        if (state.PendingGhostAutoRoleId == null)
            return true;

        if (!_listings.TryGetListing(state.PendingGhostAutoRoleId, out var pendingRole))
        {
            ClearPendingGhostAuto(state);
            return true;
        }

        var proto = pendingRole.GhostAutoJoinEntityProto;
        if (string.IsNullOrWhiteSpace(proto))
        {
            RefundPendingGhostAuto(userId, state);
            return true;
        }

        if (_ghostRoles.HasAvailableGhostRoleForEntityProto(proto))
            return false;

        Logger.InfoS("AntagTokens", $"Ghost auto-pending stale: no slot for proto {proto}, refund user {userId}");
        RefundPendingGhostAuto(userId, state);
        return true;
    }

    private void RefundPendingGhostAuto(NetUserId userId, PlayerTokenState state)
    {
        if (state.PendingGhostAutoRoleId == null)
        {
            state.PendingGhostAutoUsedRoleCredit = false;
            state.PendingGhostAutoUsedDonorDailyFree = false;
            return;
        }

        var pendingId = state.PendingGhostAutoRoleId;

        if (!_listings.TryGetListing(pendingId, out var role))
        {
            ClearPendingGhostAuto(state);
            if (_globallyClaimedGhostRoles.Remove(pendingId))
                BroadcastAntagTokenUiRefresh();
            return;
        }

        RefundRolePurchase(state, role, state.PendingGhostAutoUsedRoleCredit, state.PendingGhostAutoUsedDonorDailyFree);
        ClearPendingGhostAuto(state);
        if (_globallyClaimedGhostRoles.Remove(pendingId))
            BroadcastAntagTokenUiRefresh();
    }

    private void BroadcastAntagTokenUiRefresh()
    {
        foreach (var session in _playerManager.Sessions)
        {
            if (_states.ContainsKey(session.UserId))
                SendState(session.UserId);
        }
    }
}
