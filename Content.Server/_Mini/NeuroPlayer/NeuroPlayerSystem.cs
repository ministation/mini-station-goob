// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._DV.Shuttles.Events;
using Content.Server._TT.StationHandleJob;
using Content.Server.Shuttles.Components;
using Content.Server.Chat.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Inventory;
using Content.Server.Radio;
using Content.Server.Spawners.Components;
using Content.Shared.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using System.Numerics;
using Content.Shared._Mini.MiniCCVars;
using Content.Shared._Mini.NeuroPlayer;
using Content.Server._Mini.TypanWar;
using Content.Shared._Mini.TypanWar;
using Content.Shared.Atmos.Components;
using Content.Shared.BarSign;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared._CorvaxGoob.TTS;
using Content.Shared.Chat;
using Content.Shared.CombatMode;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared._EinsteinEngines.Language.Components;
using Content.Shared._EinsteinEngines.Language.Systems;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.DoAfter;
// Content.Shared.DoAfter is both a namespace and the class inside it, so alias the type.
using DoAfterData = Content.Shared.DoAfter.DoAfter;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Ghost;
using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.SSDIndicator;
using Content.Shared.Radio.Components;
using Content.Shared.Interaction;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Turrets;
using Content.Shared.Weapons.Ranged.Components;
using Content.Server.NPC;
using Content.Shared.NPC.Systems;
using Content.Shared.StationRecords;
using Robust.Shared.Configuration;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Mini.NeuroPlayer;

/// <summary>
/// LLM-driven passenger bots ("neuro players"): spawn each round as passengers,
/// wander public areas and answer when addressed by name in local chat or common radio.
/// </summary>
public sealed partial class NeuroPlayerSystem : EntitySystem
{
    private const string CommonChannelId = "Common";
    private const int MaxReplyLength = 200;
    private const float DirectReplyDistance = 3f;

    /// <summary>A whisper is meant for the person next to you — bots only hear it point blank.</summary>
    private const float WhisperHearRadius = 2f;

    /// <summary>How many extra letters a declined name form may add ("Вася" -> "Васей").</summary>
    private const int MaxNameSuffixLength = 2;

    /// <summary>Shortest word that can be matched against a given name stem (keeps "вас" from pinging "Вася").</summary>
    private const int MinDeclinedNameLength = 4;

    /// <summary>A bot-to-bot reply is never answered with another bot-to-bot reply within this window.</summary>
    private const float BotChatterChainWindowSeconds = 20f;

    /// <summary>Strict composition mandated by the owner: 4 NT bots + CentComm maid + Typan chef.</summary>
    private const int CoreCompositionSize = 6;

    /// <summary>Bots launch this long after the round starts, so maps and stations can settle.</summary>
    private static readonly TimeSpan SpawnDelay = TimeSpan.FromSeconds(45);

    /// <summary>Interval between retries when the roster could not be spawned yet.</summary>
    private static readonly TimeSpan SpawnRetryDelay = TimeSpan.FromSeconds(5);

    /// <summary>How often lonely bots look for company (neuroplayer.seek_radius).</summary>
    private const float SeekCheckSeconds = 5f;

    /// <summary>A bot never marches further than this to find somebody to talk to.</summary>
    private const float MaxSeekRange = 120f;

    /// <summary>
    /// Extra "distance" charged to a player another bot is already walking to, so the whole crew
    /// does not converge on one person and pile up on their tile.
    /// </summary>
    private const float SeekCrowdPenalty = 15f;

    /// <summary>Personal offset (tiles) that keeps two bots off the same tile at a shared target.</summary>
    private const float SeekSpreadRadius = 1.5f;

    /// <summary>How far from a spawn point a bot may be placed to get a free tile of its own.</summary>
    private const int SpawnSpreadRadius = 4;

    /// <summary>
    /// When a bot that is already running away is hit again, only a threat that moved further than
    /// this (tiles) from the current escape point makes it pick a new route.
    /// </summary>
    private const float RerouteDistance = 6f;

    /// <summary>Bots notice fires within this many tiles.</summary>
    private const float FireAlertRadius = 12f;

    /// <summary>How far a brawl next to the bot is noticed.</summary>
    private const float FightAlertRadius = 6f;

    /// <summary>Total damage a body must already carry for a fresh scratch next to it to read as a fight.</summary>
    private const float FightDamage = 15f;

    /// <summary>How recently a body must have been touched to count as "being beaten right now".</summary>
    private static readonly TimeSpan FightAlertWindow = TimeSpan.FromSeconds(2);

    /// <summary>
    /// An armed hostile turret this close is walked away from, not fought. Its own guns reach
    /// 10 tiles (RangedRange), so backing off from here actually gets the bot out of fire.
    /// </summary>
    private const float TurretAlertRange = 12f;

    /*
     * Resisting being handled. A live passenger is not luggage: pulls, cuffs and chairs are fought
     * off with a roll. Failing the roll means the other player simply succeeds (so security can
     * still arrest a struggling passenger), and downed bots never resist — dragging a crit body to
     * medbay must stay possible.
     */

    /// <summary>Chance that a grab on a live bot simply fails.</summary>
    private const float PullResistChance = 0.5f;

    /// <summary>Chance that applying handcuffs is refused (the do-after never starts).</summary>
    private const float CuffResistChance = 0.5f;

    /// <summary>Chance that sitting the bot down on a chair/strap is refused.</summary>
    private const float BuckleResistChance = 0.6f;

    /// <summary>Chance per second that a dragged bot wriggles out of the grab (cuffed bots cannot).</summary>
    private const float BreakFreeChance = 0.3f;

    /// <summary>Chance per second that a buckled bot gets up (cuffed bots cannot).</summary>
    private const float StandUpChance = 0.35f;

    /// <summary>Fleeing from a grabber is shorter than fleeing from a fire.</summary>
    private const float ResistFleeSeconds = 8f;

    /// <summary>How often the "let me go" struggle is rolled.</summary>
    private const float ResistCheckSeconds = 1f;

    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly NpcFactionSystem _factions = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StationSpawningSystem _spawning = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInternalsSystem _internals = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly StationRecordsSystem _stationRecords = default!;
    [Dependency] private readonly TTStationHandleJobSystem _typanJobs = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedLanguageSystem _language = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly SharedCuffableSystem _cuffable = default!;

    private ISawmill _sawmill = default!;
    private readonly HttpClient _httpClient = new();
    private readonly List<EntityUid> _bots = new();

    /// <summary>Bot -> the point of interest it is currently visiting, so two bots do not share one.</summary>
    private readonly Dictionary<EntityUid, EntityUid> _poiClaims = new();

    private bool _enabled;
    private string _provider = NeuroProviderPresets.CustomId;
    private string _apiUrl = string.Empty;
    private string _apiKey = string.Empty;
    private string _model = string.Empty;
    private int _botCount;
    private float _hearRadius;
    private int _maxContext;
    private int _cooldownSeconds;
    private int _maxTokens;
    private float _temperature;
    private int _timeoutSeconds;
    private bool _thinkingEnabled;
    private float _directChance;
    private float _proactiveChance;
    private int _proactiveCooldownSeconds;
    private int _poiIntervalSeconds;
    private float _seekRadius;
    private int _botChatterCooldownSeconds;
    private float _botChatterChance;
    private string[] _blockedWords = [];
    private bool _blockAiTalk = true;
    private string _fallbackProvider = string.Empty;
    private int _requestsPerMinute;

    private readonly Queue<TimeSpan> _requestTimes = new();

    /// <summary>Cuffing do-afters already rolled for, so one attempt gets exactly one resist roll.</summary>
    private readonly HashSet<DoAfterData> _cuffAttempts = new();

    private int _requestsOk;
    private int _requestsFailed;
    private int _fallbacks;
    private int _requestsRateLimited;
    private int _blockedReplies;

    private float _dangerScanAccumulator;
    private float _seekAccumulator;
    private float _resistAccumulator;

    /// <summary>Round bookkeeping: bots are spawned once per round, with retries while they are missing.</summary>
    private bool _spawnedThisRound;
    private TimeSpan _roundStartTime;
    private TimeSpan _lastSpawnAttempt;

    /// <summary>Bots were despawned because a station war is running; they come back when it ends.</summary>
    private bool _warSuppressed;

    /// <summary>Only the first failed spawn attempt of the round is logged as a warning.</summary>
    private bool _spawnFailureLogged;

    private bool _configWarned;
    private string? _unknownProviderWarned;

    /// <summary>Word separators for name matching — substring hits inside words stopped counting.</summary>
    private static readonly char[] WordSeparators =
    [
        ' ', '\t', '\n', '\r', ',', '.', '!', '?', ';', ':', '"', '\'', '(', ')', '[', ']', '{', '}', '<', '>',
        '-', '—', '–', '…', '/', '\\', '|', '*', '«', '»', '“', '”', '„', '№',
    ];


    private static readonly ProtoId<JobPrototype>[] BasicJobPrototypes =
    [
        "CargoTechnician",
        "ServiceWorker",
        "Botanist",
        "Chef",
    ];

    private static readonly string[] FireCries = ["Ааа, пожар! Горим!", "Огонь! Бежим отсюда!", "Горит! Спасайся!"];
    private static readonly string[] PainCries = ["Ай! Больно!", "Ой! Помогите!", "Ах! Что происходит?!"];
    private static readonly string[] FightCries = ["Драка! Спасайся кто может!", "Стреляют! Прячься!", "Ой-ой, отходим от греха!"];
    private static readonly string[] TurretCries = ["Там турель! Я лучше обойду.", "Ой, турель, назад!", "Туда не пойду, там стреляют!"];

    private static readonly string[] TouchCries =
    [
        "Эй, руки убрал!",
        "Чего надо?",
        "М? Что-то нужно?",
        "Осторожнее, я не подставка.",
    ];
    private static readonly string[] DecompressionCries =
        ["Воздух уходит! Кислород!", "Разгерметизация! Маску, МАСКУ!", "Шлюзы! Воздух кончается!"];
    private static readonly string[] EvacCries =
        ["Бежим на эвак!", "Эвак пристыковался, быстрее!", "За мной на эвак, шевелитесь!", "Погнали на эвак, пока не улетел!"];

    private static readonly string[] ResistCries =
    [
        "Отстаньте от меня!",
        "Эй, руки убрал! Я не груз!",
        "Пустите! Я вам не багаж!",
        "Отстаньте, я спешу!",
    ];

    private static readonly string[] DraggedCries =
    [
        "Отстаньте! Куда вы меня тащите?!",
        "Пустите! Я сам дойду!",
        "Отстаньте, я не чемодан!",
    ];

    private static readonly string[] BreakFreeCries =
    [
        "Отстаньте! Всё, хватит меня таскать!",
        "Пустите! Я вырвался!",
        "Отстаньте от меня, я сказал!",
    ];

    private static readonly string[] CuffResistCries =
    [
        "Отстаньте! Наручники?! Не дамся!",
        "Пустите! Я ничего не сделал!",
        "Отстаньте от меня! Помогите кто-нибудь!",
    ];

    private static readonly string[] BuckleResistCries =
    [
        "Отстаньте! Я не хочу сидеть!",
        "Пустите! У меня дела!",
        "Отстаньте, я не арестант!",
    ];

    private static readonly string[] SitDownCries =
    [
        "Отстаньте! Я тут не останусь!",
        "Пустите, я не собираюсь сидеть!",
    ];

    /// <summary>Mini: fleeing time after a shove, short enough to resume normal life quickly.</summary>
    private const float ShoveFleeSeconds = 8f;

    /// <summary>Mini: reactions to being shoved (DisarmedEvent).</summary>
    private static readonly string[] ShoveCries =
    [
        "Эй, не толкайся!",
        "Руки убери!",
        "Ты чего толкаешься?",
        "Отстаньте, я не мешок!",
    ];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NeuroPlayerComponent, InteractHandEvent>(OnBotInteracted);

        _sawmill = Logger.GetSawmill("neuroplayer");

        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerEnabled, v => _enabled = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerProvider, v => _provider = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerFallbackProvider, v => _fallbackProvider = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerRequestsPerMinute, v => _requestsPerMinute = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerApiUrl, v => _apiUrl = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerApiKey, v => _apiKey = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerModel, v => _model = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerBotCount, v => _botCount = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerHearRadius, v => _hearRadius = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerMaxContext, v => _maxContext = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerCooldownSeconds, v => _cooldownSeconds = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerMaxTokens, v => _maxTokens = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerTemperature, v => _temperature = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerTimeoutSeconds, v => _timeoutSeconds = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerThinkingEnabled, v => _thinkingEnabled = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerDirectChance, v => _directChance = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerProactiveChance, v => _proactiveChance = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerProactiveCooldownSeconds, v => _proactiveCooldownSeconds = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerPoiIntervalSeconds, v => _poiIntervalSeconds = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerSeekRadius, v => _seekRadius = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerBotChatterCooldownSeconds, v => _botChatterCooldownSeconds = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerBotChatterChance, v => _botChatterChance = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerBlockAiTalk, v => _blockAiTalk = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerBlockedWords, v => _blockedWords = ParseWordList(v), true);

        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        // EntitySpokeEvent is raised broadcast — this handler sees ALL in-character speech;
        // radio-delivered messages are handled separately via RadioReceiveEvent to avoid doubles.
        SubscribeLocalEvent<EntitySpokeEvent>(OnGlobalSpoke);
        SubscribeLocalEvent<NeuroPlayerComponent, RadioReceiveEvent>(OnRadioReceive);
        SubscribeLocalEvent<NeuroPlayerComponent, MobStateChangedEvent>(OnBotMobStateChanged);
        // Resisting being handled: grabs, handcuffs and chairs.
        SubscribeLocalEvent<NeuroPlayerComponent, PullAttemptEvent>(OnBotPullAttempt);
        SubscribeLocalEvent<NeuroPlayerComponent, PullStartedMessage>(OnBotPullStarted);
        SubscribeLocalEvent<NeuroPlayerComponent, BuckleAttemptEvent>(OnBotBuckleAttempt);
        SubscribeLocalEvent<NeuroPlayerComponent, BuckledEvent>(OnBotBuckled);
        // Shoves (WWDP shove rework) raise DisarmedEvent on the target: a bot answers with a cry and
        // breaks away from the shover instead of silently eating it.
        SubscribeLocalEvent<NeuroPlayerComponent, DisarmedEvent>(OnBotDisarmed);
        // The cuff do-after is directed at the handcuffs, so the bot's component never sees the
        // attempt itself — listen on the item and look at who is being cuffed.
        SubscribeLocalEvent<HandcuffComponent, DoAfterAttemptEvent<AddCuffDoAfterEvent>>(OnHandcuffAttempt);
        // Mini: DamageableSystem raises DamageChangedEvent with RaiseLocalEvent(victim, ...), i.e.
        // directed at the damaged entity and NOT broadcast, so the old broadcast subscription
        // (SubscribeLocalEvent<DamageChangedEvent>) never fired and bots ignored every hit. The fix has
        // to hang off our own component: the engine allows exactly one directed subscription per
        // component+event pair per game, and DailyQuestSystem already owns
        // DamageableComponent+DamageChangedEvent — a second one crashes the server at startup with
        // "Duplicate Subscriptions".
        SubscribeLocalEvent<NeuroPlayerComponent, DamageChangedEvent>(OnBotDamaged);
        SubscribeLocalEvent<TypanWarStartedEvent>(OnWarStarted);
        SubscribeLocalEvent<EvacShuttleDockedEvent>(OnEvacDocked);

        // Admin-help assistant (partial file): answers ahelp while no admins are online.
        InitializeAHelp();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _httpClient.Dispose();
    }

    private void OnRoundStarted(RoundStartedEvent ev)
    {
        _roundStartTime = _timing.CurTime;
        _spawnedThisRound = false;
        _warSuppressed = false;
        _spawnFailureLogged = false;

        if (!_enabled || _botCount <= 0)
            return;

        if (!_prototypes.EnumeratePrototypes<NeuroPersonaPrototype>().Any())
        {
            _sawmill.Warning("Neuro players enabled but no neuroPersona prototypes found.");
            return;
        }

        // Mini: spawning is driven by MaintainBots() instead of a single 45-second timer, so a
        // failed attempt (no station yet, war running, cvar enabled mid-round) is retried, and
        // toggling neuroplayer.enabled during a round actually works.
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _bots.Clear();
        _poiClaims.Clear();
        _spawnedThisRound = false;
        _warSuppressed = false;
        _roundStartTime = TimeSpan.Zero;

        // Ahelp assistant state lives in the partial file; the event handler is shared because a
        // system may only subscribe to an event once.
        ResetAHelpState();
    }

    /// <summary>
    /// Mini: a downed bot stops living its life. Previously a corpse kept hearing chat, answering
    /// (the reply was then dropped by the speech action blocker — an already paid LLM request),
    /// crying and being scanned for danger for the rest of the round.
    /// </summary>
    private void OnBotMobStateChanged(EntityUid uid, NeuroPlayerComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
        {
            // Revived (defib, cloning): back to wandering. An evac assignment is restored by RestoreIdle.
            comp.PoiAccumulator = 0f;
            RestoreIdle(uid);
            return;
        }

        comp.DangerUntil = null;
        comp.HostileUntil = null;
        comp.VisitUntil = null;
        comp.PoiAccumulator = 0f;
    }

    /// <summary>Whether the bot is alive and can act (corpses and critters stay silent).</summary>
    private bool IsBotUp(EntityUid bot)
    {
        return Exists(bot) && !TerminatingOrDeleted(bot) && _mobState.IsAlive(bot);
    }

    /// <summary>Whether somebody already put handcuffs on the bot.</summary>
    private bool IsBotCuffed(EntityUid bot)
    {
        return TryComp<CuffableComponent>(bot, out var cuffable) && _cuffable.IsCuffed((bot, cuffable));
    }

    /// <summary>Whether a bot can fight back at all right now.</summary>
    private bool CanResist(EntityUid bot, NeuroPlayerComponent comp)
    {
        return IsBotUp(bot) && !IsMuted(comp) && !IsBotCuffed(bot);
    }

    /// <summary>
    /// Grab attempt: a live passenger tears the hand away with a coin flip. Downed bots never
    /// resist, so carrying a crit body to medbay still works.
    /// </summary>
    private void OnBotPullAttempt(EntityUid uid, NeuroPlayerComponent comp, PullAttemptEvent args)
    {
        if (!_enabled || args.Cancelled || args.PullerUid == uid)
            return;

        // Bots never grab each other, but do not resist a bot either.
        if (HasComp<NeuroPlayerComponent>(args.PullerUid) || !CanResist(uid, comp))
            return;

        if (!_random.Prob(PullResistChance))
            return;

        args.Cancelled = true;
        Resist(uid, comp, args.PullerUid, ResistCries, flee: true);
    }

    /// <summary>Grab succeeded anyway: complain the whole time the bot is being dragged.</summary>
    private void OnBotPullStarted(EntityUid uid, NeuroPlayerComponent comp, PullStartedMessage args)
    {
        if (!_enabled || args.PullerUid == uid || HasComp<NeuroPlayerComponent>(args.PullerUid))
            return;

        if (!CanResist(uid, comp))
            return;

        CryResist(uid, comp, DraggedCries);
    }

    /// <summary>
    /// Handcuffs: refuse the attempt with a coin flip. The attempt event fires when the do-after
    /// starts and again when it finishes (Mini raised its frequency for exactly this hook), so the
    /// roll is remembered per do-after — otherwise the bot would also slip out at the last moment.
    /// </summary>
    private void OnHandcuffAttempt(Entity<HandcuffComponent> ent, ref DoAfterAttemptEvent<AddCuffDoAfterEvent> args)
    {
        if (!_enabled || args.Cancelled)
            return;

        // Second raise for the same do-after: the decision was already made.
        if (!_cuffAttempts.Add(args.DoAfter))
            return;

        PruneCuffAttempts();

        var user = args.DoAfter.Args.User;
        if (args.DoAfter.Args.Target is not { } bot || bot == user)
            return;

        if (!TryComp<NeuroPlayerComponent>(bot, out var comp) || !CanResist(bot, comp))
            return;

        if (!_random.Prob(CuffResistChance))
            return;

        args.Cancel();
        Resist(bot, comp, user, CuffResistCries, flee: true);
    }

    private void PruneCuffAttempts()
    {
        if (_cuffAttempts.Count < 128)
            return;

        _cuffAttempts.RemoveWhere(doAfter => doAfter.Completed || doAfter.Cancelled);
    }

    /// <summary>Sitting the bot down: refuse with a chance, otherwise complain from the chair.</summary>
    private void OnBotBuckleAttempt(EntityUid uid, NeuroPlayerComponent comp, ref BuckleAttemptEvent args)
    {
        if (!_enabled || args.Cancelled || args.User is not { } user || user == uid)
            return;

        if (HasComp<NeuroPlayerComponent>(user) || !CanResist(uid, comp))
            return;

        if (!_random.Prob(BuckleResistChance))
            return;

        args.Cancelled = true;
        Resist(uid, comp, user, BuckleResistCries, flee: true);
    }

    private void OnBotBuckled(EntityUid uid, NeuroPlayerComponent comp, ref BuckledEvent args)
    {
        if (!_enabled || !CanResist(uid, comp))
            return;

        CryResist(uid, comp, SitDownCries);
    }

    /// <summary>
    /// Mini: being shoved is a physical attack, not a no-op. The bot cries out and runs from the
    /// shover; downed, cuffed and muted bots stay silent (a body is not a person).
    /// </summary>
    private void OnBotDisarmed(EntityUid uid, NeuroPlayerComponent comp, ref DisarmedEvent args)
    {
        if (!_enabled || !CanResist(uid, comp))
            return;

        var source = args.Source;
        if (source == uid || !Exists(source))
            return;

        Cry(uid, comp, ShoveCries);
        TriggerDanger(uid, comp, _transform.GetWorldPosition(source), null, cry: false, seconds: ShoveFleeSeconds);
    }

    /// <summary>
    /// Ongoing struggle, rolled once a second: a dragged bot wriggles out of a grab and a buckled
    /// bot gets up. Handcuffs end both — that is what they are for.
    /// </summary>
    private void ResistBeingHeld()
    {
        foreach (var bot in _bots)
        {
            if (!TryComp<NeuroPlayerComponent>(bot, out var comp) || !CanResist(bot, comp))
                continue;

            if (TryComp<PullableComponent>(bot, out var pullable) &&
                pullable.Puller is { } puller && Exists(puller) && !HasComp<NeuroPlayerComponent>(puller))
            {
                // user: bot keeps the engine's own escape rules (grab stage, escape cooldown, stamina).
                if (_random.Prob(BreakFreeChance) && _pulling.TryStopPull(bot, pullable, user: bot))
                {
                    Resist(bot, comp, puller, BreakFreeCries, flee: true);
                    continue;
                }

                CryResist(bot, comp, DraggedCries);
                continue;
            }

            if (TryComp<BuckleComponent>(bot, out var buckleComp) && buckleComp.Buckled &&
                _random.Prob(StandUpChance))
            {
                _buckle.TryUnbuckle((bot, buckleComp), bot, popup: false);
                CryResist(bot, comp, SitDownCries);
            }
        }
    }

    /// <summary>Common reaction to being grabbed, cuffed or seated: shout and try to get away.</summary>
    private void Resist(EntityUid bot, NeuroPlayerComponent comp, EntityUid user, string[] cries, bool flee)
    {
        CryResist(bot, comp, cries);

        if (!flee || !user.IsValid() || !Exists(user) || HasComp<NeuroPlayerComponent>(user) || !IsBotUp(bot))
            return;

        TriggerDanger(bot, comp, _transform.GetWorldPosition(user), null, cry: false, seconds: ResistFleeSeconds);
    }

    /// <summary>"Отстаньте!" — canned line with its own, much shorter cooldown than a fire cry.</summary>
    private void CryResist(EntityUid bot, NeuroPlayerComponent comp, string[] phrases)
    {
        if (_timing.CurTime < comp.NextResistCry || !IsBotUp(bot) || IsMuted(comp))
            return;

        comp.NextResistCry = _timing.CurTime + TimeSpan.FromSeconds(6);
        SayCanned(bot, _random.Pick(phrases));
    }

    private void SayCanned(EntityUid bot, string phrase)
    {
        _chat.TrySendInGameICMessage(bot, phrase, InGameICChatType.Speak, hideChat: false, hideLog: true);
        _adminLog.Add(LogType.Chat, LogImpact.Low, $"Say from neuro player {Name(bot)}: {phrase}");
    }

    /// <summary>Admin mute (neurobots mute): permanently or until the timed mute runs out.</summary>
    private bool IsMuted(NeuroPlayerComponent comp)
    {
        return comp.Muted || (comp.MutedUntil != TimeSpan.Zero && _timing.CurTime < comp.MutedUntil);
    }

    private static string[] ParseWordList(string raw)
    {
        return raw
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void OnWarStarted(TypanWarStartedEvent ev)
    {
        // Station war: neuro passengers are hidden for the duration, and brought back afterwards.
        _warSuppressed = true;
        DespawnBots();
    }

    private void OnEvacDocked(EvacShuttleDockedEvent ev)
    {
        if (!_enabled)
            return;

        // Gather passengers on the docked evac shuttle; the first bot leads the group aloud.
        EntityUid? shuttleUid = null;
        var shuttleMap = MapId.Nullspace;

        var query = EntityQueryEnumerator<EmergencyShuttleComponent, TransformComponent>();
        while (query.MoveNext(out var shuttle, out _, out var xform))
        {
            if (!xform.GridUid.HasValue)
                continue;

            shuttleUid = shuttle;
            shuttleMap = xform.MapID;
            break;
        }

        if (shuttleUid == null)
            return;

        var led = false;
        foreach (var bot in _bots)
        {
            if (!IsBotUp(bot) || !TryComp<NeuroPlayerComponent>(bot, out var comp))
                continue;

            if (Transform(bot).MapID != shuttleMap)
                continue; // Typan chef / CentComm maid stay on their maps

            // Mini: remember the shuttle instead of a one-off point, so a bot that had to run from
            // a fight walks back aboard instead of wandering off the pad.
            comp.EvacShuttle = shuttleUid;
            comp.EvacOffset = new Vector2(_random.NextFloat(-3f, 3f), _random.NextFloat(-3f, 3f));

            if (!TryGetEvacPoint(bot, comp, out var point) || !TryComp<HTNComponent>(bot, out var htn))
                continue;

            htn.Blackboard.SetValue("NeuroPoint", point);
            SetRootTask(htn, "NeuroVisitCompound");
            comp.VisitUntil = _timing.CurTime + TimeSpan.FromSeconds(420);

            if (!led)
            {
                led = true;
                Cry(bot, comp, EvacCries);
            }
        }
    }

    /// <summary>Point on the docked evac shuttle for this bot, while the shuttle is still on its map.</summary>
    private bool TryGetEvacPoint(EntityUid bot, NeuroPlayerComponent comp, out EntityCoordinates point)
    {
        point = default;

        if (comp.EvacShuttle is not { } shuttle || !Exists(shuttle) || TerminatingOrDeleted(shuttle))
            return false;

        var shuttleXform = Transform(shuttle);
        if (shuttleXform.MapID != Transform(bot).MapID)
            return false;

        point = shuttleXform.Coordinates.Offset(comp.EvacOffset);
        return true;
    }

    public void SpawnBots()
    {
        if (!_enabled)
            return;

        // Mini: repeated spawns must not stack bots. Re-creating bots in the same
        // tick (delete+create churn) trips the client "state is missing metadata"
        // race, so skip while the previous batch is still alive.
        _bots.RemoveAll(bot => !Exists(bot));
        if (_bots.Count > 0)
        {
            _sawmill.Info("Neuro players: already spawned, skipping.");
            return;
        }

        // Station war: neuro passengers stay out of the conflict entirely.
        if (TypanStationWarRuleSystem.IsModeActive)
        {
            _sawmill.Info("Neuro players skipped: station war is active.");
            return;
        }

        var personas = _prototypes.EnumeratePrototypes<NeuroPersonaPrototype>()
            .OrderBy(p => p.Order)
            .ToList();
        if (personas.Count == 0)
        {
            _sawmill.Warning("Neuro players enabled but no neuroPersona prototypes found.");
            return;
        }

        var station = FindStation();
        if (!station.HasValue)
        {
            // No playable NT station in this round yet: passengers wait for it instead of settling
            // down on CentComm or on the second station (MaintainBots retries in a few seconds).
            LogSpawnFailure("no playable NT station found, waiting for it");
            return;
        }

        var points = FindSpawnPoints(station.Value);
        if (points.Count == 0)
        {
            LogSpawnFailure(
                $"no late-join spawn points on {Name(station.Value)} " +
                $"({MetaData(station.Value).EntityPrototype?.ID ?? "no prototype"})");
            return;
        }

        // Mini: strict composition requested by the owner:
        // 4 bots on the NT station (passenger, janitor, cargo tech, clown),
        // 1 maid on CentComm, 1 typan employee on Typan.
        // neuroplayer.bot_count may ask for more than that core; the surplus shows up as extra
        // passengers from personas the core does not use.
        var spawned = 0;
        var corePersonaIds = new HashSet<string>
        {
            "NeuroVasya", "NeuroJanitor", "NeuroCargoTech", "NeuroClown", "NeuroCCMaid", "NeuroTypanChef",
        };

        var usedPersonaIds = new HashSet<string>();
        var mainStaff = new (string PersonaId, string Job, string? RootTask)[]
        {
            ("NeuroVasya", "Passenger", null),
            ("NeuroJanitor", "Janitor", "CleanbotCompound"),
            ("NeuroCargoTech", "CargoTechnician", null),
            ("NeuroClown", "Clown", null),
        };

        // Mini: mini maps ship exactly one late-join marker, so picking a random point per bot put
        // the whole crew on the same tile — they shoved each other, could not path out and just
        // milled around in one spot. Every bot gets a free tile of its own instead.
        var slots = DistributeSpawnPoints(points, Math.Min(Math.Max(mainStaff.Length, _botCount), 32));
        if (slots.Count < mainStaff.Length)
        {
            // Every late-join marker is off-grid (map-parented): spawning here would drop the crew
            // into open space. Wait for a marker that actually has a floor under it.
            LogSpawnFailure($"late-join markers on {Name(station.Value)} are not on a grid");
            return;
        }

        var slot = 0;

        foreach (var (personaId, job, rootTask) in mainStaff)
        {
            var persona = personas.FirstOrDefault(p => p.ID == personaId) ?? personas[0];
            var coords = slots[slot++];
            if (TrySpawnBot(persona, coords, station.Value, forcedJob: job,
                    rootTask: rootTask,
                    onSpawned: rootTask == "CleanbotCompound" ? mob => GiveJanitorMop(mob) : null))
            {
                spawned++;
                usedPersonaIds.Add(persona.ID);
            }
        }

        // CentComm gets its maid.
        var ccStation = FindFactionStation(centcomm: true);
        if (ccStation != null)
        {
            var persona = personas.FirstOrDefault(p => p.ID == "NeuroCCMaid") ?? personas[0];
            var ccPoints = FindSpawnPoints(ccStation.Value);
            if (ccPoints.Count > 0 && TrySpawnBot(persona, _random.Pick(ccPoints), ccStation.Value,
                    forcedJob: "Passenger", onSpawned: GiveMaidOutfit))
            {
                spawned++;
                usedPersonaIds.Add(persona.ID);
            }
        }

        // Typan gets its own employee.
        var typanStation = FindFactionStation(typan: true);
        if (typanStation != null)
        {
            var persona = personas.FirstOrDefault(p => p.ID == "NeuroTypanChef") ?? personas[0];
            var typanPoints = FindSpawnPoints(typanStation.Value);
            if (typanPoints.Count > 0 && TrySpawnBot(persona, _random.Pick(typanPoints), typanStation.Value,
                    forcedJob: "TypanChef"))
            {
                spawned++;
                usedPersonaIds.Add(persona.ID);
            }
        }

        // Surplus: neuroplayer.bot_count is now a real number instead of a kill switch. Values below
        // the core size keep the mandated composition, higher values add random NT passengers.
        var surplus = Math.Max(0, _botCount - CoreCompositionSize);
        var sparePersonas = personas
            .Where(p => !corePersonaIds.Contains(p.ID) && !usedPersonaIds.Contains(p.ID))
            .Take(surplus)
            .ToList();

        foreach (var persona in sparePersonas)
        {
            if (slot >= slots.Count)
                break;

            if (!TrySpawnBot(persona, slots[slot++], station.Value))
                continue;

            spawned++;
            usedPersonaIds.Add(persona.ID);
        }

        if (surplus > sparePersonas.Count)
        {
            _sawmill.Warning(
                $"Neuro players: neuroplayer.bot_count={_botCount} asks for {surplus} extra bots, " +
                $"but only {sparePersonas.Count} unused personas are available " +
                $"(core composition is {CoreCompositionSize}).");
        }

        if (spawned > 0)
        {
            _spawnedThisRound = true;
            _spawnFailureLogged = false;
        }

        _sawmill.Info(
            $"Neuro players: spawned {spawned} bots on {Name(station.Value)} " +
            $"({MetaData(station.Value).EntityPrototype?.ID ?? "no prototype"}, bot_count={_botCount}).");
    }

    /// <summary>
    /// A failed spawn attempt is retried every few seconds, so only the first one of the round is a
    /// warning and the rest go to the debug log.
    /// </summary>
    private void LogSpawnFailure(string reason)
    {
        if (_spawnFailureLogged)
        {
            _sawmill.Debug($"Neuro players: spawn attempt failed: {reason}.");
            return;
        }

        _spawnFailureLogged = true;
        _sawmill.Warning($"Neuro players: spawn attempt failed: {reason}. Retrying in a few seconds.");
    }

    private void GiveJanitorMop(EntityUid mob)
    {
        try
        {
            var mop = Spawn("MopItem", Transform(mob).Coordinates);
            _hands.TryPickupAnyHand(mob, mop, checkActionBlocker: false);
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Neuro janitor mop failed: {e.Message}");
        }
    }

    private void GiveMaidOutfit(EntityUid mob)
    {
        try
        {
            var uniform = Spawn("ClothingUniformJumpskirtJanimaid", Transform(mob).Coordinates);
            _inventory.TryEquip(mob, uniform, "jumpsuit", silent: true, force: true);

            var pda = Spawn("CommandMaidPDA", Transform(mob).Coordinates);
            _inventory.TryEquip(mob, pda, "id", silent: true, force: true);
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Neuro maid outfit failed: {e.Message}");
        }
    }

    private EntityUid? FindFactionStation(bool typan = false, bool centcomm = false)
    {
        var query = EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out var uid, out var data))
        {
            if (data.Grids.Count == 0)
                continue;

            if (typan && _typanJobs.IsTypanFactionStation(uid))
                return uid;

            if (centcomm && _typanJobs.IsCentCommFactionStation(uid))
                return uid;
        }

        return null;
    }

    private bool TrySpawnBot(
        NeuroPersonaPrototype persona,
        EntityCoordinates coords,
        EntityUid station,
        ProtoId<JobPrototype>? forcedJob = null,
        string? rootTask = null,
        Action<EntityUid>? onSpawned = null)
    {
        // Sometimes a basic profession look (cargo tech, service worker...) — purely gear,
        // player job slots are never consumed. Security stays player-only.
        // The persona's own profession is the default, so "Грузчик" actually looks like a cargo tech.
        var job = forcedJob
            ?? (_random.Prob(0.4f)
                ? _random.Pick(BasicJobPrototypes)
                : persona.Job);

        var profile = HumanoidCharacterProfile.RandomWithSpecies("Human");

        // Mini: body and voice follow the persona card instead of a random roll.
        if (persona.Sex is { } personaSex)
            profile = profile.WithSex(personaSex);
        if (persona.Age is { } personaAge)
            profile = profile.WithAge(personaAge);

        var mob = _spawning.SpawnPlayerMob(coords, job, profile, station);

        var comp = EnsureComp<NeuroPlayerComponent>(mob);
        comp.PersonaId = persona.ID;

        // Mini: shared marker so shared systems (the mind examine status above all) know this body
        // is puppeteered by an LLM rather than left behind by a player.
        EnsureComp<NeuroPlayerMarkerComponent>(mob);

        // Mini: the humanoid prototype ships an SSD indicator that defaults to "SSD". A neuro player
        // has no session by design, so every bot read as a disconnected sleeper: examine said
        // "спит уже N минут" (FTL comp-ssd-person-examined), the SSD icon hung over its head and the
        // ghost teleporter filed a live crew member under "left the body". The body is neither
        // asleep nor abandoned, so drop the indicator instead of faking a session for it.
        RemComp<SSDIndicatorComponent>(mob);

        var transmitter = EnsureComp<IntrinsicRadioTransmitterComponent>(mob);
        transmitter.Channels.Add(CommonChannelId);
        var receiver = EnsureComp<ActiveRadioComponent>(mob);
        receiver.Channels.Add(CommonChannelId);

        var htn = EnsureComp<HTNComponent>(mob);
        htn.RootTask = new HTNCompoundTask { Task = rootTask ?? "IdleCompound" };

        // Mini: двери бот открывает как член экипажа — своим ID из ПДА, который получает вместе со
        // стартовым снаряжением профессии. Bump-open дверь открывается самим столкновением, click-open
        // (firelock, ставни, windoor) — взаимодействием, а дверь без доступа для него тупик: он
        // перестраивает маршрут вместо того, чтобы pry'ить её. NavInteract нужен, чтобы планировщик
        // вообще считал click-open двери проходимыми: без флага GetTileCost возвращает для них 0.
        htn.Blackboard.SetValue(NPCBlackboard.NavCrewDoors, true);
        htn.Blackboard.SetValue(NPCBlackboard.NavInteract, true);
        // Mini: reach of the "run to any reachable tile" fallback branch of NeuroFleeCompound.
        htn.Blackboard.SetValue("FleeRange", 12f);
        _factions.AddFactions(mob, new HashSet<ProtoId<NpcFactionPrototype>> { "NanoTrasen" });

        // Mini: machines (station AI turrets first of all) are not opponents a passenger can
        // fight — bots used to open fire on a turret the moment it came in range.
        IgnoreMachines(mob);

        // Random voice matching the bot's sex — neuro passengers speak out loud via TTS.
        var voicePool = _prototypes.EnumeratePrototypes<TTSVoicePrototype>()
            .Where(v => v.Sex == profile.Sex)
            .ToList();
        if (voicePool.Count > 0)
        {
            var tts = EnsureComp<TTSComponent>(mob);
            tts.VoicePrototypeId = _random.Pick(voicePool).ID;
            tts.Pitch = _random.NextFloat(0.95f, 1.05f);
        }

        var mind = _mind.CreateMind(null, persona.Name);
        _mind.TransferTo(mind, mob);
        _mind.SetGhostOnShutdown(mob, false);

        // Mini: the profession lives in the mind role, not only in the station record. The ghost
        // teleporter (GetPlayerWarps -> _jobs.MindTryGetJob) and other job interfaces read exactly
        // that, which is why bots showed up as "unknown" with no job icon. Player job slots are not
        // consumed: slots are handed out by StationJobsSystem.TryAssignJob, which is never called here.
        _roles.MindAddJobRole(mind, silent: true, jobPrototype: job);

        if (TryComp<StationRecordsComponent>(station, out var records))
        {
            try
            {
                _stationRecords.CreateGeneralRecord(
                    station, mob, persona.Name, profile.Age, profile.Species, profile.Sex,
                    job, null, null, profile, records);
            }
            catch (Exception e)
            {
                _sawmill.Warning($"Neuro player {persona.Name}: crew manifest record failed: {e.Message}");
            }
        }

        onSpawned?.Invoke(mob);

        // Mini: the HTN component is added at runtime, so MapInitEvent — the only thing that calls
        // NPCSystem.WakeNPC — has already fired for this entity and never reaches it again. NPCs
        // without ActiveNPCComponent are invisible to HTNSystem.UpdateNPC, and
        // NPCProximitySleepSystem skips bodies that hold a mind, so nothing else woke the bot
        // either: every plan (idling, fleeing, visiting) stayed dead code and the bots stood still
        // forever. Wake the finished bot explicitly.
        _npc.WakeNPC(mob, htn);

        _bots.Add(mob);
        return true;
    }

    public void DespawnBots()
    {
        foreach (var bot in _bots)
        {
            if (Exists(bot))
                QueueDel(bot);
        }

        _bots.Clear();
        _poiClaims.Clear();
    }

    /// <summary>
    /// The playable NT station the passenger crew belongs to, or null when the round has none.
    /// </summary>
    /// <remarks>
    /// Mini: CentComm (<c>NanotrasenCentralCommand</c>), Typan and Аспид (<c>StandardTypanStation</c>)
    /// are stations with their own cryo pods and job spawners, and they each have exactly one neuro
    /// player of their own (see <see cref="FindFactionStation"/>). Taking the first station the query
    /// happens to return put the whole passenger crew on CentComm or on the second station instead —
    /// the component store order is not creation order, so this is a lottery, not a rule.
    /// Those stations are not a fallback either: passengers wait for the NT station, they never
    /// spawn on a faction station.
    /// </remarks>
    private EntityUid? FindStation()
    {
        var candidates = new List<EntityUid>();

        var query = EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out var uid, out var data))
        {
            if (data.Grids.Count == 0)
                continue;

            if (_typanJobs.IsWarNanotrasenStation(uid))
                candidates.Add(uid);
        }

        if (candidates.Count == 0)
            return null;

        if (candidates.Count == 1)
            return candidates[0];

        // More than one NT station (main map + дополнительная карта): pick one that can actually
        // host a late-join passenger.
        foreach (var candidate in candidates)
        {
            if (FindSpawnPoints(candidate).Count > 0)
                return candidate;
        }

        return candidates[0];
    }

    private List<EntityCoordinates> FindSpawnPoints(EntityUid station)
    {
        var points = new List<EntityCoordinates>();

        var query = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var point, out var xform))
        {
            if (point.SpawnType != SpawnPointType.LateJoin)
                continue;

            if (_station.GetOwningStation(uid, xform) != station)
                continue;

            points.Add(new EntityCoordinates(uid, 0, 0));
        }

        if (points.Count > 0)
            return points;

        // Mini maps have no bare LateJoin markers — their late-join flow goes through
        // cryo sleep units (ContainerSpawnPoint with LateJoin type). Spawn next to those.
        var cryoQuery = EntityQueryEnumerator<ContainerSpawnPointComponent, TransformComponent>();
        while (cryoQuery.MoveNext(out var uid, out var point, out var xform))
        {
            if (point.SpawnType != SpawnPointType.LateJoin)
                continue;

            if (_station.GetOwningStation(uid, xform) != station)
                continue;

            points.Add(new EntityCoordinates(uid, 0, 0));
        }

        if (points.Count > 0)
            return points;

        // Last resort: job spawn points of the station (always present, free tiles).
        var jobQuery = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (jobQuery.MoveNext(out var uid, out var point, out var xform))
        {
            if (point.SpawnType != SpawnPointType.Job)
                continue;

            if (_station.GetOwningStation(uid, xform) != station)
                continue;

            points.Add(new EntityCoordinates(uid, 0, 0));
        }

        return points;
    }

    /// <summary>
    /// Mini: hands every bot a tile of its own. Mini maps ship exactly one late-join marker, so
    /// picking a random point per bot dropped the whole crew on the same tile — they shoved each
    /// other, could not path out and just milled around in one place.
    /// </summary>
    private List<EntityCoordinates> DistributeSpawnPoints(List<EntityCoordinates> points, int count)
    {
        var result = new List<EntityCoordinates>(count);
        if (points.Count == 0 || count <= 0)
            return result;

        // Mini: a marker that is not on a grid (parented to the map entity) has no floor under it —
        // the anchor coordinates are then a raw map position, i.e. open space, and a bot spawned
        // there floats in the void. Only grid-backed markers can host a bot.
        var anchors = new List<EntityCoordinates>(points.Count);
        foreach (var point in points)
        {
            if (Transform(point.EntityId).GridUid != null)
                anchors.Add(point);
        }

        if (anchors.Count == 0)
            return result;

        _random.Shuffle(anchors);

        var used = new HashSet<Vector2i>();

        for (var i = 0; i < count; i++)
            result.Add(FindFreeSpawnTile(anchors[i % anchors.Count], used));

        return result;
    }

    /// <summary>
    /// The anchor tile itself, otherwise the nearest free tile around it (spiralling out to
    /// <see cref="SpawnSpreadRadius"/>). A tile counts as taken when another bot already got it,
    /// when it has no floor (space) or when an anchored entity (wall, machine, furniture) sits on it.
    /// </summary>
    private EntityCoordinates FindFreeSpawnTile(EntityCoordinates anchor, HashSet<Vector2i> used)
    {
        if (Transform(anchor.EntityId).GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var mapGrid))
            return anchor;

        var center = _map.CoordinatesToTile(grid, mapGrid, anchor);

        for (var radius = 0; radius <= SpawnSpreadRadius; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    // Only the outer ring: inner tiles were already tried at smaller radii.
                    if (radius > 0 && Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                        continue;

                    var tile = center + new Vector2i(dx, dy);
                    if (!used.Add(tile) || !IsTileFreeForSpawn(grid, mapGrid, tile))
                        continue;

                    return _map.GridTileToLocal(grid, mapGrid, tile);
                }
            }
        }

        // Solid construction all around: the marker tile still beats not spawning at all.
        return anchor;
    }

    /// <summary>Whether a tile can take a spawned mob: real floor with nothing anchored on top.</summary>
    private bool IsTileFreeForSpawn(EntityUid grid, MapGridComponent mapGrid, Vector2i tile)
    {
        if (!_map.TryGetTileRef(grid, mapGrid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return false;

        var anchored = _map.GetAnchoredEntitiesEnumerator(grid, mapGrid, tile);
        return !anchored.MoveNext(out _);
    }

    private void OnGlobalSpoke(EntitySpokeEvent ev)
    {
        if (!_enabled)
            return;

        // Radio-prefixed speech is delivered again via RadioReceiveEvent — skip here.
        if (ev.Channel != null)
            return;

        var speaker = ev.Source;
        for (var i = _bots.Count - 1; i >= 0; i--)
        {
            var bot = _bots[i];
            if (!Exists(bot))
            {
                _bots.RemoveAt(i);
                continue;
            }

            // Corpses do not listen.
            if (!IsBotUp(bot))
                continue;

            if (speaker == bot)
                continue;

            var botXform = Transform(bot);
            var speakerXform = Transform(speaker);
            if (botXform.MapID != speakerXform.MapID)
                continue;

            // Mini: a bot only understands languages it actually speaks. The spoke event carries the
            // raw text (obfuscation is chat-side), so without this check bots replied to xeno/animal
            // speech and to languages they never learned.
            if (ev.Language != null && TryComp<LanguageSpeakerComponent>(bot, out _) &&
                !_language.CanUnderstand(bot, ev.Language.ID))
            {
                continue;
            }

            // Mini: whispers are for the person next to you, and walls are not transparent.
            var radius = ev.IsWhisper ? WhisperHearRadius : _hearRadius;
            if (!_examine.InRangeUnOccluded(bot, speaker, radius))
                continue;

            var distance = (_transform.GetWorldPosition(botXform) - _transform.GetWorldPosition(speakerXform)).Length();
            HandleSpeech(bot, speaker, ev.Message, viaRadio: false, distance);
        }
    }

    private void OnRadioReceive(EntityUid bot, NeuroPlayerComponent comp, ref RadioReceiveEvent ev)
    {
        if (!_enabled || !IsBotUp(bot))
            return;

        HandleSpeech(bot, ev.MessageSource, ev.OriginalChatMsg.Message, viaRadio: true);
    }

    private void HandleSpeech(EntityUid bot, EntityUid speaker, string message, bool viaRadio, float distance = -1f)
    {
        var comp = Comp<NeuroPlayerComponent>(bot);

        if (!IsBotUp(bot) || !_prototypes.TryIndex(comp.PersonaId, out var persona))
            return;

        // Admin mute: the bot keeps hearing the world but never answers it (and costs nothing).
        if (IsMuted(comp))
            return;

        var speakerName = Name(speaker);

        // Mini: radio chatter from another map used to fill the bot's context even though it is
        // nowhere near the conversation. Same map or a direct address only.
        if (!viaRadio || HasComp<NeuroPlayerComponent>(speaker) ||
            Transform(bot).MapID == Transform(speaker).MapID)
        {
            AddContext(bot, speakerName, message);
        }

        // Another bot talking: rare two-line small talk instead of total silence, never a chain.
        if (HasComp<NeuroPlayerComponent>(speaker))
        {
            TryBotChatter(bot, persona, comp, speaker, message);
            return;
        }

        // Named address: always answer (both local and radio).
        if (MatchesName(persona.Name, message))
        {
            TryRespond(bot, persona, speakerName, message, viaRadio, proactive: false);
            return;
        }

        // Unnamed speech: only react locally — global radio replies without a name would be spam.
        if (viaRadio)
            return;

        if (_timing.CurTime < comp.NextProactiveResponse)
            return;

        // Face-to-face talk (<= 3 tiles) almost always gets a reply; background chatter — by chance.
        var chance = distance >= 0 && distance <= DirectReplyDistance
            ? _directChance
            : _proactiveChance;

        if (!_random.Prob(chance))
            return;

        TryRespond(bot, persona, speakerName, message, viaRadio, proactive: true);
    }

    /// <summary>
    /// Mini: bot-to-bot small talk, throttled by neuroplayer.bot_chatter_cooldown_seconds and
    /// neuroplayer.bot_chatter_chance. A reply to a bot's own chatter is never answered again, so
    /// two bots cannot ping-pong and burn requests forever.
    /// </summary>
    private void TryBotChatter(
        EntityUid bot,
        NeuroPersonaPrototype persona,
        NeuroPlayerComponent comp,
        EntityUid speaker,
        string message)
    {
        if (!TryComp<NeuroPlayerComponent>(speaker, out var speakerComp))
            return;

        // The speaker's last line was itself a bot chatter reply — the exchange ends here.
        if (speakerComp.LastChatterReply != null &&
            _timing.CurTime - speakerComp.LastChatterReply.Value < TimeSpan.FromSeconds(BotChatterChainWindowSeconds))
        {
            return;
        }

        if (_timing.CurTime < comp.NextBotChatter || !_random.Prob(_botChatterChance))
            return;

        comp.NextBotChatter = _timing.CurTime + TimeSpan.FromSeconds(_botChatterCooldownSeconds);
        comp.LastChatterReply = _timing.CurTime;

        TryRespond(bot, persona, Name(speaker), message, viaRadio: false, proactive: true);
    }

    /// <summary>
    /// Addressed by the full persona name ("Вася Прохоров"), by the given name or by the surname,
    /// including common Russian declensions ("Васю", "Васе", "Морозову", "Гордею"). Matching is
    /// word-based: a substring inside a longer word no longer counts, so "грузчиками" does not ping
    /// the bot named "Грузчик".
    /// </summary>
    private static bool MatchesName(string personaName, string message)
    {
        var words = message.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return false;

        if (ContainsWordRun(words, personaName))
            return true;

        var nameWords = personaName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (nameWords.Length == 0)
            return false;

        if (MatchesDeclinedWord(words, nameWords[0]))
            return true;

        // Surname: "Морозову видел?" is as much an address as "Лика?".
        return nameWords.Length > 1 && MatchesDeclinedWord(words, nameWords[^1]);
    }

    /// <summary>Word-run or declined-form match of one name part against the message words.</summary>
    private static bool MatchesDeclinedWord(string[] messageWords, string nameWord)
    {
        var normalized = NormalizeYo(nameWord);
        if (normalized.Length < 3)
            return false;

        var stem = TrimDeclension(normalized);
        foreach (var word in messageWords)
        {
            var candidate = NormalizeYo(word);
            if (candidate.Length < MinDeclinedNameLength || candidate.Length > stem.Length + MaxNameSuffixLength)
                continue;

            if (candidate.AsSpan().StartsWith(stem, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>Players type "е" for "ё" all the time: Пётр/Петру must still match.</summary>
    private static string NormalizeYo(string value)
    {
        return value.Replace('ё', 'е').Replace('Ё', 'Е');
    }

    /// <summary>Whether the message contains the persona's full name as consecutive words.</summary>
    private static bool ContainsWordRun(string[] words, string name)
    {
        var nameWords = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (nameWords.Length == 0 || words.Length < nameWords.Length)
            return false;

        for (var start = 0; start <= words.Length - nameWords.Length; start++)
        {
            var match = true;
            for (var offset = 0; offset < nameWords.Length; offset++)
            {
                if (!words[start + offset].Equals(nameWords[offset], StringComparison.OrdinalIgnoreCase))
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Drops a trailing vowel or "й" so declined forms share a stem: "Вася" -> "Вас",
    /// "Гордей" -> "Горде", "Пётр" -> "Петр" (a consonant ending stays).
    /// </summary>
    private static string TrimDeclension(string name)
    {
        const string endings = "аеёиоуыэюяй";
        return name.Length > 3 && endings.Contains(char.ToLowerInvariant(name[^1]))
            ? name[..^1]
            : name;
    }

    private void AddContext(EntityUid bot, string speaker, string message)
    {
        var comp = Comp<NeuroPlayerComponent>(bot);
        comp.Context.Enqueue((speaker, message));
        while (comp.Context.Count > _maxContext)
            comp.Context.Dequeue();
    }

    private void TryRespond(EntityUid bot, NeuroPersonaPrototype persona, string speakerName, string message, bool viaRadio, bool proactive)
    {
        if (!IsBotUp(bot))
            return;

        var comp = Comp<NeuroPlayerComponent>(bot);
        if (IsMuted(comp))
            return;

        var config = ResolveProvider();
        if (!config.IsConfigured)
        {
            WarnUnconfigured(config);
            return;
        }

        if (comp.RequestInFlight || _timing.CurTime < comp.NextAllowedResponse)
            return;

        // Global budget: per-bot cooldowns alone still allowed ~45 requests a minute during spam.
        if (!TryTakeRequestSlot())
            return;

        comp.RequestInFlight = true;
        _ = RunCompletionAsync(bot, comp, persona, speakerName, message, viaRadio, proactive);
    }

    /// <summary>Sliding-window budget guard (neuroplayer.requests_per_minute, 0 = unlimited).</summary>
    private bool TryTakeRequestSlot()
    {
        if (_requestsPerMinute <= 0)
            return true;

        var now = _timing.CurTime;
        while (_requestTimes.Count > 0 && now - _requestTimes.Peek() > TimeSpan.FromMinutes(1))
            _requestTimes.Dequeue();

        if (_requestTimes.Count >= _requestsPerMinute)
        {
            _requestsRateLimited++;
            return false;
        }

        _requestTimes.Enqueue(now);
        return true;
    }

    /// <summary>
    /// Sends a request through the active provider and, if it fails, once more through
    /// neuroplayer.fallback_provider. Returns the reply text or null.
    /// </summary>
    private async Task<string?> SendCompletionWithFallbackAsync(
        NeuroProviderConfig primary,
        string systemPrompt,
        string userPrompt,
        CancellationToken cancel,
        int? maxTokens = null)
    {
        try
        {
            var reply = await SendCompletionAsync(primary, systemPrompt, userPrompt, cancel, maxTokens);
            Interlocked.Increment(ref _requestsOk);
            return reply;
        }
        catch (Exception primaryError)
        {
            Interlocked.Increment(ref _requestsFailed);
            _sawmill.Warning($"Neuro player request failed on {primary.Provider}/{primary.Model}: {primaryError.Message}");

            if (!TryResolveFallback(primary, out var fallback))
                return null;

            try
            {
                Interlocked.Increment(ref _fallbacks);
                _sawmill.Info($"Neuro players: retrying on {fallback.Provider}/{fallback.Model}.");

                using var fallbackCts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds));
                var reply = await SendCompletionAsync(fallback, systemPrompt, userPrompt, fallbackCts.Token, maxTokens);
                Interlocked.Increment(ref _requestsOk);
                return reply;
            }
            catch (Exception fallbackError)
            {
                Interlocked.Increment(ref _requestsFailed);
                _sawmill.Warning(
                    $"Neuro player fallback {fallback.Provider}/{fallback.Model} failed too: {fallbackError.Message}");
                return null;
            }
        }
    }

    private bool TryResolveFallback(NeuroProviderConfig primary, out NeuroProviderConfig fallback)
    {
        fallback = default;

        if (string.IsNullOrWhiteSpace(_fallbackProvider))
            return false;

        var candidate = NeuroProviderPresets.Resolve(_cfg, _fallbackProvider, _apiUrl, _apiKey, _model);

        if (!candidate.IsConfigured || candidate.Provider == primary.Provider)
            return false;

        fallback = candidate;
        return true;
    }

    /// <summary>Counters for the neurobots command. Written from request tasks, hence Interlocked.</summary>
    public string DescribeStats()
    {
        return $"requests ok={Volatile.Read(ref _requestsOk)}, failed={Volatile.Read(ref _requestsFailed)}, " +
               $"fallbacks={Volatile.Read(ref _fallbacks)}, rateLimited={Volatile.Read(ref _requestsRateLimited)}, " +
               $"repliesBlocked={Volatile.Read(ref _blockedReplies)}";
    }

    /// <summary>
    /// Effective endpoint/key/model of the active LLM provider (DeepSeek, Z.ai, OpenAI, OpenRouter
    /// or the legacy custom endpoint).
    /// </summary>
    private NeuroProviderConfig ResolveProvider()
    {
        // Пустое значение = legacy "custom" из конфига, ругаться на него не нужно.
        if (!string.IsNullOrWhiteSpace(_provider) && !NeuroProviderPresets.IsKnown(_provider))
        {
            if (_unknownProviderWarned != _provider)
            {
                _unknownProviderWarned = _provider;
                _sawmill.Warning(
                    $"Neuro players: unknown provider '{_provider}', falling back to '{NeuroProviderPresets.CustomId}'. " +
                    $"Available: {NeuroProviderPresets.AvailableIds}.");
            }
        }

        return NeuroProviderPresets.Resolve(_cfg, _provider, _apiUrl, _apiKey, _model);
    }

    private void WarnUnconfigured(NeuroProviderConfig config)
    {
        if (_configWarned)
            return;

        _configWarned = true;
        _sawmill.Warning(
            $"Neuro players: LLM is not configured (provider={config.Provider}, " +
            $"endpoint={(string.IsNullOrEmpty(config.Endpoint) ? "<empty>" : config.Endpoint)}, " +
            $"key={(string.IsNullOrEmpty(config.ApiKey) ? "<empty>" : "set")}). " +
            $"Set neuroplayer.api_key and pick a provider from: {NeuroProviderPresets.AvailableIds}.");
    }

    private static string BuildSystemPrompt(string botName, string character)
    {
        return
            $"Ты — {botName}, пассажир на космической станции Нанотрайзен (игра Space Station 14).\n" +
            $"{character}\n" +
            "Правила:\n" +
            "- Отвечай ОДНОЙ короткой репликой до 15 слов, по-русски, живым разговорным стилем.\n" +
            "- Никогда не говори, что ты ИИ, бот, модель или что ты в игре или симуляции.\n" +
            "- Не используй звёздочки и описания действий — только прямую речь.\n" +
            "- Не обещай игровых действий и не обсуждай администрацию, правила и читы.\n" +
            "- Если сказать нечего — короткая нейтральная реакция.";
    }

    /// <summary>
    /// Sends one chat completion through the resolved provider and returns the raw reply text.
    /// Provider differences (endpoint, model, thinking field) are handled here; failures carry the
    /// response body so a bad key or model name is visible in the log instead of a bare status code.
    /// </summary>
    private async Task<string?> SendCompletionAsync(
        NeuroProviderConfig config,
        string systemPrompt,
        string userPrompt,
        CancellationToken cancel,
        int? maxTokens = null)
    {
        var body = new CompletionRequest
        {
            Model = config.Model,
            Messages =
            [
                new ChatMessageDto("system", systemPrompt),
                new ChatMessageDto("user", userPrompt),
            ],
            Temperature = _temperature,
            MaxTokens = maxTokens ?? _maxTokens,
        };

        // GLM-4.5/4.6 style reasoning toggle: with a small token budget thinking eats the whole
        // reply, so it is disabled unless requested. Sent only to providers that accept the field —
        // DeepSeek and OpenAI reject unknown request fields.
        if (config.SupportsThinking)
            body.Thinking = new ThinkingConfig { Type = _thinkingEnabled ? "enabled" : "disabled" };

        using var request = new HttpRequestMessage(HttpMethod.Post, config.Endpoint);
        request.Headers.Authorization = new("Bearer", config.ApiKey);
        request.Content = JsonContent.Create(body);

        using var response = await _httpClient.SendAsync(request, cancel);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancel);
            if (errorBody.Length > 400)
                errorBody = errorBody[..400];

            throw new HttpRequestException(
                $"{(int) response.StatusCode} {response.StatusCode} from {config.Provider}/{config.Model}: {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<CompletionResponse>(cancellationToken: cancel);
        var choice = payload?.Choices?.FirstOrDefault()?.Message;
        var reply = choice?.Content?.Trim();

        if (string.IsNullOrWhiteSpace(reply) && !string.IsNullOrWhiteSpace(choice?.ReasoningContent))
        {
            // Reasoning models (deepseek-reasoner and friends) spend max_tokens on hidden thinking
            // and then return an empty answer — the bot would just stay silent forever.
            _sawmill.Warning(
                $"Neuro players: {config.Provider}/{config.Model} returned only reasoning_content. " +
                $"Raise neuroplayer.max_tokens (now {_maxTokens}) or use a non-reasoning model.");
        }

        return reply;
    }

    private async Task RunCompletionAsync(
        EntityUid bot,
        NeuroPlayerComponent comp,
        NeuroPersonaPrototype persona,
        string speakerName,
        string message,
        bool viaRadio,
        bool proactive)
    {
        try
        {
            // Human-feel jitter: bots never reply instantly.
            await Task.Delay(_random.Next(300, 1200));

            if (Deleted(bot) || Terminating(bot))
                return;

            var config = ResolveProvider();
            if (!config.IsConfigured)
            {
                WarnUnconfigured(config);
                return;
            }

            var botName = Name(bot);
            var context = string.Join("\n", comp.Context.Select(c => $"{c.Speaker}: {c.Message}"));

            var systemPrompt = BuildSystemPrompt(botName, persona.Character);

            var userPrompt = string.IsNullOrWhiteSpace(context)
                ? $"{speakerName} говорит тебе: {message}"
                : $"Контекст переписки:\n{context}\n\n{speakerName} обращается к тебе: {message}";

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds));
            var reply = await SendCompletionWithFallbackAsync(config, systemPrompt, userPrompt, cts.Token);

            // Mini: the bot may have died while the model was thinking — a corpse does not speak
            // (the request is already paid for, but at least it does not get broadcast).
            if (string.IsNullOrWhiteSpace(reply) || Deleted(bot) || Terminating(bot) || !IsBotUp(bot))
                return;

            // Mini: players asked for no long dashes in bot replies.
            reply = reply.Replace('—', '-').Replace('–', '-');
            reply = string.Join(' ', reply.Split(' ', StringSplitOptions.RemoveEmptyEntries));

            if (reply.Length > MaxReplyLength)
                reply = reply[..MaxReplyLength];
            if (reply.StartsWith(botName, StringComparison.OrdinalIgnoreCase))
                reply = reply[botName.Length..].TrimStart(':', ' ', '-');

            // Moderation screen: blocked words and out-of-character "I am an AI" admissions.
            if (!TryScreenReply(bot, reply))
                return;

            if (viaRadio)
                reply = ";" + reply;

            _chat.TrySendInGameICMessage(bot, reply, InGameICChatType.Speak, hideChat: false, hideLog: true);

            // Mini: bot speech used to leave no trace at all — no ActorComponent means ChatSystem
            // skips its own admin log. Record it ourselves so moderation can see what bots said.
            _adminLog.Add(LogType.Chat, LogImpact.Low, $"Say from neuro player {botName}: {reply}");
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Neuro player request failed: {e.Message}");
        }
        finally
        {
            comp.RequestInFlight = false;
            comp.NextAllowedResponse = _timing.CurTime + TimeSpan.FromSeconds(_cooldownSeconds);
            if (proactive)
                comp.NextProactiveResponse = _timing.CurTime + TimeSpan.FromSeconds(_proactiveCooldownSeconds);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // The ahelp assistant works even when the IC bots are disabled.
        TickAHelp();

        if (!_enabled)
            return;

        MaintainBots();

        _dangerScanAccumulator += frameTime;
        if (_dangerScanAccumulator >= 1f)
        {
            _dangerScanAccumulator = 0f;
            ScanDangers();
        }

        _seekAccumulator += frameTime;
        if (_seekAccumulator >= SeekCheckSeconds)
        {
            _seekAccumulator = 0f;
            SeekLonelyPlayers();
        }

        _resistAccumulator += frameTime;
        if (_resistAccumulator >= ResistCheckSeconds)
        {
            _resistAccumulator = 0f;
            ResistBeingHeld();
        }

        var now = _timing.CurTime;
        for (var i = _bots.Count - 1; i >= 0; i--)
        {
            var bot = _bots[i];
            if (!TryComp<NeuroPlayerComponent>(bot, out var comp))
            {
                _bots.RemoveAt(i);
                _poiClaims.Remove(bot);
                continue;
            }

            // Downed bots keep their slot (they can be revived) but do not run errands.
            if (!IsBotUp(bot))
                continue;

            // Restore normal behavior once the flee / visit window is over.
            if (comp.DangerUntil != null && now > comp.DangerUntil)
            {
                comp.DangerUntil = null;
                RestoreIdle(bot);
            }
            else if (comp.HostileUntil != null && now > comp.HostileUntil)
            {
                comp.HostileUntil = null;
                RestoreIdle(bot);
            }
            else if (comp.VisitUntil != null && now > comp.VisitUntil)
            {
                comp.VisitUntil = null;
                RestoreIdle(bot);
            }

            if (comp.DangerUntil != null || comp.HostileUntil != null)
                continue;

            comp.PoiAccumulator += frameTime;
            if (comp.PoiAccumulator >= _poiIntervalSeconds)
            {
                comp.PoiAccumulator = 0f;
                TryVisit(bot, comp);
            }
        }
    }

    /// <summary>
    /// Mini: keeps the roster honest instead of relying on a single 45-second timer.
    /// Bots are spawned once per round, retried while the spawn failed (maps not ready, war mode)
    /// and brought back after a station war ends. Dead bots are never resurrected.
    /// </summary>
    private void MaintainBots()
    {
        if (_botCount <= 0)
            return;

        _bots.RemoveAll(bot => !Exists(bot));

        if (_spawnedThisRound && !_warSuppressed)
            return;

        if (TypanStationWarRuleSystem.IsModeActive)
            return;

        // Nobody is on the station before the round starts, and maps need time to settle after it.
        if (_roundStartTime == TimeSpan.Zero || _timing.CurTime - _roundStartTime < SpawnDelay)
            return;

        // A failed attempt must not be repeated every tick: it scans prototypes and logs.
        if (_timing.CurTime - _lastSpawnAttempt < SpawnRetryDelay)
            return;

        _lastSpawnAttempt = _timing.CurTime;

        if (_warSuppressed)
        {
            _sawmill.Info("Neuro players: station war is over, bringing the bots back.");
            _warSuppressed = false;
        }

        SpawnBots();
    }

    /// <summary>
    /// Mini: neuroplayer.seek_radius finally does something — a bot with nobody around walks
    /// towards the nearest real player instead of idling in an empty corridor.
    /// </summary>
    private void SeekLonelyPlayers()
    {
        if (_seekRadius <= 0f)
            return;

        // Mini: every bot used to march to the same nearest player and pile up on their tile.
        // A player somebody is already walking to gets a crowd penalty, so the crew spreads over
        // the people actually on the station instead of forming one queue.
        var crowd = new Dictionary<EntityUid, int>();

        foreach (var bot in _bots)
        {
            if (!IsBotUp(bot) || !TryComp<NeuroPlayerComponent>(bot, out var comp))
                continue;

            if (comp.DangerUntil != null || comp.HostileUntil != null || comp.VisitUntil != null)
                continue;

            var nearest = FindNearestPlayer(bot, crowd);
            if (nearest == null)
                continue;

            var (target, coords, distance) = nearest.Value;

            // Somebody is already close enough — no need to march anywhere.
            if (distance <= _seekRadius)
                continue;

            if (!TryComp<HTNComponent>(bot, out var htn))
                continue;

            crowd[target] = crowd.GetValueOrDefault(target) + 1;

            // A personal offset, so two bots heading for the same person do not share one tile.
            htn.Blackboard.SetValue("NeuroPoint", coords.Offset(_random.NextVector2(SeekSpreadRadius)));
            SetRootTask(htn, "NeuroVisitCompound");
            comp.VisitUntil = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(60, 120));
        }
    }

    /// <summary>
    /// Nearest living player (never a ghost or another bot). Players that other bots are already
    /// walking to are pushed away by <see cref="SeekCrowdPenalty"/>, which keeps the whole crew from
    /// converging on one person.
    /// </summary>
    private (EntityUid Target, EntityCoordinates Coords, float Distance)? FindNearestPlayer(
        EntityUid bot, Dictionary<EntityUid, int> crowd)
    {
        var botXform = Transform(bot);
        var botPos = _transform.GetWorldPosition(botXform);

        (EntityUid Target, EntityCoordinates Coords, float Distance)? best = null;
        var bestScore = float.MaxValue;

        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (uid == bot || xform.MapID != botXform.MapID || HasComp<GhostComponent>(uid))
                continue;

            var distance = (xform.WorldPosition - botPos).Length();
            if (distance > MaxSeekRange)
                continue;

            var score = distance + crowd.GetValueOrDefault(uid) * SeekCrowdPenalty;
            if (score >= bestScore)
                continue;

            bestScore = score;
            best = (uid, xform.Coordinates, distance);
        }

        return best;
    }

    /// <summary>
    /// Mini: switches the bot's root task and throws away the plan it is running right now. Without
    /// the throw-away the engine keeps the old plan alive and only accepts a replacement whose branch
    /// record is "worse" (HTNSystem.UpdateNPC compares them), so a bot parked in a 7-second idle wait
    /// ignored a punch for as long as that wait lasted and the reaction looked absent. This is the
    /// same shutdown sequence the engine itself runs when it swaps a plan.
    /// </summary>
    private void SetRootTask(HTNComponent htn, string task)
    {
        htn.RootTask = new HTNCompoundTask { Task = task };

        // A plan still being computed belongs to the old root task. Its result would be adopted as
        // soon as it lands (the bot then carries on with its old life and ignores the danger) because
        // the engine keeps the running plan unless the new branch record is "worse". The engine drops
        // planning the same way in SetHTNEnabled / OnHTNShutdown; a cancelled job finishes with a
        // null result instead of an error.
        htn.PlanningToken?.Cancel();
        htn.PlanningToken = null;
        htn.PlanningJob = null;

        if (htn.Plan != null)
        {
            _htn.ShutdownTask(htn.Plan.CurrentOperator, htn.Blackboard, HTNOperatorStatus.BetterPlan);
            _htn.ShutdownPlan(htn); // also clears htn.Plan
        }

        _htn.Replan(htn);
    }

    private void RestoreIdle(EntityUid bot)
    {
        if (!TryComp<HTNComponent>(bot, out var htn))
            return;

        // Mini: a bot that was sent to the evac shuttle goes back aboard after a scare or a visit,
        // and keeps waiting there until the shuttle leaves its map.
        if (TryComp<NeuroPlayerComponent>(bot, out var comp) && TryGetEvacPoint(bot, comp, out var evacPoint))
        {
            htn.Blackboard.SetValue("NeuroPoint", evacPoint);
            SetRootTask(htn, "NeuroVisitCompound");
            comp.VisitUntil = _timing.CurTime + TimeSpan.FromSeconds(420);
            return;
        }

        SetRootTask(htn, "IdleCompound");
    }

    private void ScanDangers()
    {
        foreach (var bot in _bots)
        {
            if (!TryComp<NeuroPlayerComponent>(bot, out var comp) || comp.DangerUntil != null)
                continue;

            // Corpses do not run from fires.
            if (!IsBotUp(bot))
                continue;

            // Brave enough: engage hostile-faction mobs that wander too close. Machines do not
            // count: a turret or sentry is not an opponent a passenger can fight.
            var engage = false;
            foreach (var hostile in _factions.GetNearbyHostiles((bot, CompOrNull<NpcFactionMemberComponent>(bot), null), 8f))
            {
                if (IsMachine(hostile))
                    continue;

                engage = true;
                break;
            }

            if (engage)
            {
                if (TryComp<HTNComponent>(bot, out var attackHtn) && attackHtn.RootTask.Task != "SimpleHumanoidHostileCompound")
                {
                    // A fight runs through the NPC target queries, so the machine list is refreshed
                    // right before it starts: hardware that appeared after the bot spawned (a
                    // deployed turret crate, war hardware) must not become a combat target.
                    IgnoreMachines(bot);

                    SetRootTask(attackHtn, "SimpleHumanoidHostileCompound");
                }

                comp.HostileUntil = _timing.CurTime + TimeSpan.FromSeconds(6);
                continue;
            }

            // Mini: a burning bot runs from its own fire (the old scan only looked at neighbours)
            // and the scan is a range lookup instead of a sweep over every flammable entity in the world.
            var botXform = Transform(bot);
            if (TryComp<FlammableComponent>(bot, out var ownFlammable) && ownFlammable.FireStacks > 0)
            {
                TriggerDanger(bot, comp, _transform.GetWorldPosition(botXform), FireCries);
                continue;
            }

            foreach (var (uid, flammable) in _lookup.GetEntitiesInRange<FlammableComponent>(
                         botXform.Coordinates, FireAlertRadius))
            {
                if (uid == bot || flammable.FireStacks <= 0)
                    continue;

                TriggerDanger(bot, comp, _transform.GetWorldPosition(uid), FireCries);
                break;
            }

            // Mini: an armed hostile turret is a hazard, not a fight — walk out of its reach
            // instead of standing in the line of fire waiting to be shot (and say so, so the
            // players around understand why the passenger suddenly backs off).
            if (FindArmedHostileTurret(bot, botXform) is { } turret)
                TriggerDanger(bot, comp, _transform.GetWorldPosition(turret), TurretCries);

            // Mini: somebody next to the bot is being beaten right now — cry and back off. The damage
            // event itself cannot be hooked: the engine allows exactly one directed subscription per
            // component+event pair per game and DailyQuestSystem owns
            // DamageableComponent+DamageChangedEvent, so this is a range scan over bodies that still
            // carry real damage and were touched within the last couple of seconds.
            foreach (var (uid, _) in _lookup.GetEntitiesInRange<MobStateComponent>(
                         botXform.Coordinates, FightAlertRadius))
            {
                if (uid == bot || HasComp<NeuroPlayerComponent>(uid))
                    continue;

                if (!TryComp<DamageableComponent>(uid, out var damageable) || damageable.TotalDamage < FightDamage)
                    continue;

                if (_timing.CurTime - damageable.LastModifiedTime > FightAlertWindow)
                    continue;

                TriggerDanger(bot, comp, _transform.GetWorldPosition(uid), FightCries);
                break;
            }
        }
    }

    /// <summary>
    /// Mini: whether something the faction system calls a hostile is a machine rather than a
    /// living being — turrets and sentries above all. Bots do not fight machines: a machine has
    /// no fight to lose, and a bot shooting a station AI turret only collects live ammo in reply.
    /// </summary>
    private bool IsMachine(EntityUid ent)
    {
        if (HasComp<MobStateComponent>(ent))
            return false;

        return HasComp<DeployableTurretComponent>(ent)
            || HasComp<TurretTargetSettingsComponent>(ent)
            || HasComp<GunComponent>(ent);
    }

    /// <summary>Whether a machine is a hazard rather than scenery: anything but the bot's own side.</summary>
    private bool IsHostileMachine(EntityUid bot, EntityUid target)
    {
        // Friendly machines (the station's own NanoTrasen turrets) are scenery; anything else
        // that shoots is a hazard.
        return IsMachine(target) && !_factions.IsEntityFriendly(bot, target);
    }

    /// <summary>
    /// Nearest hostile turret that is actually out and loaded, or null. Retracted or broken
    /// turrets are furniture: there is nothing to run from.
    /// </summary>
    private EntityUid? FindArmedHostileTurret(EntityUid bot, TransformComponent botXform)
    {
        foreach (var (uid, deployable) in _lookup.GetEntitiesInRange<DeployableTurretComponent>(
                     botXform.Coordinates, TurretAlertRange))
        {
            // Deployed / deploying / firing share the Deployed bit; retracted and broken do not.
            if ((deployable.CurrentState & DeployableTurretState.Deployed) == 0)
                continue;

            if (!IsHostileMachine(bot, uid) || !_examine.InRangeUnOccluded(bot, uid, TurretAlertRange))
                continue;

            return uid;
        }

        // Turrets that never retract (ballistic, xeno, the mini laser ones) have no state to
        // check: an NPC-driven gun that is not a mob is armed by definition.
        foreach (var (uid, _) in _lookup.GetEntitiesInRange<GunComponent>(botXform.Coordinates, TurretAlertRange))
        {
            if (HasComp<MobStateComponent>(uid) || !HasComp<HTNComponent>(uid))
                continue;

            if (!IsHostileMachine(bot, uid) || !_examine.InRangeUnOccluded(bot, uid, TurretAlertRange))
                continue;

            return uid;
        }

        return null;
    }

    /// <summary>
    /// Takes every machine of the map out of the bot's target lists (friendly machines are never
    /// hostile targets anyway, so the list can be faction-agnostic). Both the danger scan and the
    /// NPC combat target queries read the faction system's hostile lookup, and a faction exception
    /// is the one place that covers both. Called on spawn and again whenever a bot picks a fight,
    /// so hardware deployed mid-round is covered too (the engine allows only one ComponentStartup
    /// subscriber per component, and the faction system already owns that one).
    /// </summary>
    private void IgnoreMachines(EntityUid bot)
    {
        var query = EntityQueryEnumerator<NpcFactionMemberComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (IsMachine(uid))
                _factions.IgnoreEntity(bot, uid);
        }
    }

    /// <summary>
    /// The bot itself was hurt: pain cries, decompression handling and running from whoever hit it.
    /// </summary>
    /// <remarks>
    /// Mini: the event is raised directed at the damaged entity, so this is a component subscription on
    /// the bot (<see cref="Initialize"/>). A brawl happening next to the bot is a range scan in
    /// <see cref="ScanDangers"/> instead — the global component+event slot is already taken.
    /// </remarks>
    private void OnBotDamaged(EntityUid bot, NeuroPlayerComponent comp, DamageChangedEvent ev)
    {
        if (!_enabled || !ev.DamageIncreased || ev.DamageDelta == null)
            return;

        // Already down (dying/dead): no more fleeing, no more paid replies from a corpse.
        if (!IsBotUp(bot))
            return;

        if (ev.DamageDelta.DamageDict.TryGetValue("Airloss", out var airloss) && airloss >= 3f)
        {
            // Decompression: scream, enable internals from own inventory, best-effort grab a hardsuit, run.
            Cry(bot, comp, DecompressionCries);
            TryEnableInternals(bot);
            TryGrabHardsuit(bot);
            TriggerDanger(bot, comp, _transform.GetWorldPosition(bot), null, cry: false);
            return;
        }

        var total = (float) ev.DamageDelta.GetTotal();

        // Mini: any real hit makes the bot cry out, not only a 10+ damage one — ordinary punches and
        // bullets were ignored outright. Fleeing needs a known attacker, so damage from space or a
        // stray wound only cries.
        if (total < 1f)
            return;

        Cry(bot, comp, PainCries);

        var attacker = ev.Origin is { } origin && origin != bot && Exists(origin)
            ? origin
            : (EntityUid?) null;

        if (total >= 10f || attacker != null)
        {
            var dangerPos = attacker != null
                ? _transform.GetWorldPosition(attacker.Value)
                : _transform.GetWorldPosition(bot);
            TriggerDanger(bot, comp, dangerPos, null, cry: false);
        }
    }

    private void TriggerDanger(EntityUid bot, NeuroPlayerComponent comp, Vector2 dangerPos, string[]? cries, bool cry = true, float seconds = 20f)
    {
        if (!TryComp<HTNComponent>(bot, out var htn))
            return;

        var fleePoint = ComputeFleePoint(bot, dangerPos);

        // Mini: a bot that is already on the run keeps its route. Restarting the plan on every hit
        // (the attack rate is a hit per second) cancelled the path it was walking, so it stuttered in
        // place and the reaction looked like nothing happened at all. Only a threat that moved
        // somewhere else entirely sends it down a new route.
        if (comp.DangerUntil != null &&
            htn.Blackboard.TryGetValue<EntityCoordinates>("NeuroPoint", out var current, EntityManager) &&
            (_transform.ToWorldPosition(current) - _transform.ToWorldPosition(fleePoint)).Length() <= RerouteDistance)
        {
            comp.DangerUntil = _timing.CurTime + TimeSpan.FromSeconds(seconds);
            comp.VisitUntil = null;

            if (cry && cries != null)
                Cry(bot, comp, cries);

            return;
        }

        htn.Blackboard.SetValue("NeuroPoint", fleePoint);
        SetRootTask(htn, "NeuroFleeCompound");

        comp.DangerUntil = _timing.CurTime + TimeSpan.FromSeconds(seconds);
        comp.VisitUntil = null;
        comp.PoiAccumulator = 0f;

        if (cry && cries != null)
            Cry(bot, comp, cries);
    }

    /// <summary>
    /// A tile away from the danger that the bot can actually see. Mini: a point behind a wall made
    /// MoveToOperator's planning-time pathfind fail, which failed the whole flee branch — the bot
    /// stood still while it was being beaten. Line of sight means real floor, and a short distance
    /// means the path there exists.
    /// </summary>
    private EntityCoordinates ComputeFleePoint(EntityUid bot, Vector2 dangerPos)
    {
        var botXform = Transform(bot);
        var botPos = _transform.GetWorldPosition(botXform);
        var away = botPos - dangerPos;
        if (away.LengthSquared() < 0.01f)
            away = new Vector2(1, 0);
        away = Vector2.Normalize(away);

        if (botXform.GridUid is { } grid && TryComp<MapGridComponent>(grid, out var mapGrid))
        {
            var centerTile = _transform.GetGridOrMapTilePosition(bot, botXform);
            for (var dist = 4; dist <= 30; dist += 2)
            {
                var tile = centerTile + (away * dist).Floored();
                if (!_map.TryGetTileRef(grid, mapGrid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
                    continue;

                var coords = _map.GridTileToLocal(grid, mapGrid, tile);
                if (_examine.InRangeUnOccluded(bot, coords, dist + 2f))
                    return coords;
            }
        }

        return botXform.Coordinates.Offset(away * 18f);
    }

    // Mini: react to any interaction so bots do not ignore the world.
    private void OnBotInteracted(EntityUid bot, NeuroPlayerComponent comp, InteractHandEvent ev)
    {
        if (ev.User == bot || !_enabled || !IsBotUp(bot))
            return;

        AddContext(bot, Name(ev.User), "(дотронулся до тебя)");
        Cry(bot, comp, TouchCries);
    }

    private void Cry(EntityUid bot, NeuroPlayerComponent comp, string[] phrases)
    {
        if (_timing.CurTime < comp.NextCry || !IsBotUp(bot) || IsMuted(comp))
            return;

        comp.NextCry = _timing.CurTime + TimeSpan.FromSeconds(12);
        SayCanned(bot, _random.Pick(phrases));
    }

    private void TryEnableInternals(EntityUid bot)
    {
        try
        {
            var internals = EnsureComp<InternalsComponent>(bot);

            foreach (var container in _container.GetAllContainers(bot))
            {
                foreach (var item in container.ContainedEntities)
                {
                    if (!HasComp<GasTankComponent>(item))
                        continue;

                    if (_internals.TryConnectTank((bot, internals), item))
                        return;
                }
            }
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Neuro player internals failed: {e.Message}");
        }
    }

    private void TryGrabHardsuit(EntityUid bot)
    {
        try
        {
            // Mini: range lookup instead of sweeping every piece of clothing on the map.
            var botXform = Transform(bot);
            foreach (var (uid, _) in _lookup.GetEntitiesInRange<ClothingComponent>(botXform.Coordinates, 1.5f))
            {
                // Only items lying free on the grid, within arm's reach.
                if (_container.IsEntityInContainer(uid))
                    continue;

                if ((Prototype(uid)?.ID ?? string.Empty).IndexOf("Hardsuit", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (!_hands.TryPickupAnyHand(bot, uid, checkActionBlocker: false))
                    continue;

                _inventory.TryEquip(bot, uid, "outerClothing", silent: true, force: true);
                return;
            }
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Neuro player hardsuit grab failed: {e.Message}");
        }
    }

    private void TryVisit(EntityUid bot, NeuroPlayerComponent comp)
    {
        if (!TryComp<HTNComponent>(bot, out var htn))
            return;

        var point = FindPointOfInterest(bot);
        if (point == null)
            return;

        _poiClaims[bot] = point.Value.Point;

        htn.Blackboard.SetValue("NeuroPoint", point.Value.Coords);
        SetRootTask(htn, "NeuroVisitCompound");
        comp.VisitUntil = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(120, 240));
    }

    /// <summary>
    /// A public spot to hang around: a bar first, late-join points (arrivals / halls) otherwise.
    /// Mini: spots another bot already claimed are skipped, so the crew spreads over the station
    /// instead of grazing on the same bar; when everything is taken, the spot furthest from the
    /// rest of the crew wins.
    /// </summary>
    private (EntityUid Point, EntityCoordinates Coords)? FindPointOfInterest(EntityUid bot)
    {
        var points = new List<(EntityUid Point, EntityCoordinates Coords)>();
        var botMap = Transform(bot).MapID;

        var bars = EntityQueryEnumerator<BarSignComponent, TransformComponent>();
        while (bars.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapID != botMap)
                continue;

            points.Add((uid, xform.Coordinates));
        }

        if (points.Count == 0)
        {
            // No bar on this map: hang around late-join points (arrivals / halls).
            var station = FindStation();
            if (station != null)
            {
                foreach (var coords in FindSpawnPoints(station.Value))
                    points.Add((coords.EntityId, coords));
            }
        }

        if (points.Count == 0)
            return null;

        var taken = new HashSet<EntityUid>();
        foreach (var (other, claim) in _poiClaims)
        {
            if (other != bot && Exists(other))
                taken.Add(claim);
        }

        var free = points.Where(p => !taken.Contains(p.Point)).ToList();
        if (free.Count > 0)
            return _random.Pick(free);

        // Every spot is somebody else's: take the one furthest from the rest of the crew.
        return points
            .OrderByDescending(p => DistanceToNearestBot(bot, p.Coords))
            .First();
    }

    /// <summary>Distance from <paramref name="coords"/> to the closest other bot (infinity when alone).</summary>
    private float DistanceToNearestBot(EntityUid bot, EntityCoordinates coords)
    {
        var pos = _transform.ToWorldPosition(coords);
        var botMap = Transform(bot).MapID;
        var nearest = float.MaxValue;

        foreach (var other in _bots)
        {
            if (other == bot || !Exists(other) || Transform(other).MapID != botMap)
                continue;

            nearest = Math.Min(nearest, (_transform.GetWorldPosition(other) - pos).Length());
        }

        return nearest;
    }

    private async Task<string?> CompleteRawAsync(NeuroPersonaPrototype persona, string botName, string userPrompt)
    {
        var config = ResolveProvider();
        if (!config.IsConfigured)
        {
            WarnUnconfigured(config);
            return null;
        }

        var systemPrompt = BuildSystemPrompt(botName, persona.Character);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds));
        var reply = await SendCompletionAsync(config, systemPrompt, userPrompt, cts.Token);

        if (string.IsNullOrWhiteSpace(reply))
            return null;

        if (reply.Length > MaxReplyLength)
            reply = reply[..MaxReplyLength];

        return reply;
    }

    public (bool Enabled, bool ApiConfigured, int Bots, string Provider, string Model) GetStatus()
    {
        var config = ResolveProvider();

        return (_enabled,
            config.IsConfigured,
            _bots.Count(Exists),
            config.Provider,
            config.Model);
    }

    /// <summary>
    /// Moderation screen for a raw model answer. Returns false when the line must not be said:
    /// it contains a word from neuroplayer.blocked_words, or the bot breaks character and calls
    /// itself an AI.
    /// </summary>
    private bool TryScreenReply(EntityUid bot, string reply)
    {
        return TryScreenText(Name(bot), reply, blockAiTalk: _blockAiTalk);
    }

    /// <summary>
    /// Same screen as <see cref="TryScreenReply"/> but for a non-entity speaker (the ahelp
    /// assistant), which is allowed to admit that it is an AI.
    /// </summary>
    private bool TryScreenText(string who, string reply, bool blockAiTalk)
    {
        foreach (var word in _blockedWords)
        {
            if (!ContainsWordWithSuffix(reply, word, MaxNameSuffixLength))
                continue;

            _sawmill.Warning($"Neuro reply for {who}: dropped by neuroplayer.blocked_words ('{word}'): {reply}");
            Interlocked.Increment(ref _blockedReplies);
            _adminLog.Add(LogType.Chat, LogImpact.Medium,
                $"Neuro reply for {who} blocked by blocked_words ('{word}'): {reply}");
            return false;
        }

        if (blockAiTalk && AdmitsBeingAi(reply))
        {
            _sawmill.Info($"Neuro reply for {who}: out-of-character reply dropped: {reply}");
            Interlocked.Increment(ref _blockedReplies);
            return false;
        }

        return true;
    }

    /// <summary>Word match with a little morphology: "мат" catches "матом", but not "материал".</summary>
    private static bool ContainsWordWithSuffix(string text, string word, int maxSuffix)
    {
        foreach (var token in text.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length < word.Length || token.Length > word.Length + maxSuffix)
                continue;

            if (token.AsSpan().StartsWith(word, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static readonly string[] AiWords =
    [
        "ии", "иишка", "бот", "боты", "ботик", "нейросеть", "нейронка", "нейросетевой",
        "модель", "моделька", "чатгпт", "gpt", "гпт", "llm", "deepseek", "дипсик", "промпт", "алгоритм",
    ];

    private static readonly string[] SelfWords = ["я", "мне", "меня", "мой", "моя", "моё", "мы", "нас", "себя"];

    /// <summary>True when the bot says it is an AI/бот/модель instead of staying in character.</summary>
    private static bool AdmitsBeingAi(string reply)
    {
        var words = reply
            .Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant())
            .ToArray();

        var mentionsAi = words.Any(w => AiWords.Any(ai =>
            w.Length >= ai.Length && w.Length <= ai.Length + 2 &&
            w.AsSpan().StartsWith(ai, StringComparison.OrdinalIgnoreCase)));

        if (!mentionsAi)
            return false;

        // "я бот", "как языковая модель", "я не человек" — self reference is what gives it away.
        return words.Any(w => SelfWords.Contains(w));
    }

    /// <summary>Bots matching an admin query: persona id / name, or "all".</summary>
    public List<EntityUid> FindBots(string query)
    {
        var result = new List<EntityUid>();
        var matchAll = query is "all" or "*";

        foreach (var bot in _bots)
        {
            if (!Exists(bot) || !TryComp<NeuroPlayerComponent>(bot, out var comp))
                continue;

            if (matchAll)
            {
                result.Add(bot);
                continue;
            }

            if (comp.PersonaId.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                Name(bot).Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(bot);
            }
        }

        return result;
    }

    /// <summary>Mutes matching bots forever (minutes == null) or for a while. Returns how many.</summary>
    public int MuteBots(string query, int? minutes)
    {
        var muted = 0;
        foreach (var bot in FindBots(query))
        {
            var comp = Comp<NeuroPlayerComponent>(bot);

            if (minutes == null)
            {
                comp.Muted = true;
                comp.MutedUntil = TimeSpan.Zero;
            }
            else
            {
                comp.Muted = false;
                comp.MutedUntil = _timing.CurTime + TimeSpan.FromMinutes(minutes.Value);
            }

            muted++;
        }

        return muted;
    }

    public int UnmuteBots(string query)
    {
        var unmuted = 0;
        foreach (var bot in FindBots(query))
        {
            var comp = Comp<NeuroPlayerComponent>(bot);
            if (!comp.Muted && comp.MutedUntil == TimeSpan.Zero)
                continue;

            comp.Muted = false;
            comp.MutedUntil = TimeSpan.Zero;
            unmuted++;
        }

        return unmuted;
    }

    /// <summary>One line per bot for the neurobots command.</summary>
    public string DescribeBots()
    {
        if (_bots.Count == 0)
            return "нейроигроков нет (neuroplayer.enabled / bot_count / война / раунд ещё не начался).";

        var lines = new List<string>();
        foreach (var bot in _bots)
        {
            if (!Exists(bot) || !TryComp<NeuroPlayerComponent>(bot, out var comp))
                continue;

            var state = _mobState.IsDead(bot) ? "мертв" : _mobState.IsCritical(bot) ? "крит" : "жив";
            // Why the bot is not at its post: fleeing, fighting or on an errand.
            var busy = comp.DangerUntil != null
                ? "бежит"
                : comp.HostileUntil != null
                    ? "бой"
                    : comp.VisitUntil != null
                        ? "идёт"
                        : "стоит";
            var muted = comp.Muted
                ? ", мут"
                : comp.MutedUntil != TimeSpan.Zero && _timing.CurTime < comp.MutedUntil
                    ? $", мут до {comp.MutedUntil - _timing.CurTime:hh\\:mm\\:ss}"
                    : string.Empty;

            lines.Add($"- {Name(bot)} [{comp.PersonaId.Id}] {state}, {busy}{muted}");
        }

        return lines.Count == 0 ? "нейроигроков нет." : string.Join("\n", lines);
    }

    /// <summary>
    /// Live provider check for admins: sends one real request and returns the reply or the error.
    /// Answers "did I configure DeepSeek right?" without waiting for a round to start.
    /// </summary>
    public async Task<string> TestCompletionAsync(string? prompt)
    {
        var config = ResolveProvider();

        var header = $"provider={config.Provider}, model={config.Model}, endpoint=" +
                     $"{(string.IsNullOrEmpty(config.Endpoint) ? "<empty>" : config.Endpoint)}, " +
                     $"key={(string.IsNullOrEmpty(config.ApiKey) ? "<empty>" : "set")}";

        if (!config.IsConfigured)
            return $"{header}\nНЕ НАСТРОЕНО: задайте neuroplayer.api_key и neuroplayer.provider.";

        var systemPrompt =
            "Ты — тестовый нейроигрок на космической станции. Ответь одной короткой фразой по-русски.";
        var userPrompt = string.IsNullOrWhiteSpace(prompt)
            ? "Представься одной фразой."
            : prompt;

        try
        {
            // Deliberately no fallback here: an admin checking the config wants the primary
            // provider's own answer (or its own error), not a silent switch to the backup.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds));
            var reply = await SendCompletionAsync(config, systemPrompt, userPrompt, cts.Token);

            return string.IsNullOrWhiteSpace(reply)
                ? $"{header}\nМодель вернула пустой ответ (проверьте neuroplayer.max_tokens и модель)."
                : $"{header}\nОтвет модели: {reply}";
        }
        catch (Exception e)
        {
            return $"{header}\nОШИБКА: {e.Message}";
        }
    }

    private sealed class CompletionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<ChatMessageDto> Messages { get; set; } = new();

        [JsonPropertyName("temperature")]
        public float Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("thinking")]
        public ThinkingConfig? Thinking { get; set; }
    }

    private sealed class ThinkingConfig
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "disabled";
    }

    private sealed class ChatMessageDto
    {
        public ChatMessageDto(string role, string content)
        {
            Role = role;
            Content = content;
        }

        [JsonPropertyName("role")]
        public string Role { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }
    }

    private sealed class CompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<CompletionChoice>? Choices { get; set; }
    }

    private sealed class CompletionChoice
    {
        [JsonPropertyName("message")]
        public CompletionMessage? Message { get; set; }
    }

    private sealed class CompletionMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }

        /// <summary>Hidden chain of thought some reasoning models (deepseek-reasoner) return.</summary>
        [JsonPropertyName("reasoning_content")]
        public string? ReasoningContent { get; set; }
    }
}
