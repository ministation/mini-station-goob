// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

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
using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared._EinsteinEngines.Language.Components;
using Content.Shared._EinsteinEngines.Language.Systems;
using Content.Shared.Ghost;
using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Radio.Components;
using Content.Shared.Interaction;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Prying.Components;
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
public sealed class NeuroPlayerSystem : EntitySystem
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

    /// <summary>Bots notice fires within this many tiles.</summary>
    private const float FireAlertRadius = 12f;

    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
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

    private ISawmill _sawmill = default!;
    private readonly HttpClient _httpClient = new();
    private readonly List<EntityUid> _bots = new();

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

    private int _requestsOk;
    private int _requestsFailed;
    private int _fallbacks;
    private int _requestsRateLimited;
    private int _blockedReplies;

    private float _dangerScanAccumulator;
    private float _seekAccumulator;

    /// <summary>Round bookkeeping: bots are spawned once per round, with retries while they are missing.</summary>
    private bool _spawnedThisRound;
    private TimeSpan _roundStartTime;
    private TimeSpan _lastSpawnAttempt;

    /// <summary>Bots were despawned because a station war is running; they come back when it ends.</summary>
    private bool _warSuppressed;

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
        SubscribeLocalEvent<DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<TypanWarStartedEvent>(OnWarStarted);
        SubscribeLocalEvent<EvacShuttleDockedEvent>(OnEvacDocked);
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
        _spawnedThisRound = false;
        _warSuppressed = false;
        _roundStartTime = TimeSpan.Zero;
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
            htn.RootTask = new HTNCompoundTask { Task = "NeuroVisitCompound" };
            _htn.Replan(htn);
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
            _sawmill.Warning("Neuro players: no station found to spawn on.");
            return;
        }

        var points = FindSpawnPoints(station.Value);
        if (points.Count == 0)
        {
            _sawmill.Warning("Neuro players: no late-join spawn points found.");
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

        foreach (var (personaId, job, rootTask) in mainStaff)
        {
            var persona = personas.FirstOrDefault(p => p.ID == personaId) ?? personas[0];
            var coords = _random.Pick(points);
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
            if (!TrySpawnBot(persona, _random.Pick(points), station.Value))
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
            _spawnedThisRound = true;

        _sawmill.Info($"Neuro players: spawned {spawned} bots (bot_count={_botCount}).");
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

        var transmitter = EnsureComp<IntrinsicRadioTransmitterComponent>(mob);
        transmitter.Channels.Add(CommonChannelId);
        var receiver = EnsureComp<ActiveRadioComponent>(mob);
        receiver.Channels.Add(CommonChannelId);

        var htn = EnsureComp<HTNComponent>(mob);
        htn.RootTask = new HTNCompoundTask { Task = rootTask ?? "IdleCompound" };

        // Mini: walk like a crew member — pry airlocks open and let the planner
        // route through doors instead of beelining into walls.
        EnsureComp<PryingComponent>(mob);
        htn.Blackboard.SetValue(NPCBlackboard.NavPry, true);
        _factions.AddFactions(mob, new HashSet<ProtoId<NpcFactionPrototype>> { "NanoTrasen" });

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
    }

    private EntityUid? FindStation()
    {
        var query = EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out var uid, out var data))
        {
            if (data.Grids.Count > 0)
                return uid;
        }

        return null;
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
        CancellationToken cancel)
    {
        try
        {
            var reply = await SendCompletionAsync(primary, systemPrompt, userPrompt, cancel);
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
                var reply = await SendCompletionAsync(fallback, systemPrompt, userPrompt, fallbackCts.Token);
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
        CancellationToken cancel)
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
            MaxTokens = _maxTokens,
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

        var now = _timing.CurTime;
        for (var i = _bots.Count - 1; i >= 0; i--)
        {
            var bot = _bots[i];
            if (!TryComp<NeuroPlayerComponent>(bot, out var comp))
            {
                _bots.RemoveAt(i);
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

        foreach (var bot in _bots)
        {
            if (!IsBotUp(bot) || !TryComp<NeuroPlayerComponent>(bot, out var comp))
                continue;

            if (comp.DangerUntil != null || comp.HostileUntil != null || comp.VisitUntil != null)
                continue;

            var nearest = FindNearestPlayer(bot);
            if (nearest == null)
                continue;

            var (coords, distance) = nearest.Value;

            // Somebody is already close enough — no need to march anywhere.
            if (distance <= _seekRadius)
                continue;

            if (!TryComp<HTNComponent>(bot, out var htn))
                continue;

            htn.Blackboard.SetValue("NeuroPoint", coords);
            htn.RootTask = new HTNCompoundTask { Task = "NeuroVisitCompound" };
            _htn.Replan(htn);
            comp.VisitUntil = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(60, 120));
        }
    }

    /// <summary>Nearest living player (never a ghost or another bot) with the distance to them.</summary>
    private (EntityCoordinates Coords, float Distance)? FindNearestPlayer(EntityUid bot)
    {
        var botXform = Transform(bot);
        var botPos = _transform.GetWorldPosition(botXform);

        EntityCoordinates? best = null;
        var bestDistance = float.MaxValue;

        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (uid == bot || xform.MapID != botXform.MapID || HasComp<GhostComponent>(uid))
                continue;

            var distance = (xform.WorldPosition - botPos).Length();
            if (distance > MaxSeekRange || distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = xform.Coordinates;
        }

        return best == null ? null : (best.Value, bestDistance);
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
            htn.RootTask = new HTNCompoundTask { Task = "NeuroVisitCompound" };
            _htn.Replan(htn);
            comp.VisitUntil = _timing.CurTime + TimeSpan.FromSeconds(420);
            return;
        }

        htn.RootTask = new HTNCompoundTask { Task = "IdleCompound" };
        _htn.Replan(htn);
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

            // Brave enough: engage hostile-faction mobs that wander too close.
            var hostiles = _factions.GetNearbyHostiles((bot, CompOrNull<NpcFactionMemberComponent>(bot), null), 8f);
            if (hostiles.Any())
            {
                if (TryComp<HTNComponent>(bot, out var attackHtn) && attackHtn.RootTask.Task != "SimpleHumanoidHostileCompound")
                {
                    attackHtn.RootTask = new HTNCompoundTask { Task = "SimpleHumanoidHostileCompound" };
                    _htn.Replan(attackHtn);
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
        }
    }

    private void OnDamageChanged(DamageChangedEvent ev)
    {
        if (!_enabled || !ev.DamageIncreased || ev.DamageDelta == null)
            return;

        var victim = ev.Damageable.Owner;
        if (!Exists(victim))
            return;

        var total = (float) ev.DamageDelta.GetTotal();

        // Self-damage: pain cries and depressurization handling.
        if (TryComp<NeuroPlayerComponent>(victim, out var botComp))
        {
            // Already down (dying/dead): no more fleeing, no more paid replies from a corpse.
            if (!IsBotUp(victim))
                return;

            if (ev.DamageDelta.DamageDict.TryGetValue("Airloss", out var airloss) && airloss >= 3f)
            {
                // Decompression: scream, enable internals from own inventory, best-effort grab a hardsuit, run.
                Cry(victim, botComp, DecompressionCries);
                TryEnableInternals(victim);
                TryGrabHardsuit(victim);
                TriggerDanger(victim, botComp, _transform.GetWorldPosition(victim), null, cry: false);
                return;
            }

            if (total >= 10f)
            {
                Cry(victim, botComp, PainCries);

                // Mini: significant damage should trigger fleeing immediately,
                // not just a cry; run from the attacker if one is known.
                var dangerPos = ev.Origin is { } origin && origin != victim && Exists(origin)
                    ? _transform.GetWorldPosition(origin)
                    : _transform.GetWorldPosition(victim);
                TriggerDanger(victim, botComp, dangerPos, null, cry: false);
            }

            return;
        }

        // Someone else got hurt badly nearby: run away from the fight.
        if (total < 15f || (ev.Origin is { } fightOrigin && HasComp<NeuroPlayerComponent>(fightOrigin)))
            return;

        var victimPos = _transform.GetWorldPosition(victim);
        foreach (var bot in _bots)
        {
            if (!TryComp<NeuroPlayerComponent>(bot, out var comp) || comp.DangerUntil != null || !IsBotUp(bot))
                continue;

            if (Transform(bot).MapID != Transform(victim).MapID)
                continue;

            if ((_transform.GetWorldPosition(bot) - victimPos).Length() > 6f)
                continue;

            TriggerDanger(bot, comp, victimPos, FightCries);
        }
    }

    private void TriggerDanger(EntityUid bot, NeuroPlayerComponent comp, Vector2 dangerPos, string[]? cries, bool cry = true)
    {
        if (!TryComp<HTNComponent>(bot, out var htn))
            return;

        htn.Blackboard.SetValue("NeuroPoint", ComputeFleePoint(bot, dangerPos));
        htn.RootTask = new HTNCompoundTask { Task = "NeuroFleeCompound" };
        _htn.Replan(htn);

        comp.DangerUntil = _timing.CurTime + TimeSpan.FromSeconds(20);
        comp.VisitUntil = null;
        comp.PoiAccumulator = 0f;

        if (cry && cries != null)
            Cry(bot, comp, cries);
    }

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
            for (var dist = 8; dist <= 30; dist += 4)
            {
                var tile = centerTile + (away * dist).Floored();
                if (_map.TryGetTileRef(grid, mapGrid, tile, out var tileRef) && !tileRef.Tile.IsEmpty)
                    return _map.GridTileToLocal(grid, mapGrid, tile);
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
        _chat.TrySendInGameICMessage(bot, _random.Pick(phrases), InGameICChatType.Speak, hideChat: false, hideLog: true);
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

        htn.Blackboard.SetValue("NeuroPoint", point.Value);
        htn.RootTask = new HTNCompoundTask { Task = "NeuroVisitCompound" };
        _htn.Replan(htn);
        comp.VisitUntil = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(120, 240));
    }

    private EntityCoordinates? FindPointOfInterest(EntityUid bot)
    {
        var points = new List<EntityCoordinates>();
        var botMap = Transform(bot).MapID;

        var bars = EntityQueryEnumerator<BarSignComponent, TransformComponent>();
        while (bars.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapID != botMap)
                continue;

            points.Add(xform.Coordinates);
        }

        if (points.Count > 0)
            return _random.Pick(points);

        // No bar on this map: hang around late-join points (arrivals / halls).
        var station = FindStation();
        if (station != null)
            points.AddRange(FindSpawnPoints(station.Value));

        return points.Count > 0 ? _random.Pick(points) : null;
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
        var botName = Name(bot);

        foreach (var word in _blockedWords)
        {
            if (!ContainsWordWithSuffix(reply, word, MaxNameSuffixLength))
                continue;

            _sawmill.Warning($"Neuro player {botName}: reply dropped by neuroplayer.blocked_words ('{word}'): {reply}");
            Interlocked.Increment(ref _blockedReplies);
            _adminLog.Add(LogType.Chat, LogImpact.Medium,
                $"Neuro player {botName} reply blocked by blocked_words ('{word}'): {reply}");
            return false;
        }

        if (_blockAiTalk && AdmitsBeingAi(reply))
        {
            _sawmill.Info($"Neuro player {botName}: out-of-character reply dropped: {reply}");
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
            var muted = comp.Muted
                ? ", мут"
                : comp.MutedUntil != TimeSpan.Zero && _timing.CurTime < comp.MutedUntil
                    ? $", мут до {comp.MutedUntil - _timing.CurTime:hh\\:mm\\:ss}"
                    : string.Empty;

            lines.Add($"- {Name(bot)} [{comp.PersonaId.Id}] {state}{muted}");
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
