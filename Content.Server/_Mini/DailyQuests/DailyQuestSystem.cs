using Content.Shared.Roles.Components;
// SPDX-FileCopyrightText: 2026 Casha
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Goobstation.Shared.FloorCleaner;
using Content.Goobstation.Shared.Harvestable;
using Content.Server._CorvaxGoob.Document;
using Content.Server.Cargo.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Database;
using Content.Server.Ghost;
using Content.Server.Mind;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Popups;
using Content.Server.Preferences.Managers;
using Content.Server.Research.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Server._Mini.AntagTokens;
using Content.Server._Mini.DailyRewards;
using Content.Shared._DV.Salvage;
using Content.Shared._DV.Salvage.Components;
using Content.Shared._DV.Salvage.Systems;
using Content.Shared._Mini.DailyQuests;
using Content.Shared._Mini.DailyRewards;
using Content.Shared.Cargo.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems.Hypospray;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Forensics;
using Content.Shared.GameTicking;
using Content.Shared.Kitchen.Components;
using Content.Shared.Lathe;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Paper;
using Content.Shared.Players;
using Content.Shared.Research.Components;
using Content.Shared.Repairable;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Salvage.Magnet;
using Content.Shared.Speech.Components;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Mini.DailyQuests;

public sealed class DailyQuestSystem : EntitySystem
{
    private const int DailyQuestCount = 2;
    private const float UiRefreshInterval = 1f;

    private float _uiRefreshAccumulator;

    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly UserDbDataManager _userDb = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly MindSystem _minds = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MiningPointsSystem _miningPoints = default!;
    [Dependency] private readonly AntagTokenSystem _antagTokens = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly PlayTimeTrackingSystem _playTime = default!;
    [Dependency] private readonly IServerPreferencesManager _preferences = default!;
    [Dependency] private readonly StationSystem _station = default!;

    private readonly Dictionary<NetUserId, PlayerDailyQuestState> _states = new();
    /// <summary>
    /// Round-scoped quest state kept across launcher reconnects within the same round.
    /// </summary>
    private readonly Dictionary<NetUserId, DailyQuestRoundTracker> _roundTrackers = new();

    private EntityQuery<MobStateComponent> _mobQuery;

    public override void Initialize()
    {
        base.Initialize();

        _mobQuery = GetEntityQuery<MobStateComponent>();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEnd);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeLocalEvent<MedicalHealAppliedEvent>(OnMedicalHealApplied);
        SubscribeLocalEvent<PaperComponent, PaperStampedEvent>(OnPaperStamped);
        SubscribeLocalEvent<HyposprayPatientInjectedEvent>(OnHyposprayPatientInjected);
        SubscribeLocalEvent<PlayerCuffedEvent>(OnPlayerCuffed);
        SubscribeLocalEvent<SalvageMagnetClaimedEvent>(OnSalvageMagnetClaimed);
        SubscribeLocalEvent<ForensicScanCompletedEvent>(OnForensicScanCompleted);
        SubscribeLocalEvent<PlantHarvestedEvent>(OnPlantHarvested);
        SubscribeLocalEvent<FloorCleanedEvent>(OnFloorCleaned);
        SubscribeLocalEvent<TechnologyUnlockedEvent>(OnTechnologyUnlocked);
        SubscribeLocalEvent<MicrowaveMealProducedEvent>(OnMicrowaveMeal);
        SubscribeLocalEvent<LatheItemProducedEvent>(OnLatheItemProduced);
        SubscribeLocalEvent<RepairableComponent, RepairedEvent>(OnRepaired);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<DamageableComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<EmotePerformedEvent>(OnEmotePerformed);
        SubscribeLocalEvent<CargoBountyLabelPrintedEvent>(OnCargoBountyLabelPrinted);
        SubscribeLocalEvent<CargoBountyFulfilledEvent>(OnCargoBountyFulfilled);
        SubscribeLocalEvent<StructureWeldedEvent>(OnStructureWelded);
        SubscribeLocalEvent<MiningPointsClaimedEvent>(OnMiningPointsClaimed);
        SubscribeLocalEvent<BankBalanceUpdatedEvent>(OnBankBalanceUpdated);

        _userDb.AddOnLoadPlayer(LoadPlayerData);
        _userDb.AddOnFinishLoad(OnPlayerDataLoaded);
        _userDb.AddOnPlayerDisconnect(OnPlayerDisconnect);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _uiRefreshAccumulator += frameTime;
        if (_uiRefreshAccumulator < UiRefreshInterval)
            return;

        _uiRefreshAccumulator = 0f;

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status == SessionStatus.Disconnected)
                continue;

            if (!_states.TryGetValue(session.UserId, out var state) || !state.LoadedFromDb)
                continue;

            if (TryRefreshDailyQuestsIfNeeded(session, state))
            {
                Persist(session.UserId);
                EntityManager.System<DailyRewardSystem>().RefreshUi(session.UserId);
                continue;
            }

            if (!state.Round.WasActivePlayer)
                continue;

            FlushPlaytime(state);
            var changed = UpdateLiveQuestProgress(session, state);
            if (changed)
                EntityManager.System<DailyRewardSystem>().RefreshUi(session.UserId);
        }
    }

    /// <summary>
    /// Rolls weekly quests forward when the Moscow calendar week changes. Returns true if assignments were refreshed.
    /// </summary>
    private bool TryRefreshDailyQuestsIfNeeded(ICommonSession session, PlayerDailyQuestState state)
    {
        if (!ArePreferencesReady(session))
            return false;

        var today = DailyQuestWeek.GetCurrentWeekStart();
        if (DailyQuestWeek.Matches(state.QuestDate, today))
            return false;

        var previousDate = state.QuestDate;
        EnsureDailyAssignments(session, state);
        Log.Info($"Weekly quests refreshed for {session.Name}: {previousDate:yyyy-MM-dd} -> {today:yyyy-MM-dd}");
        return true;
    }

    public List<DailyQuestEntry> BuildQuestEntries(ICommonSession session)
    {
        var state = EnsureState(session.UserId);
        if (state.LoadedFromDb && ArePreferencesReady(session))
        {
            var questDateBefore = state.QuestDate;
            EnsureDailyAssignmentsIfNeeded(session, state);

            if (state.QuestDate != questDateBefore)
                Persist(session.UserId, refreshUi: false);
        }

        var entries = new List<DailyQuestEntry>(state.Slots.Count);

        foreach (var slot in state.Slots)
        {
            if (!_prototypes.TryIndex(slot.QuestId, out DailyQuestPrototype? proto))
                continue;

            entries.Add(BuildQuestEntry(session, state, slot, proto));
        }

        return entries;
    }

    private DailyQuestEntry BuildQuestEntry(
        ICommonSession session,
        PlayerDailyQuestState state,
        DailyQuestSlot slot,
        DailyQuestPrototype proto,
        bool preview = false)
    {
        var roleHint = proto.RequiredJob == null
            ? null
            : _prototypes.TryIndex(proto.RequiredJob.Value, out var job)
                ? job.LocalizedName
                : null;

        ResolveQuestIcon(proto, out var iconId, out var iconSprite, out var iconState);
        var isTimeBased = IsTimeBasedQuest(proto);
        var (currentProgress, targetProgress) = preview
            ? (0, IsTimeBasedQuest(proto)
                ? Math.Max(1, (int)proto.MinRoundPlaytime.TotalSeconds)
                : proto.TargetCount)
            : GetQuestProgressDisplay(state, slot, proto);

        var canReplace = !preview
            && slot.Status == DailyQuestStatus.Active
            && !state.DailyReplaceUsed
            && CanReplaceSlot(state, slot, proto);

        var description = isTimeBased
            ? Loc.GetString(proto.Description, ("minutes", (int)proto.MinRoundPlaytime.TotalMinutes))
            : Loc.GetString(proto.Description, ("count", proto.TargetCount));

        DateTime? nextQuestResetUtc = null;
        if (!preview && slot.Status >= DailyQuestStatus.Completed)
            nextQuestResetUtc = DailyQuestWeek.GetNextResetUtc(state.QuestDate);

        return new DailyQuestEntry(
            slot.QuestId,
            Loc.GetString(proto.Name),
            description,
            currentProgress,
            targetProgress,
            proto.GetRewardCoins(),
            preview ? false : slot.Status >= DailyQuestStatus.Completed,
            preview ? false : slot.Status == DailyQuestStatus.Claimed,
            roleHint,
            iconId,
            iconSprite,
            iconState,
            isTimeBased,
            canReplace,
            proto.Rarity,
            nextQuestResetUtc);
    }

    public void TryReplaceQuest(ICommonSession session, string questId, int slotIndex = -1)
    {
        if (string.IsNullOrWhiteSpace(questId))
        {
            DenyQuestReplace(session, questId, Loc.GetString("daily-quest-replace-denied"), "empty-quest-id");
            return;
        }

        var state = EnsureState(session.UserId);
        if (!state.LoadedFromDb)
        {
            DenyQuestReplace(session, questId, Loc.GetString("daily-quest-replace-denied-loading"), "not-loaded-from-db");
            return;
        }

        if (slotIndex < 0 || slotIndex >= state.Slots.Count
            || state.Slots[slotIndex].QuestId != questId)
        {
            slotIndex = state.Slots.FindIndex(s => s.QuestId == questId);
        }

        if (slotIndex < 0)
        {
            var assigned = string.Join(", ", state.Slots.Select(s => s.QuestId));
            DenyQuestReplace(session, questId, Loc.GetString("daily-quest-replace-denied"),
                $"slot-not-found (requested={questId}, assigned=[{assigned}])");
            return;
        }

        var slot = state.Slots[slotIndex];

        if (!_prototypes.TryIndex(slot.QuestId, out DailyQuestPrototype? proto))
        {
            DenyQuestReplace(session, questId, Loc.GetString("daily-quest-replace-denied"),
                $"prototype-missing ({slot.QuestId})");
            return;
        }

        if (slot.Status != DailyQuestStatus.Active)
        {
            DenyQuestReplace(session, questId, Loc.GetString("daily-quest-replace-denied"),
                $"status={slot.Status}");
            return;
        }

        if (state.DailyReplaceUsed)
        {
            DenyQuestReplace(session, questId, Loc.GetString("daily-quest-replace-denied-used"), "daily-replace-used");
            return;
        }

        if (!CanReplaceSlot(state, slot, proto))
        {
            var detail = IsTimeBasedQuest(proto)
                ? $"active-playtime={state.Round.ActivePlaytime.TotalSeconds:F0}s"
                : $"progress={slot.Progress}/{proto.TargetCount}";
            DenyQuestReplace(session, questId, Loc.GetString("daily-quest-replace-denied-progress"), detail);
            return;
        }

        var exclude = state.Slots.Select(s => s.QuestId).ToHashSet();
        var pick = PickRandomQuestForReplace(session, state.QuestDate, exclude);
        if (pick == null)
        {
            DenyQuestReplace(session, questId, Loc.GetString("daily-quest-replace-empty-pool"), "empty-pool");
            return;
        }

        slot.QuestId = pick;
        slot.Progress = 0;
        state.DailyReplaceUsed = true;
        state.Round.UniqueProgress.Remove(questId);
        DeduplicateSlots(state);

        Persist(session.UserId, refreshUi: true);

        Log.Info($"Daily quest replaced for {session.Name}: {questId} -> {pick} (slot {slotIndex})");

        if (session.AttachedEntity is { Valid: true } uid)
            _popup.PopupEntity(Loc.GetString("daily-quest-replace-success"), uid, session);
    }

    private void DenyQuestReplace(ICommonSession session, string questId, string reason, string detail)
    {
        Log.Info($"Daily quest replace denied for {session.Name}: quest={questId}, reason={detail}");

        RaiseNetworkEvent(new DailyQuestReplaceDeniedEvent(questId, reason), session);

        EntityManager.System<DailyRewardSystem>().RefreshUi(session.UserId);

        if (session.AttachedEntity is { Valid: true } uid)
            _popup.PopupEntity(reason, uid, session);
    }

    private static bool CanReplaceSlot(
        PlayerDailyQuestState state,
        DailyQuestSlot slot,
        DailyQuestPrototype proto)
    {
        if (slot.Status != DailyQuestStatus.Active)
            return false;

        if (IsTimeBasedQuest(proto))
            return state.Round.ActivePlaytime <= TimeSpan.Zero && !state.Round.WasActivePlayer;

        return slot.Progress <= 0;
    }

    private async Task LoadPlayerData(ICommonSession player, CancellationToken cancel)
    {
        var weekStart = DailyQuestWeek.GetCurrentWeekStart();
        DailyQuestProgress? progress = null;

        try
        {
            progress = await _db.GetDailyQuestProgress(
                player.UserId.UserId,
                DailyQuestWeek.ToStoredKey(weekStart),
                cancel);

            if (progress == null)
            {
                var history = await _db.GetRecentDailyQuestProgress(player.UserId.UserId, 8, cancel);
                progress = history.FirstOrDefault(p => DailyQuestWeek.Matches(p.QuestDate, weekStart));
            }
        }
        catch (Exception e)
        {
            Log.Error($"Failed to load daily quest progress for {player}: {e}");
        }

        if (_states.TryGetValue(player.UserId, out var existing) && existing.LoadedFromDb)
        {
            RestoreRoundTracker(player.UserId, existing);
            return;
        }

        _states[player.UserId] = PlayerDailyQuestState.FromDb(progress, player.UserId.UserId, weekStart);
        DeduplicateSlots(_states[player.UserId]);
        RestoreRoundTracker(player.UserId, _states[player.UserId]);
        _states[player.UserId].LoadedFromDb = true;
    }

    private void OnPlayerDataLoaded(ICommonSession player)
    {
        if (!_states.TryGetValue(player.UserId, out var state) || !state.LoadedFromDb)
            return;

        RestoreRoundTracker(player.UserId, state);
        if (ArePreferencesReady(player))
            EnsureDailyAssignmentsIfNeeded(player, state);

        Persist(player.UserId);
        EntityManager.System<DailyRewardSystem>().RefreshUi(player.UserId);
    }

    private void OnPlayerDisconnect(ICommonSession player)
    {
        if (_states.TryGetValue(player.UserId, out var state))
        {
            FlushPlaytime(state);
            state.Round.TrackingSince = null;
            state.Round.ActiveEntity = null;
            _roundTrackers[player.UserId] = CloneRoundTracker(state.Round, persistEntityReference: false);
            Persist(player.UserId);
            // Keep in-memory state across reconnect so progress isn't lost if DB upsert is still in-flight.
        }
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.Player.AttachedEntity is not { Valid: true } uid)
            return;

        if (!_states.TryGetValue(ev.Player.UserId, out var state) || !state.LoadedFromDb)
            return;

        if (ev.Player.GetMind() is { } mindId && _roles.MindHasRole<ObserverRoleComponent>(mindId))
            return;

        state.Round.WasActivePlayer = true;
        state.Round.ActiveEntity = uid;
        state.Round.TrackingSince ??= _timing.CurTime;

        if (state.Round.MiningPointsBaseline == 0)
        {
            var points = _miningPoints.GetPointComp(uid);
            if (points?.Comp != null)
                state.Round.MiningPointsBaseline = points.Value.Comp.Points;
        }
    }

    private void OnRoundEnd(RoundEndMessageEvent ev)
    {
        foreach (var session in _playerManager.Sessions)
        {
            if (!_states.TryGetValue(session.UserId, out var state))
                continue;

            FlushPlaytime(state);
            EvaluatePassiveQuests(session, state);
        }

        foreach (var userId in _states.Keys.ToArray())
            Persist(userId);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent _)
    {
        _roundTrackers.Clear();

        foreach (var state in _states.Values)
            state.Round.Reset();
    }

    private void CompleteSlot(ICommonSession session, DailyQuestSlot slot, DailyQuestPrototype proto)
    {
        if (slot.Status != DailyQuestStatus.Active)
            return;

        slot.Progress = proto.TargetCount;
        _antagTokens.AddBalance(session.UserId, proto.GetRewardCoins(), out var granted, out var note);
        slot.Status = DailyQuestStatus.Claimed;
        Persist(session.UserId);
        EntityManager.System<DailyRewardSystem>().RefreshUi(session.UserId);

        if (session.AttachedEntity is not { Valid: true } uid)
            return;

        var message = granted > 0
            ? Loc.GetString("daily-quest-claim-success", ("amount", granted))
            : Loc.GetString("daily-quest-claim-empty");

        if (!string.IsNullOrWhiteSpace(note))
            message = $"{message} {note}";

        _popup.PopupEntity(message, uid, uid);
    }

    #region Tracking handlers

    private void ResolveQuestIcon(
        DailyQuestPrototype proto,
        out string? iconId,
        out string? iconSprite,
        out string? iconState)
    {
        iconId = null;
        iconSprite = null;
        iconState = null;

        if (proto.Icon != null)
        {
            iconId = proto.Icon.Value.ToString();
            return;
        }

        if (proto.Sprite is { } sprite)
        {
            switch (sprite)
            {
                case SpriteSpecifier.Rsi rsi:
                    iconSprite = rsi.RsiPath.ToString();
                    iconState = rsi.RsiState;
                    return;
                case SpriteSpecifier.Texture tex:
                    iconSprite = tex.TexturePath.ToString();
                    return;
            }
        }

        if (proto.QuestType == DailyQuestType.HealOthers)
        {
            iconSprite = "/Textures/Interface/Misc/health_icons.rsi";
            iconState = "Fine";
            return;
        }

        if (proto.RequiredJob != null && _prototypes.TryIndex(proto.RequiredJob.Value, out JobPrototype? job))
        {
            iconId = job.Icon.ToString();
            return;
        }

        if (proto.RequiredJob == null)
        {
            iconId = "JobIconNoId";
            return;
        }

        iconId = proto.QuestType switch
        {
            DailyQuestType.NoMeleeHits => "JobIconMime",
            DailyQuestType.NoDamageTaken => "JobIconMedicalDoctor",
            DailyQuestType.SurviveRound => "JobIconCaptain",
            DailyQuestType.PerformEmotes => "JobIconMime",
            DailyQuestType.StampDocuments => "JobIconHeadOfPersonnel",
            DailyQuestType.EarnMiningPoints => "JobIconQuarterMaster",
            DailyQuestType.CuffPlayers => "JobIconSecurityOfficer",
            DailyQuestType.UnlockTechnology => "JobIconScientist",
            DailyQuestType.CookMeals => "JobIconChef",
            DailyQuestType.HarvestPlants => "JobIconBotanist",
            DailyQuestType.WeldStructures => "JobIconAtmosphericTechnician",
            DailyQuestType.InjectPatients => "JobIconChemist",
            DailyQuestType.FulfillCargoBounty => "JobIconQuarterMaster",
            DailyQuestType.CleanDecals => "JobIconJanitor",
            DailyQuestType.ScanForensics => "JobIconDetective",
            DailyQuestType.PullSalvageMagnet => "JobIconQuarterMaster",
            DailyQuestType.LatheProduce => "JobIconScientist",
            DailyQuestType.KillHostiles => "JobIconSecurityOfficer",
            DailyQuestType.RepairStructures => "JobIconStationEngineer",
            DailyQuestType.EarnStationBankBalance => "JobIconQuarterMaster",
            _ => "JobIconUnknown",
        };
    }

    private void OnMedicalHealApplied(ref MedicalHealAppliedEvent args)
    {
        if (args.User == args.Target)
            return;

        if (!IsHumanPlayer(args.Target))
            return;

        Guid? uniquePlayerId = null;
        if (_minds.TryGetMind(args.Target, out _, out var mind) && mind.UserId != null)
            uniquePlayerId = mind.UserId.Value.UserId;

        TryIncrement(args.User, DailyQuestType.HealOthers, 1, uniquePlayerId);
    }

    private void OnPaperStamped(Entity<PaperComponent> ent, ref PaperStampedEvent args)
    {
        if (args.User == EntityUid.Invalid)
            return;

        TryIncrement(args.User, DailyQuestType.StampDocuments);
    }

    private void OnHyposprayPatientInjected(ref HyposprayPatientInjectedEvent args)
    {
        if (args.User == args.Target)
            return;

        if (!IsHumanPlayer(args.Target))
            return;

        TryIncrement(args.User, DailyQuestType.InjectPatients);
    }

    private void OnPlayerCuffed(ref PlayerCuffedEvent args)
    {
        TryIncrement(args.User, DailyQuestType.CuffPlayers);
    }

    private void OnSalvageMagnetClaimed(ref SalvageMagnetClaimedEvent args)
    {
        TryIncrement(args.Actor, DailyQuestType.PullSalvageMagnet);
    }

    private void OnForensicScanCompleted(ref ForensicScanCompletedEvent args)
    {
        TryIncrement(args.User, DailyQuestType.ScanForensics);
    }

    private void OnPlantHarvested(ref PlantHarvestedEvent args)
    {
        TryIncrement(args.User, DailyQuestType.HarvestPlants);
    }

    private void OnFloorCleaned(ref FloorCleanedEvent args)
    {
        if (args.Count > 0)
            TryIncrement(args.User, DailyQuestType.CleanDecals, args.Count);
    }

    private void OnTechnologyUnlocked(ref TechnologyUnlockedEvent args)
    {
        TryIncrement(args.Actor, DailyQuestType.UnlockTechnology);
    }

    private void OnMicrowaveMeal(ref MicrowaveMealProducedEvent args)
    {
        if (args.User is { } user)
            TryIncrement(user, DailyQuestType.CookMeals);
    }

    private void OnLatheItemProduced(ref LatheItemProducedEvent args)
    {
        if (args.User is { } user)
            TryIncrement(user, DailyQuestType.LatheProduce);
    }

    private void OnRepaired(Entity<RepairableComponent> ent, ref RepairedEvent args)
    {
        TryIncrement(args.User, DailyQuestType.RepairStructures);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (IsHumanPlayer(args.Target))
            return;

        if (args.Origin == null || !TryGetSession(args.Origin.Value, out _))
            return;

        TryIncrement(args.Origin.Value, DailyQuestType.KillHostiles);
    }

    private void OnDamageChanged(EntityUid uid, DamageableComponent component, DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        if (TryGetPlayerState(uid, out var victimState) && victimState.Round.WasActivePlayer)
            victimState.Round.FailedNoDamage = true;

        // NoMeleeHits is failed only via OnMeleeHit — ranged / other damage must not fail pacifist.
    }

    private void OnMeleeHit(MeleeHitEvent args)
    {
        if (!TryGetPlayerState(args.User, out var state))
            return;

        foreach (var hit in args.HitEntities)
        {
            if (!IsHumanPlayer(hit))
                continue;

            if (hit == args.User)
                continue;

            state.Round.FailedNoMelee = true;
            return;
        }
    }

    private void OnEmotePerformed(ref EmotePerformedEvent args)
    {
        TryIncrement(args.User, DailyQuestType.PerformEmotes);
    }

    private void OnCargoBountyLabelPrinted(ref CargoBountyLabelPrintedEvent args)
    {
        // Keep print as progress so cargo quests stay completable without waiting for a sale.
        TryIncrement(args.Actor, DailyQuestType.FulfillCargoBounty);
    }

    private void OnCargoBountyFulfilled(ref CargoBountyFulfilledEvent args)
    {
        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status == SessionStatus.Disconnected)
                continue;

            if (session.AttachedEntity is not { Valid: true } ent)
                continue;

            if (_station.GetOwningStation(ent) != args.Station)
                continue;

            TryIncrement(session, DailyQuestType.FulfillCargoBounty);
        }
    }

    private void OnBankBalanceUpdated(ref BankBalanceUpdatedEvent ev)
    {
        var totalBalance = 0;
        foreach (var balance in ev.Balance.Values)
            totalBalance += balance;

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status == SessionStatus.Disconnected)
                continue;

            if (!_states.TryGetValue(session.UserId, out var state))
                continue;

            foreach (var slot in state.Slots)
            {
                if (slot.Status != DailyQuestStatus.Active)
                    continue;

                if (!_prototypes.TryIndex(slot.QuestId, out DailyQuestPrototype? proto))
                    continue;

                if (proto.QuestType != DailyQuestType.EarnStationBankBalance)
                    continue;

                if (!JobMatches(session, proto.RequiredJob))
                    continue;

                slot.Progress = Math.Min(totalBalance, proto.TargetCount);
                if (slot.Progress >= proto.TargetCount)
                    CompleteSlot(session, slot, proto);

                Persist(session.UserId);
            }
        }
    }

    private void OnMiningPointsClaimed(ref MiningPointsClaimedEvent args)
    {
        if (!TryGetSession(args.Actor, out var session) ||
            !_states.TryGetValue(session.UserId, out var state) ||
            TryGetQuestTrackingEntity(session, state) is not { } uid)
            return;

        var points = _miningPoints.GetPointComp(uid);
        if (points?.Comp == null)
            return;

        var earned = points.Value.Comp.Points >= state.Round.MiningPointsBaseline
            ? points.Value.Comp.Points - state.Round.MiningPointsBaseline
            : 0;

        foreach (var slot in state.Slots)
        {
            if (slot.Status != DailyQuestStatus.Active)
                continue;

            if (!_prototypes.TryIndex(slot.QuestId, out DailyQuestPrototype? proto))
                continue;

            if (proto.QuestType != DailyQuestType.EarnMiningPoints)
                continue;

            if (!JobMatches(session, proto.RequiredJob))
                continue;

            slot.Progress = (int)Math.Min(earned, proto.TargetCount);
            if (slot.Progress >= proto.TargetCount)
                CompleteSlot(session, slot, proto);

            Persist(session.UserId);
            return;
        }
    }

    private void OnStructureWelded(ref StructureWeldedEvent args)
    {
        TryIncrement(args.User, DailyQuestType.WeldStructures);
    }

    #endregion

    private void EvaluatePassiveQuests(ICommonSession session, PlayerDailyQuestState state)
    {
        FlushPlaytime(state);

        foreach (var slot in state.Slots)
        {
            if (slot.Status != DailyQuestStatus.Active)
                continue;

            if (!_prototypes.TryIndex(slot.QuestId, out DailyQuestPrototype? proto))
                continue;

            if (!JobMatches(session, proto.RequiredJob))
                continue;

            if (state.Round.ActivePlaytime < proto.MinRoundPlaytime)
                continue;

            switch (proto.QuestType)
            {
                case DailyQuestType.NoMeleeHits:
                    if (!state.Round.FailedNoMelee && state.Round.WasActivePlayer)
                        CompleteSlot(session, slot, proto);
                    break;
                case DailyQuestType.NoDamageTaken:
                    if (!state.Round.FailedNoDamage && state.Round.WasActivePlayer)
                        CompleteSlot(session, slot, proto);
                    break;
                case DailyQuestType.SurviveRound:
                    if (state.Round.WasActivePlayer && state.Round.ActivePlaytime >= proto.MinRoundPlaytime)
                        CompleteSlot(session, slot, proto);
                    break;
                case DailyQuestType.EarnMiningPoints:
                    UpdateMiningProgress(session, state, slot, proto);
                    break;
                case DailyQuestType.EarnStationBankBalance:
                    UpdateStationBankProgress(session, state, slot, proto);
                    break;
            }
        }
    }

    private void UpdateMiningProgress(ICommonSession session, PlayerDailyQuestState state, DailyQuestSlot slot, DailyQuestPrototype proto)
    {
        if (TryGetQuestTrackingEntity(session, state) is not { } uid)
            return;

        var points = _miningPoints.GetPointComp(uid);
        if (points?.Comp == null)
            return;

        var earned = points.Value.Comp.Points >= state.Round.MiningPointsBaseline
            ? points.Value.Comp.Points - state.Round.MiningPointsBaseline
            : 0;

        slot.Progress = (int)Math.Min(earned, proto.TargetCount);
        if (slot.Progress >= proto.TargetCount)
            CompleteSlot(session, slot, proto);
    }

    private void UpdateStationBankProgress(ICommonSession session, PlayerDailyQuestState state, DailyQuestSlot slot, DailyQuestPrototype proto)
    {
        if (TryGetQuestTrackingEntity(session, state) is not { } uid)
            return;

        var station = _station.GetOwningStation(uid);
        if (station is not { } stationId || !TryComp<StationBankAccountComponent>(stationId, out var bank))
            return;

        var totalBalance = 0;
        foreach (var balance in bank.Accounts.Values)
            totalBalance += balance;

        slot.Progress = Math.Min(totalBalance, proto.TargetCount);
        if (slot.Progress >= proto.TargetCount)
            CompleteSlot(session, slot, proto);
    }

    private void TryIncrement(EntityUid user, DailyQuestType type, int amount = 1, Guid? uniquePlayerId = null)
    {
        if (!TryGetSession(user, out var session))
            return;

        TryIncrement(session, type, amount, uniquePlayerId);
    }

    private void TryIncrement(ICommonSession session, DailyQuestType type, int amount = 1, Guid? uniquePlayerId = null)
    {
        var state = EnsureState(session.UserId);

        foreach (var slot in state.Slots)
        {
            if (slot.Status != DailyQuestStatus.Active)
                continue;

            if (!_prototypes.TryIndex(slot.QuestId, out DailyQuestPrototype? proto))
                continue;

            if (proto.QuestType != type)
                continue;

            if (!JobMatches(session, proto.RequiredJob))
                continue;

            if (proto.QuestType == DailyQuestType.HealOthers && uniquePlayerId == null)
                continue;

            if (uniquePlayerId != null)
            {
                if (!state.Round.UniqueProgress.TryGetValue(slot.QuestId, out var set))
                {
                    set = new HashSet<Guid>();
                    state.Round.UniqueProgress[slot.QuestId] = set;
                }

                if (!set.Add(uniquePlayerId.Value))
                    continue;
            }

            slot.Progress = Math.Min(slot.Progress + amount, proto.TargetCount);
            if (slot.Progress >= proto.TargetCount)
                CompleteSlot(session, slot, proto);

            Persist(session.UserId);
            return;
        }
    }

    private static bool IsTimeBasedQuest(DailyQuestPrototype proto) =>
        proto.QuestType is DailyQuestType.SurviveRound
            or DailyQuestType.NoMeleeHits
            or DailyQuestType.NoDamageTaken;

    private static (int Current, int Target) GetQuestProgressDisplay(
        PlayerDailyQuestState state,
        DailyQuestSlot slot,
        DailyQuestPrototype proto)
    {
        if (IsTimeBasedQuest(proto))
        {
            var current = (int)Math.Min(state.Round.ActivePlaytime.TotalSeconds, proto.MinRoundPlaytime.TotalSeconds);
            var target = Math.Max(1, (int)proto.MinRoundPlaytime.TotalSeconds);
            return (current, target);
        }

        return (slot.Progress, proto.TargetCount);
    }

    private bool UpdateLiveQuestProgress(ICommonSession session, PlayerDailyQuestState state)
    {
        var changed = false;

        foreach (var slot in state.Slots)
        {
            if (slot.Status != DailyQuestStatus.Active)
                continue;

            if (!_prototypes.TryIndex(slot.QuestId, out DailyQuestPrototype? proto))
                continue;

            if (!JobMatches(session, proto.RequiredJob))
                continue;

            if (proto.QuestType == DailyQuestType.SurviveRound
                && state.Round.WasActivePlayer
                && state.Round.ActivePlaytime >= proto.MinRoundPlaytime)
            {
                CompleteSlot(session, slot, proto);
                changed = true;
                continue;
            }

            // Pacifist / no-hit timer: complete mid-round once playtime is met (matches UI progress).
            if (proto.QuestType == DailyQuestType.NoMeleeHits
                && state.Round.WasActivePlayer
                && !state.Round.FailedNoMelee
                && state.Round.ActivePlaytime >= proto.MinRoundPlaytime)
            {
                CompleteSlot(session, slot, proto);
                changed = true;
                continue;
            }

            if (proto.QuestType == DailyQuestType.NoDamageTaken
                && state.Round.WasActivePlayer
                && !state.Round.FailedNoDamage
                && state.Round.ActivePlaytime >= proto.MinRoundPlaytime)
            {
                CompleteSlot(session, slot, proto);
                changed = true;
                continue;
            }

            if (proto.QuestType == DailyQuestType.EarnMiningPoints)
            {
                var before = slot.Progress;
                UpdateMiningProgress(session, state, slot, proto);
                if (slot.Progress != before || slot.Status != DailyQuestStatus.Active)
                    changed = true;
                continue;
            }

            if (proto.QuestType == DailyQuestType.EarnStationBankBalance)
            {
                var before = slot.Progress;
                UpdateStationBankProgress(session, state, slot, proto);
                if (slot.Progress != before || slot.Status != DailyQuestStatus.Active)
                    changed = true;
            }
        }

        return changed;
    }

    private bool JobMatches(ICommonSession session, ProtoId<JobPrototype>? requiredJob)
    {
        if (requiredJob == null)
            return true;

        var mind = session.GetMind();
        if (mind == null || !_jobs.MindTryGetJobId(mind, out var jobId))
            return false;

        return jobId == requiredJob;
    }

    private bool IsHumanPlayer(EntityUid uid)
    {
        // MindComponent lives on the mind entity, not the body.
        if (!_minds.TryGetMind(uid, out _, out var mind) || mind.UserId == null)
            return false;

        return _playerManager.TryGetSessionById(mind.UserId.Value, out _);
    }

    private bool TryGetPlayerState(EntityUid uid, out PlayerDailyQuestState state)
    {
        state = null!;
        if (!TryGetSession(uid, out var session))
            return false;

        if (!_states.TryGetValue(session.UserId, out var found))
            return false;

        state = found;
        return true;
    }

    private bool TryGetSession(EntityUid uid, out ICommonSession session)
    {
        session = null!;
        if (!_minds.TryGetMind(uid, out _, out var mind) || mind.UserId is not { } userId)
            return false;

        if (!_playerManager.TryGetSessionById(userId, out var found) || found is null)
            return false;

        session = found;
        return true;
    }

    private void FlushPlaytime(PlayerDailyQuestState state)
    {
        if (!state.Round.WasActivePlayer || state.Round.TrackingSince == null)
            return;

        state.Round.ActivePlaytime += _timing.CurTime - state.Round.TrackingSince.Value;
        state.Round.TrackingSince = _timing.CurTime;
    }

    private void RestoreRoundTracker(NetUserId userId, PlayerDailyQuestState state, bool onlyIfEmpty = false)
    {
        if (!_roundTrackers.TryGetValue(userId, out var round))
            return;

        if (onlyIfEmpty && state.Round.WasActivePlayer)
            return;

        state.Round = CloneRoundTracker(round, persistEntityReference: false);
        state.Round.TrackingSince = null;
    }

    private EntityUid? TryGetQuestTrackingEntity(ICommonSession session, PlayerDailyQuestState state)
    {
        if (state.Round.ActiveEntity is { Valid: true } active && EntityBelongsToSession(active, session))
            return active;

        if (session.AttachedEntity is { Valid: true } attached)
            return attached;

        state.Round.ActiveEntity = null;
        return null;
    }

    private bool EntityBelongsToSession(EntityUid uid, ICommonSession session)
    {
        if (session.GetMind() is not { } mindId)
            return false;

        return _minds.TryGetMind(uid, out var entityMindId, out _) && entityMindId == mindId;
    }

    private static DailyQuestRoundTracker CloneRoundTracker(
        DailyQuestRoundTracker source,
        bool persistEntityReference = true)
    {
        return new DailyQuestRoundTracker
        {
            WasActivePlayer = source.WasActivePlayer,
            ActiveEntity = persistEntityReference ? source.ActiveEntity : null,
            ActivePlaytime = source.ActivePlaytime,
            TrackingSince = source.TrackingSince,
            FailedNoMelee = source.FailedNoMelee,
            FailedNoDamage = source.FailedNoDamage,
            MiningPointsBaseline = source.MiningPointsBaseline,
            UniqueProgress = source.UniqueProgress.ToDictionary(
                kvp => kvp.Key,
                kvp => new HashSet<Guid>(kvp.Value)),
        };
    }

    private PlayerDailyQuestState EnsureState(NetUserId userId)
    {
        if (_states.TryGetValue(userId, out var existing))
            return existing;

        var weekStart = DailyQuestWeek.GetCurrentWeekStart();
        var state = PlayerDailyQuestState.FromDb(null, userId.UserId, weekStart);
        DeduplicateSlots(state);
        RestoreRoundTracker(userId, state, onlyIfEmpty: true);
        _states[userId] = state;
        return state;
    }

    private void Persist(NetUserId userId, bool refreshUi = true)
    {
        if (!_states.TryGetValue(userId, out var state))
            return;

        _ = _db.UpsertDailyQuestProgress(state.ToDb());
        if (refreshUi)
            EntityManager.System<DailyRewardSystem>().RefreshUi(userId);
    }

    private bool ArePreferencesReady(ICommonSession session) =>
        _preferences.TryGetCachedPreferences(session.UserId, out _);

    private bool IsQuestAvailable(ICommonSession session, DailyQuestPrototype quest)
    {
        if (quest.RequiredJob == null)
            return true;

        if (!ArePreferencesReady(session))
            return false;

        return _playTime.IsAllowed(session, quest.RequiredJob.Value);
    }

    private void EnsureDailyAssignmentsIfNeeded(ICommonSession session, PlayerDailyQuestState state)
    {
        if (TryRefreshDailyQuestsIfNeeded(session, state))
            return;

        if (!ArePreferencesReady(session))
            return;

        if (state.Slots.Count == 0)
        {
            EnsureDailyAssignments(session, state);
            return;
        }

        if (state.Slots.Count < DailyQuestCount)
            TopUpDailyQuestSlots(session, state);
    }

    private void TopUpDailyQuestSlots(ICommonSession session, PlayerDailyQuestState state)
    {
        var weekStart = DailyQuestWeek.GetCurrentWeekStart();
        var assigned = state.Slots.Select(s => s.QuestId).ToHashSet();

        while (state.Slots.Count < DailyQuestCount)
        {
            var pick = PickRandomQuest(session, weekStart, assigned);
            if (pick == null)
                break;

            state.Slots.Add(new DailyQuestSlot
            {
                QuestId = pick,
                Progress = 0,
                Status = DailyQuestStatus.Active,
            });
            assigned.Add(pick);
        }
    }

    private void EnsureDailyAssignments(ICommonSession session, PlayerDailyQuestState state)
    {
        if (!ArePreferencesReady(session))
            return;

        var weekStart = DailyQuestWeek.GetCurrentWeekStart();

        if (!DailyQuestWeek.Matches(state.QuestDate, weekStart))
        {
            state.QuestDate = weekStart;
            state.Slots.Clear();
            state.Round = new DailyQuestRoundTracker();
            state.DailyReplaceUsed = false;
            _roundTrackers.Remove(session.UserId);
        }

        DeduplicateSlots(state);

        for (var i = state.Slots.Count - 1; i >= 0; i--)
        {
            var slot = state.Slots[i];
            if (slot.Status == DailyQuestStatus.Claimed)
                continue;

            if (!_prototypes.TryIndex(slot.QuestId, out DailyQuestPrototype? proto) || !IsQuestAvailable(session, proto))
                state.Slots.RemoveAt(i);
        }

        var assigned = state.Slots.Select(s => s.QuestId).ToHashSet();
        while (state.Slots.Count < DailyQuestCount)
        {
            var pick = PickRandomQuest(session, weekStart, assigned);
            if (pick == null)
                break;

            state.Slots.Add(new DailyQuestSlot
            {
                QuestId = pick,
                Progress = 0,
                Status = DailyQuestStatus.Active,
            });
            assigned.Add(pick);
        }
    }

    private string? PickRandomQuestForReplace(ICommonSession session, DateTime date, HashSet<string> exclude)
    {
        var pick = PickRandomQuest(session, date, exclude);
        if (pick != null)
            return pick;

        var fallbackPool = _prototypes.EnumeratePrototypes<DailyQuestPrototype>()
            .Where(q => !exclude.Contains(q.ID) && !IsQuestFamilyBlocked(q, BuildAssignedFamilyKeys(exclude)))
            .ToList();

        if (fallbackPool.Count == 0)
            return null;

        var rng = new Random(HashCode.Combine(session.UserId.UserId.GetHashCode(), date.Date.Ticks, exclude.Count, 17));
        var totalWeight = fallbackPool.Sum(p => p.GetDropWeight());
        var roll = rng.NextDouble() * totalWeight;
        var acc = 0f;

        foreach (var quest in fallbackPool)
        {
            acc += quest.GetDropWeight();
            if (roll <= acc)
                return quest.ID;
        }

        return fallbackPool[0].ID;
    }

    private string? PickRandomQuest(ICommonSession session, DateTime date, HashSet<string> exclude)
    {
        var assignedFamilies = BuildAssignedFamilyKeys(exclude);
        var pool = _prototypes.EnumeratePrototypes<DailyQuestPrototype>()
            .Where(q => !exclude.Contains(q.ID) && !IsQuestFamilyBlocked(q, assignedFamilies) && IsQuestAvailable(session, q))
            .ToList();

        if (pool.Count == 0)
            return null;

        var rng = new Random(HashCode.Combine(session.UserId.UserId.GetHashCode(), date.Date.Ticks, exclude.Count));
        var totalWeight = pool.Sum(p => p.GetDropWeight());
        var roll = rng.NextDouble() * totalWeight;
        var acc = 0f;

        foreach (var quest in pool)
        {
            acc += quest.GetDropWeight();
            if (roll <= acc)
                return quest.ID;
        }

        return pool[0].ID;
    }

    private static string GetQuestFamilyKey(DailyQuestPrototype quest)
        => $"{quest.QuestType}:{quest.RequiredJob?.Id ?? ""}";

    private HashSet<string> BuildAssignedFamilyKeys(HashSet<string> assignedQuestIds)
    {
        var families = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in assignedQuestIds)
        {
            if (_prototypes.TryIndex(id, out DailyQuestPrototype? proto))
                families.Add(GetQuestFamilyKey(proto));
        }

        return families;
    }

    private static bool IsQuestFamilyBlocked(DailyQuestPrototype quest, HashSet<string> assignedFamilies)
        => assignedFamilies.Contains(GetQuestFamilyKey(quest));

    private void DeduplicateSlots(PlayerDailyQuestState state)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = state.Slots.Count - 1; i >= 0; i--)
        {
            var questId = state.Slots[i].QuestId;
            if (string.IsNullOrWhiteSpace(questId))
            {
                state.Slots.RemoveAt(i);
                continue;
            }

            if (!_prototypes.TryIndex(questId, out DailyQuestPrototype? proto))
                continue;

            var family = GetQuestFamilyKey(proto);
            if (!seen.Add(family))
                state.Slots.RemoveAt(i);
        }
    }

    private sealed class PlayerDailyQuestState
    {
        public bool LoadedFromDb;
        public Guid PlayerId;
        public DateTime QuestDate;
        public bool DailyReplaceUsed;
        public List<DailyQuestSlot> Slots = new();
        public DailyQuestRoundTracker Round = new();

        public static PlayerDailyQuestState FromDb(
            DailyQuestProgress? db,
            Guid playerId,
            DateTime date)
        {
            var state = new PlayerDailyQuestState
            {
                PlayerId = playerId,
                QuestDate = date.Date,
            };

            if (db != null && DailyQuestWeek.Matches(db.QuestDate, date))
            {
                var (flagValues, dailyReplaceUsed) = ParseStatusFlags(db.StatusFlags);
                var ids = Split(db.AssignedQuestIds);
                var progress = SplitInt(db.ProgressValues);

                for (var i = 0; i < ids.Count; i++)
                {
                    var packed = i < flagValues.Count ? flagValues[i] : 0;

                    state.Slots.Add(new DailyQuestSlot
                    {
                        QuestId = ids[i],
                        Progress = i < progress.Count ? progress[i] : 0,
                        Status = (DailyQuestStatus)(packed & 3),
                    });
                }

                state.DailyReplaceUsed = dailyReplaceUsed;
            }

            return state;
        }

        public DailyQuestProgress ToDb()
        {
            var flags = string.Join(',', Slots.Select(s => (int)s.Status));
            if (DailyReplaceUsed)
                flags += ";r";

            return new DailyQuestProgress
            {
                PlayerId = PlayerId,
                QuestDate = DailyQuestWeek.ToStoredKey(QuestDate),
                AssignedQuestIds = string.Join(',', Slots.Select(s => s.QuestId)),
                ProgressValues = string.Join(',', Slots.Select(s => s.Progress)),
                StatusFlags = flags,
            };
        }

        private static (List<int> Flags, bool DailyReplaceUsed) ParseStatusFlags(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return (new List<int>(), false);

            var parts = value.Split(';', 2, StringSplitOptions.TrimEntries);
            var dailyReplaceUsed = parts.Length > 1 && parts[1] == "r";
            return (SplitInt(parts[0]), dailyReplaceUsed);
        }

        private static List<string> Split(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return new List<string>();

            return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        private static List<int> SplitInt(string value)
        {
            var result = new List<int>();
            if (string.IsNullOrWhiteSpace(value))
                return result;

            foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, out var number))
                    result.Add(number);
            }

            return result;
        }
    }

    private sealed class DailyQuestSlot
    {
        public string QuestId = string.Empty;
        public int Progress;
        public DailyQuestStatus Status;
    }

    private enum DailyQuestStatus : byte
    {
        Active = 0,
        Completed = 1,
        Claimed = 2,
    }

    private sealed class DailyQuestRoundTracker
    {
        public bool WasActivePlayer;
        public EntityUid? ActiveEntity;
        public TimeSpan ActivePlaytime;
        public TimeSpan? TrackingSince;
        public bool FailedNoMelee;
        public bool FailedNoDamage;
        public uint MiningPointsBaseline;
        public Dictionary<string, HashSet<Guid>> UniqueProgress = new();

        public void Reset()
        {
            WasActivePlayer = false;
            ActiveEntity = null;
            ActivePlaytime = TimeSpan.Zero;
            TrackingSince = null;
            FailedNoMelee = false;
            FailedNoDamage = false;
            MiningPointsBaseline = 0;
            UniqueProgress.Clear();
        }
    }
}
