// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Server._Mini.Networking;
using Content.Server._Mini.Typan.StationGoal;
using Content.Server._CorvaxGoob.Skills;
using Robust.Shared.Prototypes;
using Content.Server._TT.StationHandleJob;
using Content.Server.AlertLevel;
using Content.Server.Antag.Components;
using Content.Server.Audio;
using Content.Server.Cargo.Components;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.RoundEnd;
using Content.Server.StationEvents.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared._Mini.TypanWar;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Server.Station.Systems;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared._EinsteinEngines.Silicon.Components;
using Content.Shared.Station.Components;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Threading;

namespace Content.Server._Mini.TypanWar;

public sealed class TypanStationWarRuleSystem : GameRuleSystem<TypanStationWarRuleComponent>
{
    public static bool IsWarActive { get; private set; }

    /// <summary>True while the Typan station war gamemode rule is running (prep + combat).</summary>
    public static bool IsModeActive { get; private set; }

    private static readonly SoundPathSpecifier WarDeclarationSound = TypanWarSounds.WarDeclaration;

    [Dependency] private readonly AlertLevelSystem _alertLevel = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly TTStationHandleJobSystem _typanJobs = default!;
    [Dependency] private readonly TypanWarFriendlyFireSystem _friendlyFire = default!;
    [Dependency] private readonly TypanWarBalanceSystem _warBalance = default!;
    [Dependency] private readonly TypanStationGoalObjectiveSystem _typanGoals = default!;
    [Dependency] private readonly NtStationGoalObjectiveSystem _ntGoals = default!;
    [Dependency] private readonly TypanStationWarMapEnsureSystem _mapEnsure = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TypanWarCaptureZoneSystem _captureZones = default!;
    [Dependency] private readonly TypanWarMinimapSystem _minimap = default!;
    [Dependency] private readonly TypanWarDropShuttleSystem _dropShuttles = default!;
    [Dependency] private readonly PvsSessionOverrideSystem _pvsSession = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private float _statusBroadcastAccumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GameRuleAddedEvent>(OnGameRuleAdded);
        SubscribeNetworkEvent<TypanWarStatusRequestEvent>(OnStatusRequest);
        SubscribeLocalEvent<ConsoleFTLAttemptEvent>(OnConsoleFtlAttempt);
        SubscribeLocalEvent<ShuttleFTLAttemptEvent>(OnShuttleFtlAttempt);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    /// <summary>
    /// True while the war isolates the round from antags and station events.
    /// </summary>
    public bool IsTypanWarBlocking()
    {
        return IsTypanWarRoundIsolated();
    }

    private bool IsTypanWarRoundIsolated()
    {
        var query = EntityQueryEnumerator<TypanStationWarRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var component, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            return component.Phase is TypanWarPhase.Pending or TypanWarPhase.Active;
        }

        return false;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        IsWarActive = false;
        IsModeActive = false;

        var query = EntityQueryEnumerator<TypanStationWarRuleComponent>();
        while (query.MoveNext(out _, out var component))
            StopWarMusic(component);

        ClearWarModeEffectsFromAllPlayers();
        BroadcastInactiveStatus();
    }

    private void BroadcastInactiveStatus()
    {
        RaiseNetworkEvent(new TypanWarStatusEvent(
            TypanWarPhase.Inactive,
            0,
            0,
            0,
            0,
            100,
            0), Filter.Broadcast());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.InGame)
            return;

        SendStatusToSession(args.Session);
    }

    private void OnStatusRequest(TypanWarStatusRequestEvent ev, EntitySessionEventArgs args)
    {
        SendStatusToSession(args.SenderSession);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (TryGetRunningWarRule(out var component)
            && component.Phase is TypanWarPhase.Pending or TypanWarPhase.Active)
        {
            EnsureWarModePlayerEffects(args.Mob);

            if (_mind.TryGetMind(args.Mob, out var mindId, out var mind))
                RecordFactionJoin(component, (mindId, mind));
            else if (args.JobId is { } jobId)
                RecordFactionJoin(component, args.Player.UserId, jobId);
        }

        if (!TryGetRunningWarRule(out var warComponent)
            || warComponent.Phase is not (TypanWarPhase.Pending or TypanWarPhase.Active))
            return;

        if (!_mind.TryGetMind(args.Mob, out var combatMindId, out var combatMind))
            return;

        if (!TryGetWarSide((combatMindId, combatMind), out var side))
            return;

        _friendlyFire.SetFaction(args.Mob, side);

        if (warComponent.Phase == TypanWarPhase.Active && !IsSilicon(args.Mob))
            _friendlyFire.SetupCombatant(args.Mob, side);

        _minimap.EnsureMinimapAction(args.Mob);

        SetupCombatMind((combatMindId, combatMind), args, side);
    }

    private void SetupCombatMind(Entity<MindComponent> mind, PlayerSpawnCompleteEvent args, TypanWarSide side)
    {
        var combat = EnsureComp<TypanWarCombatMindComponent>(mind);
        combat.Side = side;
        combat.Station = args.Station;
        if (args.JobId != null)
            combat.Job = new ProtoId<JobPrototype>(args.JobId);
        combat.AllowBaseSpawn = args.JobId is "SecurityOfficer" or "TypanPatrol";
        if (combat.BaseSpawn == default)
            combat.BaseSpawn = Transform(args.Mob).Coordinates;
        combat.RespawnAvailableAt = null;
        combat.RespawnUiOpen = false;
        Dirty(mind, combat);
    }

    public void AddCapturePoints(TypanWarCaptureOwner owner, float amount)
    {
        var query = EntityQueryEnumerator<TypanStationWarRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var component, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule) || component.Phase != TypanWarPhase.Active)
                continue;

            switch (owner)
            {
                case TypanWarCaptureOwner.Nanotrasen:
                    component.NtCapturePoints += amount;
                    break;
                case TypanWarCaptureOwner.Typan:
                    component.TypanCapturePoints += amount;
                    break;
            }

            if (component.NtCapturePoints >= component.CapturePointsToWin)
            {
                component.Winner = TypanWarWinner.Nanotrasen;
                EndWar(uid, component);
            }
            else if (component.TypanCapturePoints >= component.CapturePointsToWin)
            {
                component.Winner = TypanWarWinner.Typan;
                EndWar(uid, component);
            }

            return;
        }
    }

    private bool IsSilicon(EntityUid uid)
    {
        return HasComp<SiliconComponent>(uid) || HasComp<BorgChassisComponent>(uid);
    }

    private void OnConsoleFtlAttempt(ref ConsoleFTLAttemptEvent ev)
    {
        if (ev.Cancelled || !ShouldBlockFtl())
            return;

        ev.Cancelled = true;
        ev.Reason = Loc.GetString("typan-war-ftl-blocked");
    }

    private void OnShuttleFtlAttempt(ref ShuttleFTLAttemptEvent ev)
    {
        if (ev.Cancelled || !ShouldBlockFtl())
            return;

        // Arrivals shuttles must keep cycling during prep so late-join players reach the station.
        if (HasComp<ArrivalsShuttleComponent>(ev.ShuttleUid))
            return;

        ev.Cancelled = true;
        ev.Reason = Loc.GetString("typan-war-ftl-blocked");
    }

    /// <summary>
    /// Bluespace travel is blocked for the whole station war (prep and combat), except arrivals shuttles.
    /// </summary>
    private bool ShouldBlockFtl()
    {
        var query = EntityQueryEnumerator<TypanStationWarRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var component, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            return component.Phase is TypanWarPhase.Pending or TypanWarPhase.Active;
        }

        return false;
    }

    protected override void Started(EntityUid uid, TypanStationWarRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryResolveStations(component, out var ntStation, out var typanStation))
        {
            _mapEnsure.TryEnsureSupplementalMaps();

            if (TryResolveStations(component, out ntStation, out typanStation))
            {
                BeginWarMode(uid, component, ntStation, typanStation);
                return;
            }

            component.AwaitingStations = true;
            component.AwaitingStationsAccumulator = 0f;
            Log.Warning("Typan station war: waiting for NT and Typan stations to finish loading...");
            return;
        }

        BeginWarMode(uid, component, ntStation, typanStation);
    }

    private void BeginWarMode(EntityUid uid, TypanStationWarRuleComponent component, EntityUid ntStation, EntityUid typanStation)
    {
        component.AwaitingStations = false;
        component.NtStation = ntStation;
        component.TypanStation = typanStation;
        component.Phase = TypanWarPhase.Pending;
        component.AnnouncementSent = false;
        component.AnnouncementTime = _timing.CurTime + TimeSpan.FromSeconds(component.AnnouncementDelaySeconds);
        component.WarStartTime = _timing.CurTime + TimeSpan.FromSeconds(component.WarStartDelaySeconds);
        component.WarEndTime = component.WarStartTime + TimeSpan.FromSeconds(component.WarDurationSeconds);

        IsModeActive = true;
        SeedJoinedRoster(component);
        SetupWarFactionMarkers();
        ApplyWarModeEffectsToAllPlayers();
        BroadcastStatus(component);
    }

    protected override void Ended(EntityUid uid, TypanStationWarRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        IsWarActive = false;
        IsModeActive = false;
        component.Phase = TypanWarPhase.Inactive;
        StopWarMusic(component);
        ClearWarModeEffectsFromAllPlayers();
        ClearWarCombatants();
        _dropShuttles.ClearDropShuttleState(component);
        _warBalance.NotifyCombatPhaseEnded();
        BroadcastStatus(component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<TypanStationWarRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var component, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            if (component.AwaitingStations)
            {
                component.AwaitingStationsAccumulator += frameTime;

                if (TryResolveStations(component, out var ntStation, out var typanStation))
                {
                    BeginWarMode(uid, component, ntStation, typanStation);
                }
                else if (component.AwaitingStationsAccumulator >= 30f)
                {
                    Log.Error("Typan station war cancelled: NT and Typan stations never appeared after map load.");
                    ForceEndSelf(uid, gameRule);
                }

                continue;
            }

            if (component.Phase == TypanWarPhase.Ended || component.Phase == TypanWarPhase.Inactive)
                continue;

            if (component.Phase == TypanWarPhase.Pending)
            {
                TryPlayPrepCountdown(component);
                TryCheckInsufficientForces(uid, component, gameRule, frameTime);

                if (component.WarStartTime != null &&
                    _timing.CurTime >= component.WarStartTime)
                {
                    StartWar(uid, component);
                }
            }

            if (component.Phase == TypanWarPhase.Pending &&
                !component.AnnouncementSent &&
                component.AnnouncementTime != null &&
                _timing.CurTime >= component.AnnouncementTime)
            {
                SendPrepAnnouncement(component);
            }

            if (component.Phase == TypanWarPhase.Active)
            {
                TryStartWarMusic(component);
                TryPlayWarEndWarning(component);
                TryRunWarEvents(component);
            }

            if (component.Phase == TypanWarPhase.Active &&
                component.WarEndTime != null &&
                _timing.CurTime >= component.WarEndTime)
            {
                EndWar(uid, component);
            }

            _statusBroadcastAccumulator += frameTime;
            if (_statusBroadcastAccumulator >= 1f)
            {
                _statusBroadcastAccumulator = 0f;
                BroadcastStatus(component);
            }
        }
    }

    protected override void AppendRoundEndText(EntityUid uid, TypanStationWarRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        if (component.Phase == TypanWarPhase.Inactive)
            return;

        args.AddLine(Loc.GetString("typan-war-round-end-header"));
        var ntJoined = component.NtJoinedUsers.Count;
        var typanJoined = component.TypanJoinedUsers.Count;

        args.AddLine(Loc.GetString("typan-war-round-end-initial",
            ("nt", ntJoined),
            ("typan", typanJoined)));
        args.AddLine(Loc.GetString("typan-war-round-end-final",
            ("ntPoints", (int) component.NtCapturePoints),
            ("typanPoints", (int) component.TypanCapturePoints),
            ("win", component.CapturePointsToWin)));

        if (component.NtStation is { } ntStation
            && _ntGoals.TryGetActiveGoalTitle(ntStation, out var ntGoal))
        {
            args.AddLine(Loc.GetString("typan-war-round-end-nt-goal", ("goal", ntGoal)));
        }

        if (component.TypanStation is { } typanStation
            && _typanGoals.TryGetActiveGoalTitle(typanStation, out var typanGoal))
        {
            args.AddLine(Loc.GetString("typan-war-round-end-typan-goal", ("goal", typanGoal)));
        }

        var winnerKey = component.Winner switch
        {
            TypanWarWinner.Nanotrasen => "typan-war-round-end-winner-nt",
            TypanWarWinner.Typan => "typan-war-round-end-winner-typan",
            _ => "typan-war-round-end-stalemate",
        };
        args.AddLine(Loc.GetString(winnerKey));
    }

    private void SendPrepAnnouncement(TypanStationWarRuleComponent component)
    {
        if (component.AnnouncementSent)
            return;

        component.AnnouncementSent = true;
        _chat.DispatchGlobalAnnouncement(
            Loc.GetString("typan-war-prep-announce"),
            Loc.GetString("typan-war-sender"),
            announcementSound: TypanWarSounds.HeadquartersAlert,
            colorOverride: TypanWarColors.Neutral);
        BroadcastStatus(component);
    }

    private void SendManifestAnnouncement(TypanStationWarRuleComponent? component = null)
    {
        var message = Loc.GetString("typan-war-manifest");

        if (component != null)
        {
            message += "\n"
                + Loc.GetString("typan-war-manifest-score",
                    ("nt", (int) component.NtCapturePoints),
                    ("typan", (int) component.TypanCapturePoints),
                    ("win", component.CapturePointsToWin));
        }

        SendMarkupGlobalAnnouncement(message);
    }

    private void SendMarkupGlobalAnnouncement(string message, Color? colorOverride = null)
    {
        var wrappedMessage = Loc.GetString(
            "chat-manager-sender-announcement-wrap-message",
            ("sender", Loc.GetString("typan-war-sender")),
            ("message", message));
        _chatManager.ChatMessageToAll(
            ChatChannel.Radio,
            message,
            wrappedMessage,
            default,
            false,
            true,
            colorOverride ?? TypanWarColors.Neutral);
    }

    private void StartWar(EntityUid ruleUid, TypanStationWarRuleComponent component)
    {
        var ntAlive = CountNtAlive();
        var typanAlive = CountTypanAlive();

        if (!HasSufficientForces(component, ntAlive, typanAlive))
        {
            CancelWarInsufficient(ruleUid, component, ntAlive, typanAlive);
            return;
        }

        CacheStationGoalTitles(component);
        SeedJoinedRoster(component);
        component.Phase = TypanWarPhase.Active;
        IsWarActive = true;

        if (component.NtStation is { } nt)
            _alertLevel.SetLevel(nt, "gamma", true, true, true, locked: true);

        if (component.TypanStation is { } typan)
            _alertLevel.SetLevel(typan, "omega", true, true, true, locked: true);

        var announcement = Loc.GetString("typan-war-declaration");
        _chat.DispatchGlobalAnnouncement(
            announcement,
            Loc.GetString("typan-war-sender"),
            playSound: false,
            colorOverride: TypanWarColors.Neutral);
        _audio.PlayGlobal(WarDeclarationSound, Filter.Broadcast(), false, AudioParams.Default.WithVolume(-2f));

        AssignWarObjectives(component);
        SetupWarCombatants();
        _pvsSession.RefreshAllPlayers();
        BroadcastStatus(component);

        if (component.NtStation is { } ntStation && component.TypanStation is { } typanStation)
            RaiseLocalEvent(new TypanWarStartedEvent(ruleUid, ntStation, typanStation));
    }

    private void CancelWarInsufficient(EntityUid ruleUid, TypanStationWarRuleComponent component, int ntAlive, int typanAlive)
    {
        component.Phase = TypanWarPhase.Ended;
        component.Winner = TypanWarWinner.Stalemate;

        var locKey = ntAlive < component.MinNtAlive
            ? "typan-war-start-cancelled-nt"
            : typanAlive < component.MinTypanAlive
                ? "typan-war-start-cancelled-typan"
                : "typan-war-start-cancelled";

        _chat.DispatchGlobalAnnouncement(
            Loc.GetString(locKey,
                ("nt", ntAlive),
                ("ntMin", component.MinNtAlive),
                ("typan", typanAlive),
                ("typanMin", component.MinTypanAlive)),
            Loc.GetString("typan-war-sender"),
            announcementSound: TypanWarSounds.HeadquartersAlert,
            colorOverride: TypanWarColors.Neutral);
        BroadcastStatus(component);
        _warBalance.NotifyCombatPhaseEnded();
        ClearWarModeEffectsFromAllPlayers();
        ForceEndSelf(ruleUid);
    }

    private static bool HasSufficientForces(TypanStationWarRuleComponent component, int ntAlive, int typanAlive)
    {
        return ntAlive >= component.MinNtAlive && typanAlive >= component.MinTypanAlive;
    }

    private void CacheStationGoalTitles(TypanStationWarRuleComponent component)
    {
        component.NtStationGoalTitle = null;
        component.TypanStationGoalTitle = null;

        if (component.NtStation is { } nt && _ntGoals.TryGetActiveGoalTitle(nt, out var ntGoal))
            component.NtStationGoalTitle = ntGoal;

        if (component.TypanStation is { } typan && _typanGoals.TryGetActiveGoalTitle(typan, out var typanGoal))
            component.TypanStationGoalTitle = typanGoal;
    }

    private void TryCheckInsufficientForces(EntityUid ruleUid, TypanStationWarRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        component.PrepInsufficientCheckAccumulator += frameTime;
        if (component.PrepInsufficientCheckAccumulator < component.PrepInsufficientCheckIntervalSeconds)
            return;

        component.PrepInsufficientCheckAccumulator = 0f;

        var ntAlive = CountNtAlive();
        var typanAlive = CountTypanAlive();
        if (HasSufficientForces(component, ntAlive, typanAlive))
            return;

        CancelWarInsufficient(ruleUid, component, ntAlive, typanAlive);
    }

    private void TryPlayPrepCountdown(TypanStationWarRuleComponent component)
    {
        if (component.PrepCountdownPlayed || component.WarStartTime == null)
            return;

        var remaining = (component.WarStartTime.Value - _timing.CurTime).TotalSeconds;
        if (remaining > component.PrepCountdownSoundSeconds || remaining <= 0)
            return;

        component.PrepCountdownPlayed = true;
        _audio.PlayGlobal(WarDeclarationSound, Filter.Broadcast(), false, AudioParams.Default.WithVolume(-4f));
    }

    private void TryPlayWarEndWarning(TypanStationWarRuleComponent component)
    {
        if (component.WarEndWarningPlayed || component.WarEndTime == null)
            return;

        var remaining = (component.WarEndTime.Value - _timing.CurTime).TotalSeconds;
        if (remaining > component.WarEndWarningSeconds || remaining <= 0)
            return;

        component.WarEndWarningPlayed = true;
        _chat.DispatchGlobalAnnouncement(
            Loc.GetString("typan-war-end-warning"),
            Loc.GetString("typan-war-sender"),
            announcementSound: TypanWarSounds.HeadquartersAlert,
            colorOverride: TypanWarColors.Neutral);
    }

    private void TryRunWarEvents(TypanStationWarRuleComponent component)
    {
        if (component.WarStartTime == null)
            return;

        var elapsed = (_timing.CurTime - component.WarStartTime.Value).TotalSeconds;

        if (!component.WarIntelEventSent && elapsed >= component.WarIntelEventDelaySeconds)
        {
            component.WarIntelEventSent = true;
            SendMarkupGlobalAnnouncement(Loc.GetString("typan-war-event-intel",
                ("nt", (int) component.NtCapturePoints),
                ("typan", (int) component.TypanCapturePoints)));
        }
    }

    private void EndWar(EntityUid ruleUid, TypanStationWarRuleComponent component)
    {
        if (component.Phase == TypanWarPhase.Ended)
            return;

        component.Phase = TypanWarPhase.Ended;
        IsWarActive = false;
        IsModeActive = false;
        StopWarMusic(component);
        ClearWarModeEffectsFromAllPlayers();
        ClearWarCombatants();
        _dropShuttles.ClearDropShuttleState(component);
        _pvsSession.RefreshAllPlayers();

        if (component.Winner == TypanWarWinner.None)
            component.Winner = DetermineWinner(component);

        var winnerKey = component.Winner switch
        {
            TypanWarWinner.Nanotrasen => "typan-war-end-announce-nt",
            TypanWarWinner.Typan => "typan-war-end-announce-typan",
            _ => "typan-war-end-announce-stalemate",
        };

        SendManifestAnnouncement(component);
        SendMarkupGlobalAnnouncement(
            Loc.GetString(winnerKey,
                ("nt", (int) component.NtCapturePoints),
                ("typan", (int) component.TypanCapturePoints)),
            TypanWarColors.ForWinner(component.Winner));

        BroadcastStatus(component);
        _warBalance.NotifyCombatPhaseEnded();
        _roundEnd.EndRound(TimeSpan.FromSeconds(component.RoundEndDelaySeconds));
    }

    private void TryStartWarMusic(TypanStationWarRuleComponent component)
    {
        if (component.WarMusicStarted || component.WarStartTime == null)
            return;

        if (_timing.CurTime < component.WarStartTime + TimeSpan.FromSeconds(component.WarMusicDelaySeconds))
            return;

        component.WarMusicStarted = true;
        PlayWarMusicCycle(component);
    }

    private void PlayWarMusicCycle(TypanStationWarRuleComponent component)
    {
        if (component.Phase != TypanWarPhase.Active || component.WarMusicTracks.Count == 0)
            return;

        var trackIndex = component.WarMusicTrackIndex % component.WarMusicTracks.Count;
        var trackPath = component.WarMusicTracks[trackIndex];
        var duration = trackIndex < component.WarMusicTrackDurations.Count
            ? component.WarMusicTrackDurations[trackIndex]
            : component.WarMusicDurationSeconds;

        var result = _audio.PlayGlobal(
            new SoundPathSpecifier(trackPath),
            Filter.Broadcast(),
            false,
            AudioParams.Default.WithVolume(-4f));

        if (result != null)
            component.WarMusicAudio = result.Value.Entity;

        component.WarMusicTrackIndex = (trackIndex + 1) % component.WarMusicTracks.Count;

        component.WarMusicLoopCancel?.Cancel();
        component.WarMusicLoopCancel = new CancellationTokenSource();
        var token = component.WarMusicLoopCancel.Token;

        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(duration), () =>
        {
            if (token.IsCancellationRequested || component.Phase != TypanWarPhase.Active)
                return;

            PlayWarMusicCycle(component);
        }, token);
    }

    private void StopWarMusic(TypanStationWarRuleComponent component)
    {
        component.WarMusicLoopCancel?.Cancel();
        component.WarMusicLoopCancel = null;
        component.WarMusicStarted = false;

        if (component.WarMusicAudio is { } audio)
        {
            _audio.Stop(audio);
            component.WarMusicAudio = null;
        }
    }

    private static TypanWarWinner DetermineWinner(TypanStationWarRuleComponent component)
    {
        if (component.NtCapturePoints > component.TypanCapturePoints)
            return TypanWarWinner.Nanotrasen;

        if (component.TypanCapturePoints > component.NtCapturePoints)
            return TypanWarWinner.Typan;

        return TypanWarWinner.Stalemate;
    }

    private void AssignWarObjectives(TypanStationWarRuleComponent component)
    {
        var minds = EntityQueryEnumerator<MindComponent>();
        while (minds.MoveNext(out var mindId, out var mind))
        {
            if (!IsMindAlive(mind))
                continue;

            if (_typanJobs.MindHasHandledJob(mindId))
                TryAddObjective(mindId, mind, "TypanWarObjective", Loc.GetString("typan-war-objective-typan"));
            else if (_jobs.MindTryGetJobId(mindId, out var jobId) && jobId != null)
                TryAddObjective(mindId, mind, "NtWarObjective", Loc.GetString("typan-war-objective-nt"));
        }
    }

    private void TryAddObjective(EntityUid mindId, MindComponent mind, string proto, string text)
    {
        if (_mind.TryFindObjective((mindId, mind), proto, out _))
            return;

        if (!_mind.TryAddObjective(mindId, mind, proto))
            return;

        if (!_mind.TryFindObjective((mindId, mind), proto, out var objective) || objective == null)
            return;

        _metaData.SetEntityDescription(objective.Value, text, MetaData(objective.Value));
    }

    private void SetupWarFactionMarkers()
    {
        var minds = EntityQueryEnumerator<MindComponent>();
        while (minds.MoveNext(out var mindId, out var mind))
        {
            if (!IsMindAlive(mind) || mind.CurrentEntity is not { } mob)
                continue;

            if (!TryGetWarSide((mindId, mind), out var side))
                continue;

            _friendlyFire.SetFaction(mob, side);

            if (IsWarActive && !IsSilicon(mob))
            {
                _friendlyFire.SetupCombatant(mob, side);
                _minimap.EnsureMinimapAction(mob);
            }
        }
    }

    private void SetupWarCombatants()
    {
        SetupWarFactionMarkers();
    }

    private void ClearWarCombatants()
    {
        var query = EntityQueryEnumerator<TypanWarFactionComponent>();
        while (query.MoveNext(out var uid, out _))
            _friendlyFire.RemoveCombatant(uid);

        var minimap = EntityQueryEnumerator<TypanWarMinimapComponent>();
        while (minimap.MoveNext(out var uid, out _))
            _minimap.RemoveMinimapAction(uid);
    }

    private void EnsureWarModePlayerEffects(EntityUid mob)
    {
        EnsureComp<IgnoreSkillsComponent>(mob);
    }

    private void ClearWarModePlayerEffects(EntityUid mob)
    {
        RemComp<IgnoreSkillsComponent>(mob);
    }

    private void ApplyWarModeEffectsToAllPlayers()
    {
        var minds = EntityQueryEnumerator<MindComponent>();
        while (minds.MoveNext(out _, out var mind))
        {
            if (!IsMindAlive(mind) || mind.CurrentEntity is not { } mob)
                continue;

            EnsureWarModePlayerEffects(mob);
        }
    }

    private void ClearWarModeEffectsFromAllPlayers()
    {
        var minds = EntityQueryEnumerator<MindComponent>();
        while (minds.MoveNext(out _, out var mind))
        {
            if (mind.CurrentEntity is not { } mob)
                continue;

            ClearWarModePlayerEffects(mob);
        }
    }

    public bool TryGetWarSide(Entity<MindComponent> mind, out TypanWarSide side) => TryGetWarSideInternal(mind, out side);

    private bool TryGetWarSideInternal(Entity<MindComponent> mind, out TypanWarSide side)
    {
        side = default;

        if (_typanJobs.MindHasHandledJob(mind.Owner))
        {
            side = TypanWarSide.Typan;
            return true;
        }

        if (_jobs.MindTryGetJobId(mind.Owner, out var jobId) && jobId != null)
        {
            side = TypanWarSide.Nanotrasen;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Living faction headcounts used by war logic and late-join balance (includes silicons).
    /// </summary>
    public (int Nt, int Typan) CountFactionAlive() => (CountNtAlive(), CountTypanAlive());

    private int CountTypanAlive()
    {
        var count = 0;
        var minds = EntityQueryEnumerator<MindComponent>();
        while (minds.MoveNext(out var mindId, out var mind))
        {
            if (!IsMindAlive(mind) || !_typanJobs.MindHasHandledJob(mindId))
                continue;

            count++;
        }

        return count;
    }

    private int CountNtAlive()
    {
        var count = 0;
        var minds = EntityQueryEnumerator<MindComponent>();
        while (minds.MoveNext(out var mindId, out var mind))
        {
            if (!IsMindAlive(mind))
                continue;

            if (!_jobs.MindTryGetJobId(mindId, out var jobId) || jobId == null)
                continue;

            if (_typanJobs.IsHandledJob(jobId.Value))
                continue;

            count++;
        }

        return count;
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

    private void BroadcastStatus(TypanStationWarRuleComponent component)
    {
        var phase = component.Phase;
        var ntAlive = phase >= TypanWarPhase.Active ? CountNtAlive() : 0;
        var typanAlive = phase >= TypanWarPhase.Active ? CountTypanAlive() : 0;

        float remaining = 0f;
        if (phase == TypanWarPhase.Pending && component.WarStartTime != null)
            remaining = (float) (component.WarStartTime.Value - _timing.CurTime).TotalSeconds;
        else if (phase == TypanWarPhase.Active && component.WarEndTime != null)
            remaining = (float) (component.WarEndTime.Value - _timing.CurTime).TotalSeconds;

        remaining = Math.Max(0f, remaining);

        RaiseNetworkEvent(new TypanWarStatusEvent(
            phase,
            ntAlive,
            typanAlive,
            component.NtCapturePoints,
            component.TypanCapturePoints,
            component.CapturePointsToWin,
            remaining,
            component.Winner,
            _captureZones.GetZoneStatuses(),
            CollectAllyBlips(),
            CollectMinimapGrids(component)), Filter.Broadcast());
    }

    private TypanWarMinimapGrid[] CollectMinimapGrids(TypanStationWarRuleComponent rule)
    {
        if (rule.Phase != TypanWarPhase.Active || !rule.LayoutApplied)
            return Array.Empty<TypanWarMinimapGrid>();

        var list = new List<TypanWarMinimapGrid>();

        if (rule.NtStation is { } ntStation && TryComp<StationDataComponent>(ntStation, out var ntData))
            CollectStationGrids(ntStation, ntData, TypanWarMinimapGridKind.NtStation, TypanWarMinimapGridKind.NtShuttle, list);

        if (rule.TypanStation is { } typanStation && TryComp<StationDataComponent>(typanStation, out var typanData))
            CollectStationGrids(typanStation, typanData, TypanWarMinimapGridKind.TypanStation, TypanWarMinimapGridKind.TypanShuttle, list);

        return list.ToArray();
    }

    private void CollectStationGrids(
        EntityUid station,
        StationDataComponent stationData,
        TypanWarMinimapGridKind stationKind,
        TypanWarMinimapGridKind shuttleKind,
        List<TypanWarMinimapGrid> list)
    {
        var largest = _station.GetLargestGrid((station, stationData));

        foreach (var gridUid in stationData.Grids)
        {
            if (!TryComp<MapGridComponent>(gridUid, out _))
                continue;

            var aabb = _lookup.GetWorldAABB(gridUid);
            if (aabb.Size == Vector2.Zero)
                continue;

            var kind = gridUid switch
            {
                _ when HasComp<TradeStationComponent>(gridUid) => TypanWarMinimapGridKind.Trade,
                _ when gridUid != largest && HasComp<ShuttleComponent>(gridUid) => shuttleKind,
                _ => stationKind,
            };

            var name = kind is TypanWarMinimapGridKind.NtShuttle or TypanWarMinimapGridKind.TypanShuttle
                ? MetaData(gridUid).EntityName
                : string.Empty;

            list.Add(new TypanWarMinimapGrid(GetNetEntity(gridUid), aabb.Left, aabb.Bottom, aabb.Right, aabb.Top, kind, name));
        }
    }

    private TypanWarAllyBlip[] CollectAllyBlips()
    {
        var list = new List<TypanWarAllyBlip>();
        var minds = EntityQueryEnumerator<MindComponent>();
        while (minds.MoveNext(out var mindId, out var mind))
        {
            if (!IsMindAlive(mind) || !TryGetWarSide((mindId, mind), out var side))
                continue;

            if (mind.CurrentEntity is not { } ent || !Exists(ent))
                continue;

            var pos = _transform.GetWorldPosition(ent);
            list.Add(new TypanWarAllyBlip(pos.X, pos.Y, side));
        }

        return list.ToArray();
    }

    public void SendStatusToSession(ICommonSession session)
    {
        var query = EntityQueryEnumerator<TypanStationWarRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var ruleUid, out var component, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(ruleUid, gameRule))
                continue;

            if (component.Phase is TypanWarPhase.Inactive)
                continue;

            var phase = component.Phase;
            var ntAlive = phase >= TypanWarPhase.Active ? CountNtAlive() : 0;
            var typanAlive = phase >= TypanWarPhase.Active ? CountTypanAlive() : 0;

            float remaining = 0f;
            if (phase == TypanWarPhase.Pending && component.WarStartTime != null)
                remaining = (float) (component.WarStartTime.Value - _timing.CurTime).TotalSeconds;
            else if (phase == TypanWarPhase.Active && component.WarEndTime != null)
                remaining = (float) (component.WarEndTime.Value - _timing.CurTime).TotalSeconds;

            remaining = Math.Max(0f, remaining);

            RaiseNetworkEvent(
                new TypanWarStatusEvent(
                    phase,
                    ntAlive,
                    typanAlive,
                    component.NtCapturePoints,
                    component.TypanCapturePoints,
                    component.CapturePointsToWin,
                    remaining,
                    component.Winner,
                    _captureZones.GetZoneStatuses(),
                    CollectAllyBlips(),
                    CollectMinimapGrids(component)),
                session);
            return;
        }

        RaiseNetworkEvent(
            new TypanWarStatusEvent(TypanWarPhase.Inactive, 0, 0, 0, 0, 100, 0),
            session);
    }

    private void SeedJoinedRoster(TypanStationWarRuleComponent component)
    {
        var minds = EntityQueryEnumerator<MindComponent>();
        while (minds.MoveNext(out var mindId, out var mind))
            RecordFactionJoin(component, (mindId, mind));
    }

    private void RecordFactionJoin(TypanStationWarRuleComponent component, Entity<MindComponent> mind)
    {
        if (mind.Comp.UserId is not { } userId)
            return;

        if (_typanJobs.MindHasHandledJob(mind.Owner))
        {
            component.TypanJoinedUsers.Add(userId);
            return;
        }

        if (_jobs.MindTryGetJobId(mind.Owner, out var jobId) && jobId != null)
            component.NtJoinedUsers.Add(userId);
    }

    private void RecordFactionJoin(TypanStationWarRuleComponent component, NetUserId userId, string jobId)
    {
        if (_typanJobs.IsHandledJob(new ProtoId<JobPrototype>(jobId)))
            component.TypanJoinedUsers.Add(userId);
        else
            component.NtJoinedUsers.Add(userId);
    }

    private bool TryGetRunningWarRule([NotNullWhen(true)] out TypanStationWarRuleComponent? component)
    {
        var query = EntityQueryEnumerator<TypanStationWarRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var comp, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            if (comp.Phase is TypanWarPhase.Inactive or TypanWarPhase.Ended)
                continue;

            component = comp;
            return true;
        }

        component = null;
        return false;
    }

    private bool TryResolveStations(TypanStationWarRuleComponent component, out EntityUid ntStation, out EntityUid typanStation)
    {
        ntStation = EntityUid.Invalid;
        typanStation = EntityUid.Invalid;

        var stations = EntityQueryEnumerator<StationDataComponent>();
        while (stations.MoveNext(out var uid, out _))
        {
            if (HasComp<TTStationHandleJobComponent>(uid))
            {
                if (!typanStation.IsValid())
                    typanStation = uid;
                continue;
            }

            if (!ntStation.IsValid())
                ntStation = uid;
        }

        return ntStation.IsValid() && typanStation.IsValid();
    }

    private void OnGameRuleAdded(ref GameRuleAddedEvent args)
    {
        // Only block midround additions — roundstart rules are added in the lobby before war goes active.
        if (GameTicker.RunLevel != GameRunLevel.InRound || !IsTypanWarBlocking())
            return;

        if (HasComp<TypanStationWarRuleComponent>(args.RuleEntity))
            return;

        if (HasComp<AdminForcedGameRuleComponent>(args.RuleEntity))
            return;

        if (!TryComp<GameRuleComponent>(args.RuleEntity, out var rule))
            return;

        if (HasComp<AntagSelectionComponent>(args.RuleEntity) ||
            HasComp<StationEventComponent>(args.RuleEntity))
        {
            GameTicker.EndGameRule(args.RuleEntity, rule);
        }
    }
}
