// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Chat.Systems;
using Content.Server.NPC.HTN;
using Content.Server.Radio;
using Content.Server.Spawners.Components;
using Content.Shared.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Robust.Shared.Map;
using Content.Shared._Mini.MiniCCVars;
using Content.Shared._Mini.NeuroPlayer;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Preferences;
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
    [Dependency] private readonly StationRecordsSystem _stationRecords = default!;

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
    private int _maxDailyRequests;
    private int _maxTokens;
    private float _temperature;
    private int _timeoutSeconds;
    private bool _thinkingEnabled;

    private DateTime _dailyDate = DateTime.UtcNow.Date;
    private int _dailyRequests;

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
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerMaxDailyRequests, v => _maxDailyRequests = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerMaxTokens, v => _maxTokens = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerTemperature, v => _temperature = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerTimeoutSeconds, v => _timeoutSeconds = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroPlayerThinkingEnabled, v => _thinkingEnabled = v, true);

        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        // EntitySpokeEvent is raised broadcast — this handler sees ALL in-character speech;
        // radio-delivered messages are handled separately via RadioReceiveEvent to avoid doubles.
        SubscribeLocalEvent<EntitySpokeEvent>(OnGlobalSpoke);
        SubscribeLocalEvent<NeuroPlayerComponent, RadioReceiveEvent>(OnRadioReceive);
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

    public void SpawnBots()
    {
        if (!_enabled)
            return;

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

        _sawmill.Info($"Neuro players: spawned {spawned}/{_botCount} bots.");
    }

    private bool TrySpawnBot(NeuroPersonaPrototype persona, EntityCoordinates coords, EntityUid station)
    {
        var profile = HumanoidCharacterProfile.RandomWithSpecies("Human").WithName(persona.Name);
        var mob = _spawning.SpawnPlayerMob(coords, persona.Job, profile, station);

        var comp = EnsureComp<NeuroPlayerComponent>(mob);
        comp.PersonaId = persona.ID;

        var transmitter = EnsureComp<IntrinsicRadioTransmitterComponent>(mob);
        transmitter.Channels.Add(CommonChannelId);
        var receiver = EnsureComp<ActiveRadioComponent>(mob);
        receiver.Channels.Add(CommonChannelId);

        var htn = EnsureComp<HTNComponent>(mob);
        htn.RootTask = new HTNCompoundTask { Task = "IdleCompound" };
        _factions.AddFactions(mob, new HashSet<ProtoId<NpcFactionPrototype>> { "NanoTrasen" });

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

            if ((_transform.GetWorldPosition(botXform) - _transform.GetWorldPosition(speakerXform)).LengthSquared()
                > _hearRadius * _hearRadius)
            {
                continue;
            }

            HandleSpeech(bot, speaker, ev.Message, viaRadio: false);
        }
    }

    private void OnRadioReceive(EntityUid bot, NeuroPlayerComponent comp, ref RadioReceiveEvent ev)
    {
        if (!_enabled)
            return;

        HandleSpeech(bot, ev.MessageSource, ev.OriginalChatMsg.Message, viaRadio: true);
    }

    private void HandleSpeech(EntityUid bot, EntityUid speaker, string message, bool viaRadio)
    {
        if (!_prototypes.TryIndex(Comp<NeuroPlayerComponent>(bot).PersonaId, out var persona))
            return;

        var speakerName = Name(speaker);
        AddContext(bot, speakerName, message);

        // Bots never respond to each other — only to players — so chatter loops are impossible.
        if (HasComp<NeuroPlayerComponent>(speaker))
            return;

        if (!MatchesName(persona.Name, message))
            return;

        TryRespond(bot, persona, speakerName, message, viaRadio);
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

    private void TryRespond(EntityUid bot, NeuroPersonaPrototype persona, string speakerName, string message, bool viaRadio)
    {
        if (string.IsNullOrEmpty(_apiUrl) || string.IsNullOrEmpty(_apiKey))
            return;

        var comp = Comp<NeuroPlayerComponent>(bot);
        if (comp.RequestInFlight || _timing.CurTime < comp.NextAllowedResponse)
            return;

        if (!CheckDailyBudget())
            return;

        comp.RequestInFlight = true;
        _ = RunCompletionAsync(bot, comp, persona, speakerName, message, viaRadio);
    }

    private bool CheckDailyBudget()
    {
        var today = DateTime.UtcNow.Date;
        if (_dailyDate != today)
        {
            _dailyDate = today;
            _dailyRequests = 0;
        }

        if (_maxDailyRequests <= 0 || _dailyRequests < _maxDailyRequests)
        {
            _dailyRequests++;
            return true;
        }

        return false;
    }

    private async Task RunCompletionAsync(
        EntityUid bot,
        NeuroPlayerComponent comp,
        NeuroPersonaPrototype persona,
        string speakerName,
        string message,
        bool viaRadio)
    {
        try
        {
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

            _chat.TrySendInGameICMessage(bot, reply, InGameICChatType.Speak, hideChat: true, hideLog: true);
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Neuro player request failed: {e.Message}");
        }
        finally
        {
            comp.RequestInFlight = false;
            comp.NextAllowedResponse = _timing.CurTime + TimeSpan.FromSeconds(_cooldownSeconds);
        }
    }

    public (bool Enabled, bool ApiConfigured, int Bots, int DailyRequests) GetStatus()
    {
        return (_enabled,
            !string.IsNullOrEmpty(_apiUrl) && !string.IsNullOrEmpty(_apiKey),
            _bots.Count(Exists),
            _dailyRequests);
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
