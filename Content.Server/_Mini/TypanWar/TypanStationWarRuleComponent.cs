// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.TypanWar;
using Robust.Shared.Network;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Utility;
using System.Collections.Generic;
using System.Threading;

namespace Content.Server._Mini.TypanWar;

[RegisterComponent, Access(typeof(TypanStationWarRuleSystem), typeof(TypanWarBalanceSystem), typeof(TypanStationWarLayoutSystem), typeof(TypanWarRespawnSystem), typeof(TypanWarCaptureZoneSystem), typeof(TypanWarDropShuttleSystem))]
public sealed partial class TypanStationWarRuleComponent : Component
{
    [DataField]
    public float AnnouncementDelaySeconds = 15f;

    [DataField]
    public float WarStartDelaySeconds = 300f;

    [DataField]
    public float WarDurationSeconds = 2700f;

    [DataField]
    public float WarMusicDelaySeconds = 90f;

    /// <summary>Fallback track length when <see cref="WarMusicTrackDurations"/> is shorter than the playlist.</summary>
    [DataField]
    public float WarMusicDurationSeconds = 90f;

    [DataField]
    public List<ResPath> WarMusicTracks =
    [
        new("/Audio/_Mini/TypanWar/station_war.ogg"),
        new("/Audio/_Mini/TypanWar/desolation.ogg"),
    ];

    [DataField]
    public List<float> WarMusicTrackDurations = [90f, 90f];

    /// <summary>Next playlist index (runtime).</summary>
    public int WarMusicTrackIndex;

    [DataField]
    public float RoundEndDelaySeconds = 120f;

    [DataField]
    public int MinNtAlive = 1;

    [DataField]
    public int MinTypanAlive = 1;

    /// <summary>Max allowed faction headcount ratio (e.g. 2 means at most 2× players on one side).</summary>
    [DataField]
    public int MaxFactionRatio = 2;

    [DataField]
    public float PrepInsufficientCheckIntervalSeconds = 30f;

    [DataField]
    public float WarIntelEventDelaySeconds = 900f;

    [DataField]
    public float CaptureZoneActivationDelaySeconds = 0f;

    [DataField]
    public int CapturePointsToWin = 100;

    [DataField]
    public float CapturePointIntervalSeconds = 45f;

    [DataField]
    public float MinRespawnSeconds = 10f;

    [DataField]
    public float MaxRespawnSeconds = 120f;

    /// <summary>How long recent deaths count toward the death-penalty respawn delay.</summary>
    [DataField]
    public float DeathPenaltyWindowSeconds = 300f;

    /// <summary>Extra respawn seconds added per death beyond the first inside the window.</summary>
    [DataField]
    public float DeathPenaltyStepSeconds = 20f;

    [DataField]
    public float NtCapturePoints;

    [DataField]
    public float TypanCapturePoints;

    /// <summary>When either faction reaches this score, trade zone C swaps to the other trade post.</summary>
    [DataField]
    public float TradeZoneSwapScoreThreshold = 50f;

    public bool TradeZoneSwapped;

    /// <summary>Coins (antag token balance) paid to every war participant at EndWar.</summary>
    [DataField]
    public int ParticipationTokenReward = 1;

    /// <summary>Extra coins for participants of the winning faction.</summary>
    [DataField]
    public int VictoryTokenReward = 2;

    /// <summary>Extra coins for the MVP of each faction (top contributor by captures + kills).</summary>
    [DataField]
    public int MvpTokenReward = 5;

    /// <summary>Per-player contribution for MVP pick and round-end text (runtime).</summary>
    public Dictionary<NetUserId, TypanWarPlayerStats> PlayerStats = new();

    /// <summary>Coins granted per user at EndWar (runtime, used by round-end text).</summary>
    public Dictionary<NetUserId, int> RewardsGranted = new();

    /// <summary>Total coins paid to each faction at EndWar (runtime, used by round-end text).</summary>
    public int NtRewardsTotal;

    public int TypanRewardsTotal;

    public string? NtMvpName;

    public string? TypanMvpName;

    /// <summary>Set for technical stalemates (layout failure) where nobody fought — suppress rewards.</summary>
    public bool SkipRewards;

    /// <summary>Max capture points a faction can earn from combat kills per war.</summary>
    [DataField]
    public int KillScoreCap = 30;

    /// <summary>Capture points awarded to the opposing faction for destroying a drop shuttle.</summary>
    [DataField]
    public int ShuttleDestroyScore = 15;

    /// <summary>A faction with zero living combatants for this long loses the war by elimination.</summary>
    [DataField]
    public float EliminationGraceSeconds = 180f;

    /// <summary>Score gap after which the losing faction receives comeback supply.</summary>
    [DataField]
    public float ComebackScoreGap = 30f;

    /// <summary>Loot probability multiplier for the losing faction while comeback is active.</summary>
    [DataField]
    public float ComebackLootMultiplier = 1.5f;

    /// <summary>When either faction reaches this score, trade zone C swaps a second time.</summary>
    [DataField]
    public float TradeZoneSwapSecondScoreThreshold = 75f;

    /// <summary>Interval between rotating war events (supply drop / diversion / reinforcement).</summary>
    [DataField]
    public float WarEventsIntervalSeconds = 360f;

    public int KillScoreNt;

    public int KillScoreTypan;

    public bool TradeZoneSwappedSecond;

    /// <summary>Winner was decided by eliminating all enemy combatants (end announcement variant).</summary>
    public bool WonByElimination;

    public TimeSpan? NtEliminatedSince;

    public TimeSpan? TypanEliminatedSince;

    public float WarEventsAccumulator;

    public int WarEventsIndex;

    /// <summary>Coin amounts allowed for a single war bet (paid ×2 on win, half back on stalemate).</summary>
    [DataField]
    public int[] BetAmounts = [5, 10, 25];

    /// <summary>One bet per player per war, placed during prep only (runtime).</summary>
    public Dictionary<NetUserId, (TypanWarSide Side, int Amount)> Bets = new();

    /// <summary>Total coins paid out as bet winnings at EndWar (runtime, used by round-end text).</summary>
    public int BetsTotalPayout;

    [DataField]
    public int StationSeparationTiles = 300;

    /// <summary>Max distance (tiles / meters) from a station anchor when repositioning trade posts for war.</summary>
    [DataField]
    public float TradePostMaxDistanceTiles = 500f;

    [DataField]
    public string WarParallax = "TypanWarParallax";

    /// <summary>
    /// Parallax used by planet-surface stations (MiniSilly, CorvaxPearl).
    /// When the NT map already uses this parallax, war layout keeps it and ensures map atmosphere
    /// instead of replacing it with <see cref="WarParallax"/>.
    /// </summary>
    [DataField]
    public string SurfaceParallax = "Water";

    /// <summary>Shuttle map spawned and docked to Typan when combat begins. Disabled for capture-zone war mode.</summary>
    [DataField]
    public ResPath DropShuttlePath;

    /// <summary>Shuttle map spawned and docked to NanoTrasen when combat begins. Disabled for capture-zone war mode.</summary>
    [DataField]
    public ResPath NtDropShuttlePath;

    /// <summary>Delay before docking a replacement drop shuttle after its console is lost.</summary>
    [DataField]
    public float DropShuttleRespawnDelaySeconds = 120f;

    /// <summary>Tracked NT reinforcement shuttle with a working console.</summary>
    public EntityUid? NtDropShuttle;

    /// <summary>Tracked Typan reinforcement shuttle with a working console.</summary>
    public EntityUid? TypanDropShuttle;

    public TimeSpan? NtDropShuttleRespawnAt;

    public TimeSpan? TypanDropShuttleRespawnAt;

    [DataField]
    public float WarEndWarningSeconds = 60f;

    [DataField]
    public float PrepCountdownSoundSeconds = 10f;

    [DataField]
    public string? NtStationGoalTitle;

    [DataField]
    public string? TypanStationGoalTitle;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? AnnouncementTime;

    [DataField]
    public bool AnnouncementSent;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? WarStartTime;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? WarEndTime;

    [DataField]
    public TypanWarPhase Phase = TypanWarPhase.Pending;

    [DataField]
    public EntityUid? NtStation;

    [DataField]
    public EntityUid? TypanStation;

    [DataField]
    public TypanWarWinner Winner = TypanWarWinner.None;

    /// <summary>Unique players who spawned on the NT station during this war (includes late join).</summary>
    public HashSet<NetUserId> NtJoinedUsers = new();

    /// <summary>Unique players who spawned on the Typan station during this war (includes late join).</summary>
    public HashSet<NetUserId> TypanJoinedUsers = new();

    [DataField]
    public bool WarMusicStarted;

    [DataField]
    public bool PrepCountdownPlayed;

    [DataField]
    public bool WarEndWarningPlayed;

    [DataField]
    public bool WarIntelEventSent;

    [DataField]
    public bool CaptureZonesActivated;

    [DataField]
    public bool CaptureZonesSpawned;

    public TimeSpan? CaptureZonesActivateAt;

    [DataField]
    public bool LayoutApplied;

    [DataField]
    public float PrepInsufficientCheckAccumulator;

    public EntityUid? WarMusicAudio;

    public CancellationTokenSource? WarMusicLoopCancel;

    /// <summary>Waiting for both NT and Typan stations to exist after map load.</summary>
    public bool AwaitingStations;

    public float AwaitingStationsAccumulator;
}
