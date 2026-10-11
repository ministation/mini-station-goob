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
using Robust.Shared.Containers;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Radio.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
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

    private ISawmill _sawmill = default!;
    private readonly HttpClient _httpClient = new();
    private readonly List<EntityUid> _bots = new();

    private bool _enabled;
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

    private float _dangerScanAccumulator;


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
    private static readonly string[] DecompressionCries =
        ["Воздух уходит! Кислород!", "Разгерметизация! Маску, МАСКУ!", "Шлюзы! Воздух кончается!"];
    private static readonly string[] EvacCries =
        ["Бежим на эвак!", "Эвак пристыковался, быстрее!", "За мной на эвак, шевелитесь!", "Погнали на эвак, пока не улетел!"];

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("neuroplayer");

        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerEnabled, v => _enabled = v, true);
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

        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        // EntitySpokeEvent is raised broadcast — this handler sees ALL in-character speech;
        // radio-delivered messages are handled separately via RadioReceiveEvent to avoid doubles.
        SubscribeLocalEvent<EntitySpokeEvent>(OnGlobalSpoke);
        SubscribeLocalEvent<NeuroPlayerComponent, RadioReceiveEvent>(OnRadioReceive);
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
        if (!_enabled || _botCount <= 0)
            return;

        if (!_prototypes.EnumeratePrototypes<NeuroPersonaPrototype>().Any())
        {
            _sawmill.Warning("Neuro players enabled but no neuroPersona prototypes found.");
            return;
        }

        // Wait for maps/stations to settle before spawning.
        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(45), SpawnBots);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _bots.Clear();
    }

    private void OnWarStarted(TypanWarStartedEvent ev)
    {
        // Station war: neuro passengers are hidden for the duration.
        DespawnBots();
    }

    private void OnEvacDocked(EvacShuttleDockedEvent ev)
    {
        if (!_enabled)
            return;

        // Gather passengers on the docked evac shuttle; the first bot leads the group aloud.
        EntityCoordinates? shuttlePoint = null;
        var shuttleMap = MapId.Nullspace;

        var query = EntityQueryEnumerator<EmergencyShuttleComponent, TransformComponent>();
        while (query.MoveNext(out var shuttle, out _, out var xform))
        {
            if (!xform.GridUid.HasValue)
                continue;

            shuttlePoint = xform.Coordinates;
            shuttleMap = xform.MapID;
            break;
        }

        if (shuttlePoint == null)
            return;

        var led = false;
        foreach (var bot in _bots)
        {
            if (!Exists(bot) || !TryComp<NeuroPlayerComponent>(bot, out var comp))
                continue;

            if (Transform(bot).MapID != shuttleMap)
                continue; // Typan chef / CentComm maid stay on their maps

            if (!TryComp<HTNComponent>(bot, out var htn))
                continue;

            var offset = new Vector2(_random.NextFloat(-3f, 3f), _random.NextFloat(-3f, 3f));
            htn.Blackboard.SetValue("NeuroPoint", shuttlePoint.Value.Offset(offset));
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

    public void SpawnBots()
    {
        if (!_enabled)
            return;

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

        var spawned = 0;
        for (var i = 0; i < _botCount; i++)
        {
            var persona = personas[i % personas.Count];
            var coords = points[i % points.Count];

            if (TrySpawnBot(persona, coords, station.Value))
                spawned++;
        }

        // Station janitor: experiments with the cleanbot routine (mop + puddles + decals).
        var janitorPersona = _prototypes.EnumeratePrototypes<NeuroPersonaPrototype>()
            .FirstOrDefault(p => p.ID == "NeuroJanitor");
        if (janitorPersona != null && points.Count > 0)
        {
            var coords = _random.Pick(points);
            if (TrySpawnBot(janitorPersona, coords, station.Value, forcedJob: "Janitor",
                    rootTask: "CleanbotCompound",
                    onSpawned: mob => GiveJanitorMop(mob)))
                spawned++;
        }

        // Typan gets its own chef, CentComm gets its maid.
        var typanStation = FindFactionStation(typan: true);
        if (typanStation != null)
        {
            var persona = personas.FirstOrDefault(p => p.ID == "NeuroTypanChef") ?? personas[0];
            var points2 = FindSpawnPoints(typanStation.Value);
            if (points2.Count > 0 && TrySpawnBot(persona, _random.Pick(points2), typanStation.Value, forcedJob: "TypanChef"))
                spawned++;
        }

        var ccStation = FindFactionStation(centcomm: true);
        if (ccStation != null)
        {
            var persona = personas.FirstOrDefault(p => p.ID == "NeuroCCMaid") ?? personas[0];
            var points3 = FindSpawnPoints(ccStation.Value);
            if (points3.Count > 0 && TrySpawnBot(persona, _random.Pick(points3), ccStation.Value,
                    forcedJob: "Passenger", onSpawned: mob => GiveMaidOutfit(mob)))
                spawned++;
        }

        _sawmill.Info($"Neuro players: spawned {spawned} bots.");
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
        var job = forcedJob
            ?? (_random.Prob(0.4f)
                ? _random.Pick(BasicJobPrototypes)
                : (ProtoId<JobPrototype>) "Passenger");

        var profile = HumanoidCharacterProfile.RandomWithSpecies("Human");
        var mob = _spawning.SpawnPlayerMob(coords, job, profile, station);

        var comp = EnsureComp<NeuroPlayerComponent>(mob);
        comp.PersonaId = persona.ID;

        var transmitter = EnsureComp<IntrinsicRadioTransmitterComponent>(mob);
        transmitter.Channels.Add(CommonChannelId);
        var receiver = EnsureComp<ActiveRadioComponent>(mob);
        receiver.Channels.Add(CommonChannelId);

        var htn = EnsureComp<HTNComponent>(mob);
        htn.RootTask = new HTNCompoundTask { Task = rootTask ?? "IdleCompound" };
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
                    persona.Job, null, null, profile, records);
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

            if (speaker == bot)
                continue;

            var botXform = Transform(bot);
            var speakerXform = Transform(speaker);
            if (botXform.MapID != speakerXform.MapID)
                continue;

            var distance = (_transform.GetWorldPosition(botXform) - _transform.GetWorldPosition(speakerXform)).Length();
            if (distance > _hearRadius)
                continue;

            HandleSpeech(bot, speaker, ev.Message, viaRadio: false, distance);
        }
    }

    private void OnRadioReceive(EntityUid bot, NeuroPlayerComponent comp, ref RadioReceiveEvent ev)
    {
        if (!_enabled)
            return;

        HandleSpeech(bot, ev.MessageSource, ev.OriginalChatMsg.Message, viaRadio: true);
    }

    private void HandleSpeech(EntityUid bot, EntityUid speaker, string message, bool viaRadio, float distance = -1f)
    {
        if (!_prototypes.TryIndex(Comp<NeuroPlayerComponent>(bot).PersonaId, out var persona))
            return;

        var speakerName = Name(speaker);
        AddContext(bot, speakerName, message);

        // Bots never respond to each other — only to players — so chatter loops are impossible.
        if (HasComp<NeuroPlayerComponent>(speaker))
            return;

        // Named address: always answer (both local and radio).
        if (MatchesName(persona.Name, message))
        {
            TryRespond(bot, persona, speakerName, message, viaRadio, proactive: false);
            return;
        }

        // Unnamed speech: only react locally — global radio replies without a name would be spam.
        if (viaRadio)
            return;

        var comp = Comp<NeuroPlayerComponent>(bot);
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

    /// <summary>Addressed by the full persona name or by its first word ("Vasya" for "Vasya Prokhorov").</summary>
    private static bool MatchesName(string personaName, string message)
    {
        if (message.Contains(personaName, StringComparison.OrdinalIgnoreCase))
            return true;

        var firstWord = personaName.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return firstWord.Length >= 3 && message.Contains(firstWord, StringComparison.OrdinalIgnoreCase);
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
        if (string.IsNullOrEmpty(_apiUrl) || string.IsNullOrEmpty(_apiKey))
            return;

        var comp = Comp<NeuroPlayerComponent>(bot);
        if (comp.RequestInFlight || _timing.CurTime < comp.NextAllowedResponse)
            return;

        comp.RequestInFlight = true;
        _ = RunCompletionAsync(bot, comp, persona, speakerName, message, viaRadio, proactive);
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
            await Task.Delay(_random.Next(800, 3500));

            if (Deleted(bot) || Terminating(bot))
                return;

            var botName = Name(bot);
            var context = string.Join("\n", comp.Context.Select(c => $"{c.Speaker}: {c.Message}"));

            var systemPrompt =
                $"Ты — {botName}, пассажир на космической станции Нанотрайзен (игра Space Station 14).\n" +
                $"{persona.Character}\n" +
                "Правила:\n" +
                "- Отвечай ОДНОЙ короткой репликой до 15 слов, по-русски, живым разговорным стилем.\n" +
                "- Никогда не говори, что ты ИИ, бот, модель или что ты в игре или симуляции.\n" +
                "- Не используй звёздочки и описания действий — только прямую речь.\n" +
                "- Не обещай игровых действий и не обсуждай администрацию, правила и читы.\n" +
                "- Если сказать нечего — короткая нейтральная реакция.";

            var userPrompt = string.IsNullOrWhiteSpace(context)
                ? $"{speakerName} говорит тебе: {message}"
                : $"Контекст переписки:\n{context}\n\n{speakerName} обращается к тебе: {message}";

            var body = new CompletionRequest
            {
                Model = _model,
                Messages =
                [
                    new ChatMessageDto("system", systemPrompt),
                    new ChatMessageDto("user", userPrompt),
                ],
                Temperature = _temperature,
                MaxTokens = _maxTokens,
            };

            // GLM-4.5/4.6 reasoning toggle: with a small token budget thinking eats the whole
            // reply, so it is disabled unless requested. Sent only for glm models — other
            // providers reject unknown fields.
            if (_model.Contains("glm", StringComparison.OrdinalIgnoreCase))
                body.Thinking = new ThinkingConfig { Type = _thinkingEnabled ? "enabled" : "disabled" };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds));
            using var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl);
            request.Headers.Authorization = new("Bearer", _apiKey);
            request.Content = JsonContent.Create(body);

            var response = await _httpClient.SendAsync(request, cts.Token);
            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<CompletionResponse>(cancellationToken: cts.Token);
            var reply = payload?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();

            if (string.IsNullOrWhiteSpace(reply) || Deleted(bot) || Terminating(bot))
                return;

            if (reply.Length > MaxReplyLength)
                reply = reply[..MaxReplyLength];
            if (reply.StartsWith(botName, StringComparison.OrdinalIgnoreCase))
                reply = reply[botName.Length..].TrimStart(':', ' ', '-');

            if (viaRadio)
                reply = ";" + reply;

            _chat.TrySendInGameICMessage(bot, reply, InGameICChatType.Speak, hideChat: false, hideLog: true);
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

        _dangerScanAccumulator += frameTime;
        if (_dangerScanAccumulator >= 2f)
        {
            _dangerScanAccumulator = 0f;
            ScanDangers();
        }

        var now = _timing.CurTime;
        for (var i = _bots.Count - 1; i >= 0; i--)
        {
            var bot = _bots[i];
            if (!Exists(bot) || !TryComp<NeuroPlayerComponent>(bot, out var comp))
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

    private void RestoreIdle(EntityUid bot)
    {
        if (!TryComp<HTNComponent>(bot, out var htn))
            return;

        htn.RootTask = new HTNCompoundTask { Task = "IdleCompound" };
        _htn.Replan(htn);
    }

    private void ScanDangers()
    {
        foreach (var bot in _bots)
        {
            if (!Exists(bot) || !TryComp<NeuroPlayerComponent>(bot, out var comp) || comp.DangerUntil != null)
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

            var botPos = _transform.GetWorldPosition(bot);
            var botMap = Transform(bot).MapID;

            var query = EntityQueryEnumerator<FlammableComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var flammable, out var xform))
            {
                if (flammable.FireStacks <= 0 || xform.MapID != botMap)
                    continue;

                if ((xform.WorldPosition - botPos).Length() > 6f)
                    continue;

                TriggerDanger(bot, comp, xform.WorldPosition, FireCries);
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
                Cry(victim, botComp, PainCries);

            return;
        }

        // Someone else got hurt badly nearby: run away from the fight.
        if (total < 15f || (ev.Origin is { } fightOrigin && HasComp<NeuroPlayerComponent>(fightOrigin)))
            return;

        var victimPos = _transform.GetWorldPosition(victim);
        foreach (var bot in _bots)
        {
            if (!Exists(bot) || !TryComp<NeuroPlayerComponent>(bot, out var comp) || comp.DangerUntil != null)
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

        comp.DangerUntil = _timing.CurTime + TimeSpan.FromSeconds(12);
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
            for (var dist = 10; dist <= 25; dist += 5)
            {
                var tile = centerTile + (away * dist).Floored();
                if (_map.TryGetTileRef(grid, mapGrid, tile, out var tileRef) && !tileRef.Tile.IsEmpty)
                    return _map.GridTileToLocal(grid, mapGrid, tile);
            }
        }

        return botXform.Coordinates.Offset(away * 18f);
    }

    private void Cry(EntityUid bot, NeuroPlayerComponent comp, string[] phrases)
    {
        if (_timing.CurTime < comp.NextCry)
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
            var query = EntityQueryEnumerator<ClothingComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out _, out var xform))
            {
                if (xform.MapID != Transform(bot).MapID)
                    continue;

                // Only items lying free on the grid, within arm's reach.
                if (_container.IsEntityInContainer(uid))
                    continue;

                if ((Prototype(uid)?.ID ?? string.Empty).IndexOf("Hardsuit", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if ((_transform.GetWorldPosition(bot) - _transform.GetWorldPosition(xform)).Length() > 1.5f)
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
        var systemPrompt =
            $"Ты — {botName}, пассажир на космической станции Нанотрайзен (игра Space Station 14).\n" +
            $"{persona.Character}\n" +
            "Правила:\n" +
            "- Отвечай ОДНОЙ короткой репликой до 15 слов, по-русски, живым разговорным стилем.\n" +
            "- Никогда не говори, что ты ИИ, бот, модель или что ты в игре или симуляции.\n" +
            "- Не используй звёздочки и описания действий — только прямую речь.\n" +
            "- Не обещай игровых действий и не обсуждай администрацию, правила и читы.\n" +
            "- Если сказать нечего — короткая нейтральная реакция.";

        var body = new CompletionRequest
        {
            Model = _model,
            Messages =
            [
                new ChatMessageDto("system", systemPrompt),
                new ChatMessageDto("user", userPrompt),
            ],
            Temperature = _temperature,
            MaxTokens = _maxTokens,
        };

        // GLM-4.5/4.6 reasoning toggle: with a small token budget thinking eats the whole
        // reply, so it is disabled unless requested. Sent only for glm models — other
        // providers reject unknown fields.
        if (_model.Contains("glm", StringComparison.OrdinalIgnoreCase))
            body.Thinking = new ThinkingConfig { Type = _thinkingEnabled ? "enabled" : "disabled" };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds));
        using var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl);
        request.Headers.Authorization = new("Bearer", _apiKey);
        request.Content = JsonContent.Create(body);

        var response = await _httpClient.SendAsync(request, cts.Token);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CompletionResponse>(cancellationToken: cts.Token);
        var reply = payload?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();

        if (string.IsNullOrWhiteSpace(reply))
            return null;

        if (reply.Length > MaxReplyLength)
            reply = reply[..MaxReplyLength];

        return reply;
    }

    public (bool Enabled, bool ApiConfigured, int Bots) GetStatus()
    {
        return (_enabled,
            !string.IsNullOrEmpty(_apiUrl) && !string.IsNullOrEmpty(_apiKey),
            _bots.Count(Exists));
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
    }
}
