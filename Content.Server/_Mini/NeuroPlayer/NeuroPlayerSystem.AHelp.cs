// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Systems;
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
    /// <summary>Ahelp answers may be longer than an IC one-liner, but still bounded.</summary>
    private const int MaxAHelpReplyLength = 600;

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

    private bool _ahelpEnabled;
    private string _ahelpName = "Нейропомощник";
    private int _ahelpDelaySeconds;
    private int _ahelpCooldownSeconds;
    private int _ahelpMaxContext;
    private int _ahelpMaxTokens;
    private int _ahelpRequestsPerMinute;
    private string _ahelpExtraPrompt = string.Empty;

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
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpMaxContext, v => _ahelpMaxContext = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpMaxTokens, v => _ahelpMaxTokens = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpRequestsPerMinute, v => _ahelpRequestsPerMinute = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpExtraPrompt, v => _ahelpExtraPrompt = v, true);

        SubscribeNetworkEvent<SharedBwoinkSystem.BwoinkTextMessage>(OnAHelpTextMessage);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnAHelpRoundRestart);
    }

    private void OnAHelpRoundRestart(RoundRestartCleanupEvent ev)
    {
        _ahelpContext.Clear();
        _ahelpLastStaffMessage.Clear();
        _ahelpLastReply.Clear();
        _ahelpQueue.Clear();
        _ahelpRequestTimes.Clear();
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
        if (HasActiveAdmins())
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

            // An admin came online (or answered) while the ticket was waiting.
            if (HasActiveAdmins())
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

            var systemPrompt = BuildAHelpSystemPrompt();
            var userPrompt = BuildAHelpUserPrompt(userId, session.Name);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds));
            var reply = await SendCompletionWithFallbackAsync(config, systemPrompt, userPrompt, cts.Token,
                maxTokens: _ahelpMaxTokens);

            if (string.IsNullOrWhiteSpace(reply))
                return;

            // Mini: same cleanup the IC bots get — no long dashes, single spaces, bounded length.
            reply = reply.Replace('—', '-').Replace('–', '-');
            reply = string.Join(' ', reply.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            if (reply.Length > MaxAHelpReplyLength)
                reply = reply[..MaxAHelpReplyLength];

            // Blocked words still apply, but unlike the IC bots the assistant is supposed to admit
            // that it is an AI, so that part of the screen is off.
            if (!TryScreenText($"ahelp/{session.Name}", reply, blockAiTalk: false))
                return;

            // An admin could have come online while the model was thinking.
            if (HasActiveAdmins())
            {
                _sawmill.Info($"Neuro ahelp: admin came online, answer to {session.Name} dropped.");
                return;
            }

            if (!_playerManager.TryGetSessionById(userId, out var stillConnected))
                return;

            var text =
                $"[color={AHelpColor}][bold]{FormattedMessage.EscapeText(_ahelpName)}[/bold][/color]: " +
                FormattedMessage.EscapeText(reply);

            _bwoink.SendPlayerChannelSystemMessage(userId, text);
            _adminLog.Add(LogType.AdminMessage, LogImpact.Low,
                $"Neuro ahelp answered {stillConnected.Name}: {reply}");

            // Keep our own line in context so follow-up questions make sense. The context itself is
            // game-loop state, so the line is handed over through a queue instead of being added here.
            _ahelpAssistantLines.Enqueue((userId, reply));
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Neuro ahelp request failed: {e.Message}");
        }
    }

    private string BuildAHelpSystemPrompt()
    {
        var discord = _cfg.GetCVar(CCVars.InfoLinksDiscord);
        var wiki = _cfg.GetCVar(CCVars.InfoLinksWiki);

        var prompt =
            $"Ты — «{_ahelpName}», ИИ-помощник в админ-хелпе (ahelp) русского сервера «Мини-Станция» по игре Space Station 14.\n" +
            "Администраторов сейчас в сети нет, поэтому на обращение отвечаешь ты — как первая линия поддержки.\n" +
            "Правила:\n" +
            "- Отвечай по-русски, коротко (2-4 предложения), вежливо и по делу.\n" +
            "- Сразу честно говори, что ты ИИ-помощник, а не администратор.\n" +
            "- Ты не можешь банить, выдавать предметы, воскрешать, разбирать конфликты и апелляции. Не обещай этого.\n" +
            "- Если нужен живой админ (нарушения, читы, конфликт игроков, потеря вещей, апелляция) — скажи, что обращение увидят администраторы, и попроси коротко изложить суть.\n";

        if (!string.IsNullOrWhiteSpace(wiki))
            prompt += $"- На вопросы про правила и механики ссылайся на вики сервера: {wiki}\n";
        if (!string.IsNullOrWhiteSpace(discord))
            prompt += $"- Для срочных вопросов и апелляций дай ссылку на Discord сервера: {discord}\n";

        prompt +=
            "- Помогай с игровыми вопросами: что делать при баге, как найти механику, куда обратиться.\n" +
            "- Не раскрывай внутренние настройки сервера, не обсуждай других игроков и не говори от лица администрации.\n" +
            "- Не используй звёздочки и описания действий — только прямую речь.";

        if (!string.IsNullOrWhiteSpace(_ahelpExtraPrompt))
            prompt += "\n\nДополнительные указания сервера:\n" + _ahelpExtraPrompt;

        return prompt;
    }

    private string BuildAHelpUserPrompt(NetUserId userId, string playerName)
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

        return "Обращение в админ-хелп:\n" + string.Join("\n", lines) +
               "\n\nОтветь игроку одной короткой репликой в ahelp.";
    }
}
