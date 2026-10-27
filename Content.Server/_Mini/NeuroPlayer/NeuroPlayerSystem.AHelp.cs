// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Notes;
using Content.Server.Administration.Systems;
using Content.Server.Chat.Managers;
using Content.Server.Database;
using Content.Shared._Mini.MiniCCVars;
using Content.Shared._Mini.NeuroAHelp;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Asynchronous;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Mini.NeuroPlayer;

/// <summary>
/// Admin-help (ahelp) entry point: collects player messages, decides when the AI administrator may
/// step in, and keeps the ticket context for the agent loop in <see cref="NeuroPlayerSystem"/>
/// (see NeuroPlayerSystem.AdminAI.cs).
/// </summary>
/// <remarks>
/// Reuses the neuro player LLM plumbing (provider presets, fallback provider, blocked words,
/// screen) through the partial class, but keeps its own request budget so the IC bots cannot
/// starve the support channel.
/// </remarks>
public sealed partial class NeuroPlayerSystem
{
    /// <summary>Hard ceiling for an ahelp answer, even if neuroahelp.max_reply_chars is set higher.</summary>
    private const int MaxAHelpReplyLength = 700;

    /// <summary>Colour of the AI administrator's name in the ahelp window.</summary>
    private const string AHelpColor = "#8ab4f8";

    private enum AHelpSpeaker : byte
    {
        Player,
        Staff,
        Assistant,
    }

    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IAdminNotesManager _adminNotes = default!;
    [Dependency] private readonly IBanManager _banManager = default!;
    [Dependency] private readonly BwoinkSystem _bwoink = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    [Dependency] private readonly ITaskManager _taskManager = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;

    private bool _ahelpEnabled;
    private string _ahelpName = "Нейроадминистратор";
    private int _ahelpDelaySeconds;
    private int _ahelpNoAdminDelaySeconds;
    private int _ahelpAdminSilenceSeconds;
    private int _ahelpStaffMuteMinutes;
    private int _ahelpCooldownSeconds;
    private int _ahelpMaxContext;
    private int _ahelpMaxTokens;
    private int _ahelpMaxReplyChars;
    private int _ahelpRequestsPerMinute;
    private int _ahelpMaxActionSteps;
    private int _ahelpActionTimeoutSeconds;
    private int _ahelpMaxLogLines;
    private int _ahelpMaxBanHours;
    private bool _ahelpBansEnabled;
    private bool _ahelpWarningsEnabled;
    private string _ahelpExtraPrompt = string.Empty;
    private string _ahelpKnowledgeFile = string.Empty;
    private int _ahelpKnowledgeMaxChars;

    private string _ahelpKnowledgeCache = string.Empty;
    private DateTime _ahelpKnowledgeStamp = DateTime.MinValue;
    private bool _ahelpKnowledgeWarned;

    /// <summary>Аварийный выключатель ассистента командой /ai global off (до рестарта сервера).</summary>
    private bool _ahelpGlobalOff;

    /// <summary>Тикеты, где ИИ замучен командой /ai off (до времени или до конца раунда).</summary>
    private readonly Dictionary<NetUserId, TimeSpan> _ahelpTicketMutedUntil = new();

    /// <summary>Тикеты с форсированным ответом (/ai answer): один раз игнорирует тишину админа.</summary>
    private readonly HashSet<NetUserId> _ahelpForcedTickets = new();

    private readonly Dictionary<NetUserId, List<(AHelpSpeaker Speaker, string Text)>> _ahelpContext = new();
    private readonly Dictionary<NetUserId, TimeSpan> _ahelpLastStaffMessage = new();
    private readonly Dictionary<NetUserId, TimeSpan> _ahelpLastReply = new();
    private readonly List<PendingAHelp> _ahelpQueue = new();
    private readonly Queue<TimeSpan> _ahelpRequestTimes = new();

    /// <summary>When the AI last warned a player. Shared with the agent loop, hence concurrent.</summary>
    private readonly ConcurrentDictionary<NetUserId, DateTime> _ahelpWarnTimes = new();

    /// <summary>Players whose ticket is being handled right now, so one player never gets two agents.</summary>
    private readonly HashSet<NetUserId> _ahelpAgentUsers = new();

    /// <summary>
    /// Assistant lines produced by finished requests. The request task runs on a thread pool thread,
    /// so the shared ahelp context is only touched from the game loop: the task enqueues, Update drains.
    /// </summary>
    private readonly ConcurrentQueue<(NetUserId UserId, string Text)> _ahelpAssistantLines = new();

    /// <summary>A player message waiting out the admin grace period before the AI steps in.</summary>
    private sealed record PendingAHelp(NetUserId UserId, string PlayerName, TimeSpan AskedAt, bool Force = false);

    private void InitializeAHelp()
    {
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpEnabled, v => _ahelpEnabled = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpName, v => _ahelpName = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpDelaySeconds, v => _ahelpDelaySeconds = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpNoAdminDelaySeconds, v => _ahelpNoAdminDelaySeconds = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpAdminSilenceSeconds, v => _ahelpAdminSilenceSeconds = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpStaffMuteMinutes, v => _ahelpStaffMuteMinutes = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpCooldownSeconds, v => _ahelpCooldownSeconds = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpMaxContext, v => _ahelpMaxContext = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpMaxTokens, v => _ahelpMaxTokens = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpMaxReplyChars, v => _ahelpMaxReplyChars = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpRequestsPerMinute, v => _ahelpRequestsPerMinute = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpMaxActionSteps, v => _ahelpMaxActionSteps = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpActionTimeoutSeconds, v => _ahelpActionTimeoutSeconds = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpMaxLogLines, v => _ahelpMaxLogLines = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpMaxBanHours, v => _ahelpMaxBanHours = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpBansEnabled, v => _ahelpBansEnabled = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpWarningsEnabled, v => _ahelpWarningsEnabled = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpExtraPrompt, v => _ahelpExtraPrompt = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpKnowledgeFile, v => _ahelpKnowledgeFile = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpKnowledgeMaxChars, v => _ahelpKnowledgeMaxChars = v, true);

        SubscribeNetworkEvent<SharedBwoinkSystem.BwoinkTextMessage>(OnAHelpTextMessage);
        // Команды админов "/ai ..." приходят из BwoinkSystem: он их перехватывает до релея игроку.
        SubscribeLocalEvent<AHelpAdminCommandEvent>(OnAHelpAdminCommand);
        // RoundRestartCleanupEvent is already subscribed in the main Initialize(); the ahelp state is
        // reset from that handler via ResetAHelpState() - a second subscription throws at startup.
    }

    /// <summary>Drops per-round ahelp state. Called from the system's RoundRestartCleanupEvent handler.</summary>
    private void ResetAHelpState()
    {
        _ahelpContext.Clear();
        _ahelpLastStaffMessage.Clear();
        _ahelpLastReply.Clear();
        _ahelpQueue.Clear();
        _ahelpRequestTimes.Clear();
        _ahelpWarnTimes.Clear();
        _ahelpAgentUsers.Clear();
        _ahelpTicketMutedUntil.Clear();
        _ahelpForcedTickets.Clear();
        _ahelpKnowledgeWarned = false;
    }

    /// <summary>
    /// Sees every ahelp line (players and admins). Player lines queue an AI answer, staff lines make
    /// the AI back off the ticket.
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

        // Команды ассистенту исполняются через AHelpAdminCommandEvent (см. BwoinkSystem), в контекст
        // тикета и очередь ответов они попадать не должны.
        if (fromStaff && NeuroAHelpCommands.IsCommand(message.Text))
            return;

        AddAHelpContext(message.UserId, fromPlayer ? AHelpSpeaker.Player : AHelpSpeaker.Staff, message.Text);

        if (fromStaff)
        {
            _ahelpLastStaffMessage[message.UserId] = _timing.CurTime;

            // Тикет забрал живой админ: кольцо в окне ахелпа переводится в режим «молчит».
            var mute = TimeSpan.FromMinutes(Math.Max(0, _ahelpStaffMuteMinutes));
            BroadcastAHelpStatus(message.UserId, NeuroAHelpState.Silent,
                mute > TimeSpan.Zero
                    ? $"тихо: тикет взял администратор, ИИ молчит {Math.Max(1, (int) mute.TotalMinutes)} мин"
                    : "тихо: тикет взял администратор");
            return;
        }

        // One pending answer per player, always for the newest message. The delay depends on whether
        // a live admin is online: with admins the AI waits admin_silence_seconds, otherwise only
        // no_admin_delay_seconds. It is recomputed in TickAHelp, so an admin coming online later
        // extends the wait instead of cancelling the ticket.
        _ahelpQueue.RemoveAll(pending => pending.UserId == message.UserId);
        // Форсированный разбор (/ai answer) должен переживать новые сообщения игрока.
        var force = _ahelpForcedTickets.Contains(message.UserId);
        _ahelpQueue.Add(new PendingAHelp(message.UserId, session.Name, _timing.CurTime, force));
    }

    /// <summary>Whether any non-de-adminned admin is connected right now.</summary>
    private bool HasActiveAdmins()
    {
        return _adminManager.ActiveAdmins.Any();
    }

    /// <summary>Кольцо-индикатор нейропомощника у админов: одно сообщение на смену состояния.</summary>
    private void BroadcastAHelpStatus(NetUserId userId, NeuroAHelpState state, string detail)
    {
        // Агент работает в потоковом пуле: доступ к админам только из основного потока.
        _taskManager.RunOnMainThread(() =>
        {
            foreach (var admin in _adminManager.ActiveAdmins)
            {
                if (_adminManager.GetAdminData(admin) is not { } data || !data.HasFlag(AdminFlags.Adminhelp))
                    continue;

                RaiseNetworkEvent(new NeuroAHelpStatusMessage(userId, state, _ahelpName, detail), admin.Channel);
            }
        });
    }

    /// <summary>
    /// Системная строка для админов в тикете (результаты команд /ai). Игроку и в вебхук не уходит.
    /// </summary>
    private void SendAHelpAdminNote(NetUserId userId, string text)
    {
        var line = $"[color={AHelpColor}]{FormattedMessage.EscapeText(text)}[/color]";

        foreach (var admin in _adminManager.ActiveAdmins)
        {
            if (_adminManager.GetAdminData(admin) is not { } data || !data.HasFlag(AdminFlags.Adminhelp))
                continue;

            RaiseNetworkEvent(new SharedBwoinkSystem.BwoinkTextMessage(
                userId, SharedBwoinkSystem.SystemUserId, line, playSound: false, adminOnly: true), admin.Channel);
        }
    }

    private void OnAHelpAdminCommand(AHelpAdminCommandEvent ev)
    {
        // На случай прямого поднятия события без проверки прав в BwoinkSystem.
        var senderAdmin = _adminManager.GetAdminData(ev.Sender);
        if (senderAdmin == null || !senderAdmin.HasFlag(AdminFlags.Adminhelp))
            return;

        if (!NeuroAHelpCommands.TryParse(ev.Text, out var command, out var error))
        {
            SendAHelpAdminNote(ev.Channel, $"[{_ahelpName}] {error}");
            return;
        }

        var now = _timing.CurTime;
        var online = _playerManager.TryGetSessionById(ev.Channel, out var targetSession);

        switch (command.Kind)
        {
            case NeuroAHelpCommandKind.Help:
                SendAHelpAdminNote(ev.Channel,
                    $"[{_ahelpName}] команды: /ai status; /ai off [минут] - молчать в тикете (без числа - до конца раунда); " +
                    "/ai on - вернуть ИИ в тикет; /ai answer - ответить сейчас, не дожидаясь тишины; " +
                    "/ai reset - забыть переписку тикета; /ai global off|on - выключить/включить ассистента совсем.");
                break;

            case NeuroAHelpCommandKind.Status:
            {
                var state = _ahelpGlobalOff
                    ? "выключен глобально (/ai global on)"
                    : _ahelpEnabled
                        ? "включен"
                        : "выключен сервером (neuroahelp.enabled)";
                var adminOnline = HasActiveAdmins() ? "админы в сети" : "админов нет";
                var context = _ahelpContext.TryGetValue(ev.Channel, out var lines) ? lines.Count : 0;
                var busy = _ahelpAgentUsers.Contains(ev.Channel) ? "агент разбирает тикет" : "агент свободен";

                var ticket = new StringBuilder($"{busy}, строк в контексте: {context}");
                if (_ahelpTicketMutedUntil.TryGetValue(ev.Channel, out var mutedUntil))
                {
                    ticket.Append(mutedUntil == TimeSpan.MaxValue
                        ? ", ИИ замучен до конца раунда"
                        : $", ИИ замучен ещё {Math.Ceiling((mutedUntil - now).TotalMinutes)} мин");
                }

                if (_ahelpLastStaffMessage.TryGetValue(ev.Channel, out var staffAt))
                {
                    ticket.Append($", админ отвечал {(int) (now - staffAt).TotalMinutes} мин назад");
                }

                if (_ahelpLastReply.TryGetValue(ev.Channel, out var replyAt))
                {
                    ticket.Append($", ИИ отвечал {(int) (now - replyAt).TotalMinutes} мин назад");
                }

                ticket.Append(online ? $", игрок «{targetSession!.Name}» в сети" : ", игрок не в сети");
                SendAHelpAdminNote(ev.Channel, $"[{_ahelpName}] {state}, {adminOnline}. {ticket}");
                break;
            }

            case NeuroAHelpCommandKind.Off:
                _ahelpQueue.RemoveAll(pending => pending.UserId == ev.Channel);
                _ahelpForcedTickets.Remove(ev.Channel);
                _ahelpTicketMutedUntil[ev.Channel] =
                    command.Minutes > 0 ? now + TimeSpan.FromMinutes(command.Minutes) : TimeSpan.MaxValue;
                BroadcastAHelpStatus(ev.Channel, NeuroAHelpState.Muted,
                    command.Minutes > 0
                        ? $"выключен админом на {command.Minutes} мин"
                        : "выключен админом до конца раунда");
                SendAHelpAdminNote(ev.Channel,
                    command.Minutes > 0
                        ? $"[{_ahelpName}] молчу в этом тикете {command.Minutes} мин. Вернуть: /ai on."
                        : $"[{_ahelpName}] молчу в этом тикете до конца раунда. Вернуть: /ai on.");
                _sawmill.Info($"Neuro ahelp: {ev.Sender.Name} muted the assistant in the ticket of {ev.Channel} ({command.Minutes} min).");
                break;

            case NeuroAHelpCommandKind.On:
                if (_ahelpTicketMutedUntil.Remove(ev.Channel))
                {
                    BroadcastAHelpStatus(ev.Channel, NeuroAHelpState.Idle, string.Empty);
                    SendAHelpAdminNote(ev.Channel, $"[{_ahelpName}] снова отвечаю в этом тикете.");
                }
                else
                {
                    SendAHelpAdminNote(ev.Channel, $"[{_ahelpName}] в этом тикете я и не был выключен.");
                }
                break;

            case NeuroAHelpCommandKind.Answer:
                if (!online)
                {
                    SendAHelpAdminNote(ev.Channel, $"[{_ahelpName}] игрок не в сети, отвечать некому.");
                    break;
                }

                if (_ahelpGlobalOff || !_ahelpEnabled)
                {
                    SendAHelpAdminNote(ev.Channel, $"[{_ahelpName}] ассистент выключен, сначала /ai global on.");
                    break;
                }

                _ahelpForcedTickets.Add(ev.Channel);
                _ahelpQueue.RemoveAll(pending => pending.UserId == ev.Channel);
                // AskedAt в нуле: форсированный тикет проходит любую задержку в TickAHelp.
                _ahelpQueue.Add(new PendingAHelp(ev.Channel, targetSession!.Name, TimeSpan.Zero, Force: true));
                SendAHelpAdminNote(ev.Channel, $"[{_ahelpName}] разбираю тикет прямо сейчас, несмотря на ответ админа.");
                break;

            case NeuroAHelpCommandKind.Reset:
                _ahelpContext.Remove(ev.Channel);
                _ahelpQueue.RemoveAll(pending => pending.UserId == ev.Channel);
                _ahelpLastReply.Remove(ev.Channel);
                SendAHelpAdminNote(ev.Channel, $"[{_ahelpName}] переписку тикета забыл, контекст очищен.");
                break;

            case NeuroAHelpCommandKind.GlobalOff:
                _ahelpGlobalOff = true;
                SendAHelpAdminNote(ev.Channel,
                    $"[{_ahelpName}] выключен для всех тикетов до рестарта сервера или /ai global on.");
                _sawmill.Info($"Neuro ahelp: {ev.Sender.Name} disabled the assistant globally.");
                break;

            case NeuroAHelpCommandKind.GlobalOn:
                _ahelpGlobalOff = false;
                SendAHelpAdminNote(ev.Channel, $"[{_ahelpName}] снова работаю во всех тикетах.");
                _sawmill.Info($"Neuro ahelp: {ev.Sender.Name} enabled the assistant globally.");
                break;
        }
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

    /// <summary>Grace period expired: re-check that no admin took the ticket, then start the agent.</summary>
    private void TickAHelp()
    {
        if (!_ahelpEnabled || _ahelpGlobalOff)
            return;

        // Lines from finished requests land in the context here, on the game loop thread.
        while (_ahelpAssistantLines.TryDequeue(out var line))
            AddAHelpContext(line.UserId, AHelpSpeaker.Assistant, line.Text);

        if (_ahelpQueue.Count == 0)
            return;

        var now = _timing.CurTime;
        var adminsOnline = HasActiveAdmins();
        var delay = adminsOnline
            ? TimeSpan.FromSeconds(Math.Max(0, _ahelpAdminSilenceSeconds))
            : TimeSpan.FromSeconds(Math.Max(0, _ahelpNoAdminDelaySeconds > 0 ? _ahelpNoAdminDelaySeconds : _ahelpDelaySeconds));
        var staffMute = TimeSpan.FromMinutes(Math.Max(0, _ahelpStaffMuteMinutes));

        for (var i = _ahelpQueue.Count - 1; i >= 0; i--)
        {
            var pending = _ahelpQueue[i];

            // Форсированный тикет (/ai answer) проходит и задержку, и тишину после админа.
            if (!pending.Force && now - pending.AskedAt < delay)
                continue;

            // The agent for this player is still working: keep the ticket queued instead of starting
            // a second one.
            if (_ahelpAgentUsers.Contains(pending.UserId))
                continue;

            if (pending.Force)
            {
                _ahelpQueue.RemoveAt(i);
            }
            else
            {
                // Команда /ai off: ИИ молчит в тикете до времени или до конца раунда.
                if (_ahelpTicketMutedUntil.TryGetValue(pending.UserId, out var mutedUntil) && now < mutedUntil)
                {
                    _ahelpQueue.RemoveAt(i);
                    continue;
                }

                // A live admin really answered this ticket after the player's message.
                if (_ahelpLastStaffMessage.TryGetValue(pending.UserId, out var staffAt) && staffAt > pending.AskedAt)
                {
                    _ahelpQueue.RemoveAt(i);
                    continue;
                }

                // И админ вообще отвечал недавно: пока идет окно тишины, игрок общается с человеком,
                // и ИИ не встревает между ними даже на новые сообщения.
                if (staffMute > TimeSpan.Zero &&
                    _ahelpLastStaffMessage.TryGetValue(pending.UserId, out var lastStaff) &&
                    now - lastStaff < staffMute)
                {
                    _ahelpQueue.RemoveAt(i);
                    continue;
                }

                if (_ahelpLastReply.TryGetValue(pending.UserId, out var repliedAt) &&
                    now - repliedAt < TimeSpan.FromSeconds(Math.Max(0, _ahelpCooldownSeconds)))
                {
                    continue;
                }

                if (!_playerManager.TryGetSessionById(pending.UserId, out _))
                {
                    _ahelpQueue.RemoveAt(i);
                    continue;
                }

                // Бюджет запросов кончился: тикет остается в очереди и попробует еще раз на
                // следующем тике, а не пропадает.
                if (!TryTakeAHelpRequestSlot())
                    continue;

                _ahelpQueue.RemoveAt(i);
            }

            // Mark the request immediately so a spamming player cannot queue several at once.
            _ahelpLastReply[pending.UserId] = now;
            _ahelpAgentUsers.Add(pending.UserId);
            _ = RunAdminAgentAsync(pending.UserId, pending.PlayerName, pending.AskedAt, pending.Force);
        }
    }

    /// <summary>Sliding-window budget for the AI (neuroahelp.requests_per_minute, 0 = unlimited).</summary>
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
}
