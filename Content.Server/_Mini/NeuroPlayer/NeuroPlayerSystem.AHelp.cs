// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Systems;
using Content.Server.GameTicking;
using Content.Shared._Mini.MiniCCVars;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Mini.NeuroPlayer;

/// <summary>
/// Admin-help (ahelp) assistant: while no admins are online, an LLM answers the player right in the
/// ahelp channel, and the exchange goes to the admin log for the staff to read later.
/// </summary>
/// <remarks>
/// Reuses the neuro player LLM plumbing (provider presets, fallback provider, blocked words,
/// screen) through the partial class, but keeps its own request budget so the IC bots cannot
/// starve the support channel.
/// </remarks>
public sealed partial class NeuroPlayerSystem
{
    /// <summary>Hard ceiling for an ahelp answer, even if neuroahelp.max_reply_chars is set higher.</summary>
    private const int MaxAHelpReplyLength = 400;

    /// <summary>Mini: ticket wording that means "a human has to look at this".</summary>
    private static readonly string[] HumanAdminWords =
    [
        "админ", "бан", "разбан", "апелляц", "чит", "грифер", "жалоб", "наруш",
        "верните", "украл", "убили", "тикет", "наказ",
    ];

    /// <summary>Colour of the assistant's name in the ahelp window.</summary>
    private const string AHelpColor = "#8ab4f8";

    private enum AHelpSpeaker : byte
    {
        Player,
        Staff,
        Assistant,
    }

    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly BwoinkSystem _bwoink = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;

    private bool _ahelpEnabled;
    private string _ahelpName = "Нейропомощник";
    private int _ahelpDelaySeconds;
    private int _ahelpCooldownSeconds;
    private int _ahelpAdminSilenceSeconds;
    private int _ahelpMaxContext;
    private int _ahelpMaxTokens;
    private int _ahelpMaxReplyChars;
    private int _ahelpRequestsPerMinute;
    private string _ahelpExtraPrompt = string.Empty;
    private string _ahelpKnowledgeFile = string.Empty;
    private int _ahelpKnowledgeMaxChars;

    private string _ahelpKnowledgeCache = string.Empty;
    private DateTime _ahelpKnowledgeStamp = DateTime.MinValue;
    private bool _ahelpKnowledgeWarned;

    private readonly Dictionary<NetUserId, List<(AHelpSpeaker Speaker, string Text)>> _ahelpContext = new();
    private readonly Dictionary<NetUserId, TimeSpan> _ahelpLastStaffMessage = new();
    private readonly Dictionary<NetUserId, TimeSpan> _ahelpLastReply = new();
    private readonly List<PendingAHelp> _ahelpQueue = new();
    private readonly Queue<TimeSpan> _ahelpRequestTimes = new();

    /// <summary>
    /// Assistant lines produced by finished requests. The request task runs on a thread pool thread,
    /// so the shared ahelp context is only touched from the game loop: the task enqueues, Update drains.
    /// </summary>
    private readonly ConcurrentQueue<(NetUserId UserId, string Text)> _ahelpAssistantLines = new();

    /// <summary>A player message waiting out the grace period before the assistant steps in.</summary>
    private sealed record PendingAHelp(NetUserId UserId, string PlayerName, TimeSpan AskedAt, TimeSpan ReadyAt);

    private void InitializeAHelp()
    {
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpEnabled, v => _ahelpEnabled = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpName, v => _ahelpName = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpDelaySeconds, v => _ahelpDelaySeconds = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpCooldownSeconds, v => _ahelpCooldownSeconds = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpAdminSilenceSeconds, v => _ahelpAdminSilenceSeconds = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpMaxContext, v => _ahelpMaxContext = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpMaxTokens, v => _ahelpMaxTokens = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpMaxReplyChars, v => _ahelpMaxReplyChars = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpRequestsPerMinute, v => _ahelpRequestsPerMinute = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpExtraPrompt, v => _ahelpExtraPrompt = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpKnowledgeFile, v => _ahelpKnowledgeFile = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpKnowledgeMaxChars, v => _ahelpKnowledgeMaxChars = v, true);

        SubscribeNetworkEvent<SharedBwoinkSystem.BwoinkTextMessage>(OnAHelpTextMessage);
        // RoundRestartCleanupEvent is already subscribed in the main Initialize(); the ahelp state is
        // reset from that handler via ResetAHelpState() — a second subscription throws at startup.
    }

    /// <summary>Drops per-round ahelp state. Called from the system's RoundRestartCleanupEvent handler.</summary>
    private void ResetAHelpState()
    {
        _ahelpContext.Clear();
        _ahelpLastStaffMessage.Clear();
        _ahelpLastReply.Clear();
        _ahelpQueue.Clear();
        _ahelpRequestTimes.Clear();
        _ahelpKnowledgeWarned = false;
    }

    /// <summary>
    /// Sees every ahelp line (players and admins). Player lines queue an answer, staff lines make
    /// the assistant back off the ticket.
    /// </summary>
    private void OnAHelpTextMessage(SharedBwoinkSystem.BwoinkTextMessage message, EntitySessionEventArgs args)
    {
        if (!_ahelpEnabled)
            return;

        var session = args.SenderSession;

        // A player always writes into their own channel; an admin answering writes into somebody
        // else's. Star-mute messages are admin-only, so they are ignored either way.
        var fromPlayer = message.UserId == session.UserId && !message.AdminOnly;
        var senderAdmin = _adminManager.GetAdminData(session);
        var fromStaff = !fromPlayer && senderAdmin != null;

        if (!fromPlayer && !fromStaff)
            return;

        // Admins asking in their own channel: nothing to answer.
        if (fromPlayer && senderAdmin != null)
            return;

        AddAHelpContext(message.UserId, fromPlayer ? AHelpSpeaker.Player : AHelpSpeaker.Staff, message.Text);

        if (fromStaff)
        {
            _ahelpLastStaffMessage[message.UserId] = _timing.CurTime;
            return;
        }

        // Somebody is already on duty — no need for the assistant.
        if (IsAdminHandlingTicket(message.UserId))
            return;

        // One pending answer per player, always for the newest message.
        _ahelpQueue.RemoveAll(pending => pending.UserId == message.UserId);
        _ahelpQueue.Add(new PendingAHelp(
            message.UserId,
            session.Name,
            _timing.CurTime,
            _timing.CurTime + TimeSpan.FromSeconds(Math.Max(0, _ahelpDelaySeconds))));
    }

    /// <summary>Whether any non-de-adminned admin is connected right now.</summary>
    private bool HasActiveAdmins()
    {
        return _adminManager.ActiveAdmins.Any();
    }

    /// <summary>
    /// Whether a live admin is actually handling this ticket: somebody is online AND wrote in this
    /// channel recently (neuroahelp.admin_silence_seconds). An online but silent admin — busy with
    /// another ticket, AFK, or simply not answering — no longer blocks the assistant.
    /// </summary>
    private bool IsAdminHandlingTicket(NetUserId userId)
    {
        if (!HasActiveAdmins())
            return false;

        if (!_ahelpLastStaffMessage.TryGetValue(userId, out var staffAt))
            return false;

        var silence = TimeSpan.FromSeconds(Math.Max(0, _ahelpAdminSilenceSeconds));
        return _timing.CurTime - staffAt < silence;
    }

    private void AddAHelpContext(NetUserId userId, AHelpSpeaker speaker, string text)
    {
        if (!_ahelpContext.TryGetValue(userId, out var context))
        {
            context = new List<(AHelpSpeaker, string)>();
            _ahelpContext[userId] = context;
        }

        context.Add((speaker, text));
        while (context.Count > _ahelpMaxContext)
            context.RemoveAt(0);
    }

    /// <summary>
    /// Grace period expired: re-check that nobody picked the ticket up, then ask the model.
    /// </summary>
    private void TickAHelp()
    {
        if (!_ahelpEnabled)
            return;

        // Lines from finished requests land in the context here, on the game loop thread.
        while (_ahelpAssistantLines.TryDequeue(out var line))
            AddAHelpContext(line.UserId, AHelpSpeaker.Assistant, line.Text);

        if (_ahelpQueue.Count == 0)
            return;

        var now = _timing.CurTime;

        for (var i = _ahelpQueue.Count - 1; i >= 0; i--)
        {
            var pending = _ahelpQueue[i];
            if (now < pending.ReadyAt)
                continue;

            _ahelpQueue.RemoveAt(i);

            // An admin picked the ticket up (or came online and answered) while it was waiting.
            if (IsAdminHandlingTicket(pending.UserId))
                continue;

            if (_ahelpLastStaffMessage.TryGetValue(pending.UserId, out var staffAt) && staffAt > pending.AskedAt)
                continue;

            if (_ahelpLastReply.TryGetValue(pending.UserId, out var repliedAt) &&
                now - repliedAt < TimeSpan.FromSeconds(Math.Max(0, _ahelpCooldownSeconds)))
            {
                continue;
            }

            if (!_playerManager.TryGetSessionById(pending.UserId, out var session))
                continue;

            if (!TryTakeAHelpRequestSlot())
                continue;

            // Mark the request immediately so a spamming player cannot queue several at once.
            _ahelpLastReply[pending.UserId] = now;
            _ = AnswerAHelpAsync(session, pending.UserId);
        }
    }

    /// <summary>Sliding-window budget for the assistant (neuroahelp.requests_per_minute, 0 = unlimited).</summary>
    private bool TryTakeAHelpRequestSlot()
    {
        if (_ahelpRequestsPerMinute <= 0)
            return true;

        var now = _timing.CurTime;
        while (_ahelpRequestTimes.Count > 0 && now - _ahelpRequestTimes.Peek() > TimeSpan.FromMinutes(1))
            _ahelpRequestTimes.Dequeue();

        if (_ahelpRequestTimes.Count >= _ahelpRequestsPerMinute)
        {
            _sawmill.Info("Neuro ahelp: request skipped by neuroahelp.requests_per_minute.");
            return false;
        }

        _ahelpRequestTimes.Enqueue(now);
        return true;
    }

    private async Task AnswerAHelpAsync(ICommonSession session, NetUserId userId)
    {
        try
        {
            var config = ResolveProvider();
            if (!config.IsConfigured)
            {
                WarnUnconfigured(config);
                return;
            }

            var startedAt = _timing.CurTime;
            var systemPrompt = BuildAHelpSystemPrompt();
            var userPrompt = BuildAHelpUserPrompt(userId, session.Name, session);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds));
            var reply = await SendCompletionWithFallbackAsync(config, systemPrompt, userPrompt, cts.Token,
                maxTokens: _ahelpMaxTokens);

            if (string.IsNullOrWhiteSpace(reply))
                return;

            // Mini: same cleanup the IC bots get — no long dashes, single spaces, then a hard cap at
            // a sentence boundary: answers in ahelp must stay short.
            reply = reply.Replace('—', '-').Replace('–', '-');
            reply = string.Join(' ', reply.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            reply = TrimAHelpReply(reply, Math.Min(_ahelpMaxReplyChars, MaxAHelpReplyLength));

            // Blocked words still apply, but unlike the IC bots the assistant is supposed to admit
            // that it is an AI, so that part of the screen is off.
            if (!TryScreenText($"ahelp/{session.Name}", reply, blockAiTalk: false))
                return;

            // An admin could have answered while the model was thinking.
            if (_ahelpLastStaffMessage.TryGetValue(userId, out var staffAt) && staffAt > startedAt)
            {
                _sawmill.Info($"Neuro ahelp: admin answered {session.Name} first, reply dropped.");
                return;
            }

            if (!_playerManager.TryGetSessionById(userId, out var stillConnected))
                return;

            var text =
                $"[color={AHelpColor}][bold]{FormattedMessage.EscapeText(_ahelpName)}[/bold][/color]: " +
                FormattedMessage.EscapeText(reply);

            _bwoink.SendPlayerChannelSystemMessage(userId, text);

            // Mini: the assistant cannot punish and does not promise anything, but it can make a
            // ticket that clearly needs a human stand out in the admin log.
            var needsAdmin = NeedsHumanAdmin(userId);
            _adminLog.Add(LogType.AdminMessage, needsAdmin ? LogImpact.Medium : LogImpact.Low,
                $"{(needsAdmin ? "[нужен админ] " : "")}Neuro ahelp answered {stillConnected.Name}: {reply}");

            // Keep our own line in context so follow-up questions make sense. The context itself is
            // game-loop state, so the line is handed over through a queue instead of being added here.
            _ahelpAssistantLines.Enqueue((userId, reply));
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Neuro ahelp request failed: {e.Message}");
        }
    }

    /// <summary>
    /// Mini: whether this ticket clearly needs a human (grief, cheats, bans, appeals, lost items).
    /// The assistant cannot act, but it flags such answers in the admin log at Medium impact.
    /// </summary>
    private bool NeedsHumanAdmin(NetUserId userId)
    {
        if (!_ahelpContext.TryGetValue(userId, out var context))
            return false;

        foreach (var (speaker, text) in context)
        {
            if (speaker == AHelpSpeaker.Assistant)
                continue;

            foreach (var word in HumanAdminWords)
            {
                if (ContainsWordWithSuffix(text, word, MaxNameSuffixLength))
                    return true;
            }
        }

        return false;
    }

    /// <summary>Answer length cap: cut at the last sentence end (or word) inside the budget.</summary>
    private static string TrimAHelpReply(string reply, int maxChars)
    {
        if (maxChars <= 0 || reply.Length <= maxChars)
            return reply;

        var window = reply[..maxChars];

        var sentence = window.LastIndexOfAny(['.', '!', '?', '…']);
        if (sentence > maxChars / 3)
            return window[..(sentence + 1)].TrimEnd();

        var space = window.LastIndexOf(' ');
        return space > maxChars / 3 ? window[..space].TrimEnd() + "…" : window.TrimEnd();
    }

    /// <summary>
    /// Facts about this build, from neuroahelp.knowledge_file. Cached until the file changes, so the
    /// owner can edit it on a live server without a restart.
    /// </summary>
    private string GetAHelpKnowledge()
    {
        if (string.IsNullOrWhiteSpace(_ahelpKnowledgeFile))
            return string.Empty;

        try
        {
            var path = Path.GetFullPath(_ahelpKnowledgeFile);

            if (!File.Exists(path))
            {
                WarnAHelpKnowledge($"knowledge file '{path}' not found");
                return string.Empty;
            }

            var stamp = File.GetLastWriteTimeUtc(path);
            if (stamp == _ahelpKnowledgeStamp)
                return _ahelpKnowledgeCache;

            var text = File.ReadAllText(path);
            if (text.Length > _ahelpKnowledgeMaxChars)
                text = text[.._ahelpKnowledgeMaxChars];

            _ahelpKnowledgeStamp = stamp;
            _ahelpKnowledgeCache = text;
            return text;
        }
        catch (Exception e)
        {
            WarnAHelpKnowledge($"cannot read knowledge file '{_ahelpKnowledgeFile}': {e.Message}");
            return string.Empty;
        }
    }

    private void WarnAHelpKnowledge(string reason)
    {
        if (_ahelpKnowledgeWarned)
            return;

        _ahelpKnowledgeWarned = true;
        _sawmill.Warning($"Neuro ahelp: {reason}.");
    }

    private string BuildAHelpSystemPrompt()
    {
        var discord = _cfg.GetCVar(CCVars.InfoLinksDiscord);
        var wiki = _cfg.GetCVar(CCVars.InfoLinksWiki);

        var prompt =
            $"Ты — «{_ahelpName}», ИИ-помощник в админ-хелпе (ahelp) сервера «Мини-Станция» — русский МРП-сервер по Space Station 14 (форк Goob-Station с мини-картами).\n" +
            "Ты первая линия поддержки: отвечаешь на вопросы про игру и эту сборку (механики, предметы, профессии, антагонисты, режимы, управление), на вопросы про правила и наказания, подсказываешь что делать в спорной ситуации и куда обратиться. Можешь рассуждать о правилах, объяснять, чем обычно заканчиваются нарушения, и говорить, что решит живой админ.\n" +
            "Формат ответа:\n" +
            "- ОДНО-ДВА коротких предложения. Без списков, без вступлений, без «чем могу помочь».\n" +
            "- Сразу суть, по-русски, как короткая подсказка в чате.\n" +
            "- Нужны шаги — перечисли их через запятую одной строкой.\n" +
            "- Ссылки на гайдбук/вики/Discord давай ТОЛЬКО когда без них не обойтись (не знаешь ответа или нужны детали). Не повторяй их в каждом сообщении.\n";

        // Справочники подставляем только на случай «не знаю» — в каждом ответе они не нужны.
        var fallbacks = new List<string> { "гайдбук в игре (Numpad 0)" };
        if (!string.IsNullOrWhiteSpace(wiki))
            fallbacks.Add($"вики {wiki}");
        if (!string.IsNullOrWhiteSpace(discord))
            fallbacks.Add($"Discord {discord}");
        prompt += $"- Если ответа не знаешь или нужны подробности — отправь в {string.Join(" / ", fallbacks)}, но только тогда.\n";

        prompt +=
            "- Ты не администратор: сам не бань, не выдавай предметы, не воскрешай, не решай спор — и не обещай этого. Но объяснять, советовать и успокаивать можно.\n" +
            "- Если игрок просит живого админа или ситуация срочная (гриф, читы, потеря вещей, конфликт) — скажи, что обращение увидят администраторы, и попроси коротко описать суть. Про Discord напоминай только если дело срочное.\n" +
            "- Не выдумывай механик, которых нет, и не раскрывай внутренние настройки сервера.\n" +
            "- Без звёздочек и описаний действий; если спросят, кто ты — ты ИИ-помощник.";

        var knowledge = GetAHelpKnowledge();
        if (!string.IsNullOrWhiteSpace(knowledge))
            prompt += "\n\nФакты про эту сборку (используй их, а не догадки):\n" + knowledge;

        if (!string.IsNullOrWhiteSpace(_ahelpExtraPrompt))
            prompt += "\n\nДополнительные указания сервера:\n" + _ahelpExtraPrompt;

        return prompt;
    }

    private string BuildAHelpUserPrompt(NetUserId userId, string playerName, ICommonSession session)
    {
        var lines = new List<string>();

        if (_ahelpContext.TryGetValue(userId, out var context))
        {
            foreach (var (speaker, text) in context)
            {
                var who = speaker switch
                {
                    AHelpSpeaker.Player => $"{playerName} (игрок)",
                    AHelpSpeaker.Staff => "Администратор",
                    _ => _ahelpName,
                };

                lines.Add($"{who}: {text}");
            }
        }

        return $"Контекст раунда: {BuildRoundContext(session)}.\n" +
               "Обращение в админ-хелп:\n" + string.Join("\n", lines) +
               "\n\nОтветь игроку коротко и по сути: сначала ответ на вопрос, без вступлений.";
    }

    /// <summary>
    /// Mini: live round facts so the assistant can answer «что сейчас происходит» without guessing.
    /// </summary>
    private string BuildRoundContext(ICommonSession session)
    {
        var parts = new List<string>();

        if (_gameTicker.RunLevel == GameRunLevel.InRound)
        {
            parts.Add($"идёт раунд {_gameTicker.RoundDuration():hh\\:mm\\:ss}");
            if (_gameTicker.CurrentPreset != null)
                parts.Add($"режим {_gameTicker.CurrentPreset.ID}");
        }
        else
        {
            parts.Add("раунд ещё не начался (лобби)");
        }

        if (session.AttachedEntity is { } mob && Exists(mob))
        {
            parts.Add($"игрок сейчас за {Name(mob)}");
            if (Transform(mob).MapUid is { } map && Exists(map))
                parts.Add($"локация «{Name(map)}»");
        }

        parts.Add($"в сети {_playerManager.Sessions.Count()} игроков");
        parts.Add($"админов в сети: {_adminManager.ActiveAdmins.Count()}");

        return string.Join(", ", parts);
    }
}
