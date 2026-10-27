// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Shared._Mini.NeuroAHelp;
using Content.Shared.Administration.Logs;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Mini.NeuroPlayer;

/// <summary>
/// The AI administrator itself: a small agent loop on top of the neuro player LLM plumbing.
/// Each step the model returns exactly one JSON action, the action is executed (read logs, notes,
/// warn, ban, escalate, reply) and its result is fed back into the next step.
/// </summary>
/// <remarks>
/// The loop runs on the thread pool. Anything that touches entities, sessions, chat or the ban
/// manager is executed on the main thread through <see cref="ITaskManager"/>; only HTTP and database
/// work happens off it. Every mutating action also goes to the admin log.
/// </remarks>
public sealed partial class NeuroPlayerSystem
{
    /// <summary>The whole ticket must be handled in roughly three minutes.</summary>
    private static readonly TimeSpan AdminAgentDeadline = TimeSpan.FromMinutes(3);

    /// <summary>Warn cooldown per player, as in the brief: at most one warn per ten minutes.</summary>
    private static readonly TimeSpan AdminWarnCooldown = TimeSpan.FromMinutes(10);

    private static readonly JsonSerializerOptions AdminActionJson = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>State of one ticket handled by the agent loop.</summary>
    private sealed class AdminTicket
    {
        public required NetUserId UserId { get; init; }
        public required string PlayerName { get; init; }
        public required TimeSpan AskedAt { get; init; }
        public DateTime StartedUtc { get; init; } = DateTime.UtcNow;

        /// <summary>Запущен принудительно (/ai answer): ответ живого админа его не снимает.</summary>
        public bool Forced { get; init; }

        public int Steps { get; set; }
        public bool Replied { get; set; }
        public bool BanIssued { get; set; }

        /// <summary>Targets a successful read_logs actually returned logs for (ban evidence).</summary>
        public HashSet<NetUserId> Evidence { get; } = new();

        /// <summary>Action and result lines shown to the model on the next step.</summary>
        public List<string> Journal { get; } = new();
    }

    /// <summary>One action as returned by the model.</summary>
    private sealed class AdminAction
    {
        public string? Action { get; set; }
        public string? Ckey { get; set; }
        public int? Minutes { get; set; }
        public int? Hours { get; set; }
        public string[]? Types { get; set; }
        public string? Search { get; set; }
        public string? Url { get; set; }
        public string? Message { get; set; }
        public string? Reason { get; set; }
    }

    private readonly record struct LoggedPlayer(NetUserId UserId, string Ckey, string? Character);

    /// <summary>
    /// Runs an action on the game loop and waits for it. The agent loop itself runs on a thread pool
    /// thread, so this never blocks the main thread: the callback is queued and the caller awaits.
    /// </summary>
    private Task RunAdminMainThreadAsync(Action action)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _taskManager.RunOnMainThread(() =>
        {
            try
            {
                action();
                tcs.TrySetResult();
            }
            catch (Exception e)
            {
                tcs.TrySetException(e);
            }
        });

        return tcs.Task;
    }

    private Task<T> RunAdminMainThreadAsync<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _taskManager.RunOnMainThread(() =>
        {
            try
            {
                tcs.TrySetResult(func());
            }
            catch (Exception e)
            {
                tcs.TrySetException(e);
            }
        });

        return tcs.Task;
    }

    /// <summary>Main entry point called from <c>TickAHelp</c> when the grace period is over.</summary>
    private async Task RunAdminAgentAsync(NetUserId userId, string playerName, TimeSpan askedAt, bool forced = false)
    {
        var ticket = new AdminTicket
        {
            UserId = userId,
            PlayerName = playerName,
            AskedAt = askedAt,
            Forced = forced,
        };

        BroadcastAHelpStatus(userId, NeuroAHelpState.Working, "разбираю обращение");

        try
        {
            var config = ResolveProvider();
            if (!config.IsConfigured)
            {
                WarnUnconfigured(config);
                await FailAdminTicketAsync(ticket, "LLM не настроен");
                return;
            }

            var systemPrompt = BuildAdminSystemPrompt();
            var adminCount = await RunAdminMainThreadAsync(() => _adminManager.ActiveAdmins.Count());
            _sawmill.Info($"Neuro ahelp: admin agent started for {playerName} (admins={adminCount}, forced={forced})");

            var maxSteps = Math.Clamp(_ahelpMaxActionSteps, 1, 12);
            for (var step = 1; step <= maxSteps; step++)
            {
                if (DateTime.UtcNow - ticket.StartedUtc > AdminAgentDeadline)
                {
                    await FailAdminTicketAsync(ticket, "цикл занял больше трех минут");
                    return;
                }

                // Форсированный тикет админ попросил явно, поэтому ответ живого админа его не снимает.
                if (!forced && await AdminAlreadyAnsweredAsync(ticket))
                {
                    _sawmill.Info($"Neuro ahelp: admin answered the ticket of {playerName}, agent stopped.");
                    BroadcastAHelpStatus(userId, NeuroAHelpState.Silent, "тихо: тикет взял администратор");
                    return;
                }

                ticket.Steps = step;
                var transcript = await RunAdminMainThreadAsync(() => BuildAdminTranscript(ticket.UserId));
                var userPrompt = BuildAdminUserPrompt(ticket, transcript);

                // A step never outlives the whole-ticket budget.
                var remaining = AdminAgentDeadline - (DateTime.UtcNow - ticket.StartedUtc);
                if (remaining <= TimeSpan.Zero)
                {
                    await FailAdminTicketAsync(ticket, "цикл занял больше трех минут");
                    return;
                }

                var stepTimeout = TimeSpan.FromSeconds(Math.Clamp(_ahelpActionTimeoutSeconds, 5, 300));
                if (remaining < stepTimeout)
                    stepTimeout = remaining;

                string? raw;
                try
                {
                    using var cts = new CancellationTokenSource(stepTimeout);
                    raw = await SendCompletionWithFallbackAsync(config, systemPrompt, userPrompt, cts.Token,
                        maxTokens: _ahelpMaxTokens);
                }
                catch (Exception e)
                {
                    _sawmill.Warning($"Neuro ahelp: action step failed for {playerName}: {e.Message}");
                    await FailAdminTicketAsync(ticket, "модель не ответила");
                    return;
                }

                if (string.IsNullOrWhiteSpace(raw))
                {
                    await FailAdminTicketAsync(ticket, "пустой ответ модели");
                    return;
                }

                raw = StripAdminCodeFences(raw);

                if (!TryParseAdminAction(raw, out var action))
                {
                    // Модель вернула JSON, но побитый или обрезанный: игроку такой мусор не
                    // показываем, вместо этого просим модель переформулировать (журнал уйдет в
                    // следующий шаг) и только на последнем шаге молча передаем тикет админам.
                    if (LooksLikeAdminActionJson(raw))
                    {
                        _sawmill.Info($"Neuro ahelp: model returned broken JSON for {playerName} (step {step}).");
                        if (step < maxSteps)
                        {
                            AddAdminJournal(ticket, "формат",
                                "Твой прошлый ответ был не валидным JSON или обрезался. " +
                                "Верни ровно один JSON-объект действия, компактно, без переносов внутри полей.");
                            continue;
                        }

                        await FailAdminTicketAsync(ticket, "модель вернула обрезанный JSON");
                        return;
                    }

                    // The model ignored the JSON protocol with plain prose: treat its text as a
                    // normal answer, as the brief asks, instead of dropping the ticket.
                    _sawmill.Info($"Neuro ahelp: model returned plain text for {playerName}, using it as the reply.");
                    await ReplyAdminAsync(ticket, raw);
                    return;
                }

                if (await ExecuteAdminActionAsync(ticket, action))
                    return;
            }

            await FailAdminTicketAsync(ticket, "закончились шаги");
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Neuro ahelp: admin agent crashed for {playerName}: {e}");
            await FailAdminTicketAsync(ticket, "внутренняя ошибка");
        }
        finally
        {
            // Fire and forget on purpose: this only clears the "agent is running" flag, and awaiting
            // the game loop here could hang if the round is shutting down.
            _taskManager.RunOnMainThread(() =>
            {
                _ahelpAgentUsers.Remove(userId);
                _ahelpForcedTickets.Remove(userId);
            });
        }
    }

    /// <summary>Убирает markdown-ограждения ```json ... ``` вокруг действия, если модель их прислала.</summary>
    private static string StripAdminCodeFences(string raw)
    {
        var trimmed = raw.Trim();
        if (!trimmed.StartsWith("```"))
            return raw;

        // Срезаем первую строку (``` или ```json) и хвостовую решетку.
        var firstLineEnd = trimmed.IndexOf('\n');
        if (firstLineEnd < 0)
            return trimmed;

        var body = trimmed[(firstLineEnd + 1)..].TrimEnd();
        if (body.EndsWith("```"))
            body = body[..^3];

        return body.Trim();
    }

    /// <summary>Похоже ли на действие в JSON: не отправляем такое игроку даже как «простой текст».</summary>
    private static bool LooksLikeAdminActionJson(string raw)
    {
        var trimmed = raw.TrimStart();
        return trimmed.StartsWith('{') || trimmed.Contains("\"action\"", StringComparison.OrdinalIgnoreCase);
    }

    private Task<bool> AdminAlreadyAnsweredAsync(AdminTicket ticket)
    {
        return RunAdminMainThreadAsync(() =>
            _ahelpLastStaffMessage.TryGetValue(ticket.UserId, out var staffAt) && staffAt > ticket.AskedAt);
    }

    /// <summary>Executes one action. Returns true when the ticket is finished.</summary>
    private async Task<bool> ExecuteAdminActionAsync(AdminTicket ticket, AdminAction action)
    {
        var name = (action.Action ?? string.Empty).Trim().ToLowerInvariant();
        _sawmill.Info($"Neuro ahelp: action {name} (ticket {ticket.PlayerName}, step {ticket.Steps})");

        BroadcastAHelpStatus(ticket.UserId, NeuroAHelpState.Working, DescribeAdminAction(name));

        switch (name)
        {
            case "list_players":
                AddAdminJournal(ticket, name, await ListAdminPlayersAsync());
                return false;
            case "search_build":
                AddAdminJournal(ticket, name, await SearchBuildForAdminAsync(action));
                return false;
            case "read_url":
                AddAdminJournal(ticket, name, await ReadUrlForAdminAsync(action));
                return false;
            case "read_logs":
                AddAdminJournal(ticket, name, await ReadAdminLogsAsync(ticket, action));
                return false;
            case "get_notes":
                AddAdminJournal(ticket, name, await GetAdminNotesAsync(action));
                return false;
            case "warn":
                AddAdminJournal(ticket, name, await WarnAdminAsync(ticket, action));
                return false;
            case "ban":
                AddAdminJournal(ticket, name, await BanAdminAsync(ticket, action));
                return true;
            case "escalate":
                AddAdminJournal(ticket, name, await EscalateAdminAsync(ticket, action));
                BroadcastAHelpStatus(ticket.UserId, NeuroAHelpState.Done, "передал тикет администраторам");
                return true;
            case "reply":
            {
                var text = (action.Message ?? action.Reason ?? string.Empty).Trim();
                if (text.Length == 0)
                {
                    AddAdminJournal(ticket, name, "Пустой ответ не отправлен.");
                    return false;
                }

                await ReplyAdminAsync(ticket, text);
                AddAdminJournal(ticket, name, "Ответ отправлен игроку.");
                return true;
            }
            case "done":
                AddAdminJournal(ticket, name, "Тикет завершен без ответа.");
                BroadcastAHelpStatus(ticket.UserId, NeuroAHelpState.Done, "тикет закрыт без ответа");
                return true;
            default:
                AddAdminJournal(ticket, name, $"Неизвестное действие '{name}'. Доступные действия: list_players, search_build, read_url, read_logs, get_notes, warn, ban, escalate, reply, done.");
                return false;
        }
    }

    /// <summary>Человекочитаемый статус действия для тултипа кольца у админов.</summary>
    private static string DescribeAdminAction(string action)
    {
        return action switch
        {
            "list_players" => "смотрю, кто в сети",
            "search_build" => "проверяю по сборке",
            "read_url" => "читаю вики",
            "read_logs" => "читаю логи раунда",
            "get_notes" => "читаю заметки и баны",
            "warn" => "выдаю предупреждение",
            "ban" => "рассматриваю бан",
            "escalate" => "зову живого администратора",
            "reply" => "пишу ответ игроку",
            "done" => "завершаю тикет",
            _ => "разбираю обращение",
        };
    }

    private static void AddAdminJournal(AdminTicket ticket, string action, string result)
    {
        var line = result.Replace('\n', ' ').Replace('\r', ' ');
        if (line.Length > 400)
            line = line[..400] + "…";

        ticket.Journal.Add($"{ticket.Steps}. {action} -> {line}");
    }

    /// <summary>Online players with ckey, character name and job. No antagonist data.</summary>
    private Task<string> ListAdminPlayersAsync()
    {
        return RunAdminMainThreadAsync(() =>
        {
            var lines = new List<string>();
            foreach (var session in _playerManager.Sessions)
            {
                if (session.AttachedEntity is not { } entity)
                    continue;
                if (session.Status is not (SessionStatus.Connected or SessionStatus.InGame))
                    continue;

                var job = string.Empty;
                if (_mind.TryGetMind(session, out var mindId, out _))
                    job = _jobs.MindTryGetJobName(mindId);

                lines.Add($"{session.Name} | {Identity.Name(entity, EntityManager)} | {job}");
            }

            if (lines.Count == 0)
                return "Онлайн игроков нет.";

            var text = string.Join("\n", lines
                .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)
                .Take(60));
            return text.Length > 4000 ? text[..4000] : text;
        });
    }

    /// <summary>
    /// Round logs for one player (or a text search). This is how the AI checks whether a player is
    /// telling the truth. A successful read for a target also counts as evidence for a later ban.
    /// </summary>
    private async Task<string> ReadAdminLogsAsync(AdminTicket ticket, AdminAction action)
    {
        var query = (action.Ckey ?? string.Empty).Trim();
        var search = string.IsNullOrWhiteSpace(action.Search) ? null : action.Search.Trim();

        LoggedPlayer? target = null;
        if (query.Length > 0)
            target = await ResolveAdminTargetAsync(query);

        if (target == null && search == null)
        {
            return query.Length == 0
                ? "Укажи ckey игрока (см. list_players) или search по тексту лога."
                : $"Игрок '{query}' не найден. Проверь ckey через list_players.";
        }

        var minutes = Math.Clamp(action.Minutes ?? 10, 1, 60);
        var maxLines = Math.Clamp(_ahelpMaxLogLines, 1, 200);

        var filter = new LogFilter
        {
            Types = ResolveAdminLogTypes(action.Types),
            After = DateTime.UtcNow.AddMinutes(-minutes),
            Limit = maxLines,
            Search = search,
            AnyPlayers = target == null ? null : [target.Value.UserId.UserId],
        };

        List<SharedAdminLog> logs;
        try
        {
            logs = await _adminLog.CurrentRoundLogs(filter);
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Neuro ahelp: read_logs failed: {e.Message}");
            return "Не удалось прочитать логи раунда.";
        }

        var who = target?.Ckey ?? search;
        if (logs.Count == 0)
            return $"Логи по '{who}' за последние {minutes} мин не найдены.";

        if (target != null)
            ticket.Evidence.Add(target.Value.UserId);

        var rows = new List<string>();
        var total = 0;
        foreach (var log in logs)
        {
            var message = log.Message.Replace('\n', ' ').Replace('\r', ' ');
            if (message.Length > 240)
                message = message[..240] + "…";

            var row = $"[{log.Date.ToLocalTime():HH:mm:ss}] {log.Type}: {message}";
            if (rows.Count >= maxLines || total + row.Length > 4000)
                break;

            rows.Add(row);
            total += row.Length;
        }

        rows.Reverse();

        var sb = new StringBuilder();
        sb.AppendLine($"Логи по '{who}' за последние {minutes} мин (старые сверху):");
        foreach (var row in rows)
            sb.AppendLine(row);

        return sb.ToString();
    }

    private static HashSet<LogType> ResolveAdminLogTypes(string[]? requested)
    {
        var defaults = new HashSet<LogType>
        {
            LogType.Chat,
            LogType.AdminMessage,
            LogType.Damaged,
            LogType.MeleeHit,
            LogType.BulletHit,
            LogType.AttackUnarmedClick,
            LogType.AttackUnarmedWide,
            LogType.Throw,
            LogType.Pickup,
            LogType.Drop,
            LogType.DisarmedAction,
            LogType.InteractHand,
            LogType.Mind,
        };

        if (requested is not { Length: > 0 })
            return defaults;

        var types = new HashSet<LogType>();
        foreach (var raw in requested)
        {
            if (Enum.TryParse<LogType>(raw.Trim(), true, out var type) && Enum.IsDefined(type))
                types.Add(type);
        }

        return types.Count == 0 ? defaults : types;
    }

    /// <summary>
    /// Resolves a player by ckey, character name or last seen user name. Online players are found on
    /// the main thread, offline ones in the server database.
    /// </summary>
    private async Task<LoggedPlayer?> ResolveAdminTargetAsync(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        var trimmed = query.Trim().TrimStart('@');
        if (trimmed.Length == 0)
            return null;

        var online = await RunAdminMainThreadAsync(() => ResolveOnlineAdminTarget(trimmed));
        if (online != null)
            return online;

        try
        {
            var record = await _db.GetPlayerRecordByUserName(trimmed);
            if (record != null)
                return new LoggedPlayer(record.UserId, record.LastSeenUserName, null);
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Neuro ahelp: player lookup failed for '{trimmed}': {e.Message}");
        }

        return null;
    }

    /// <summary>Must run on the main thread: reads entity names.</summary>
    private LoggedPlayer? ResolveOnlineAdminTarget(string query)
    {
        if (_playerManager.TryGetSessionByUsername(query, out var byName))
            return new LoggedPlayer(byName.UserId, byName.Name, CharacterName(byName));

        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { } entity)
                continue;

            var character = Identity.Name(entity, EntityManager);
            if (!string.Equals(character, query, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(Name(entity), query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return new LoggedPlayer(session.UserId, session.Name, character);
        }

        return null;
    }

    private string? CharacterName(ICommonSession session)
    {
        return session.AttachedEntity is { } entity ? Identity.Name(entity, EntityManager) : null;
    }

    /// <summary>Notes and bans of a player, trimmed for the model.</summary>
    private async Task<string> GetAdminNotesAsync(AdminAction action)
    {
        var target = await ResolveAdminTargetAsync(action.Ckey);
        if (target == null)
            return "Игрок не найден. Укажи ckey из list_players.";

        var remarks = await _adminNotes.GetAllAdminRemarks(target.Value.UserId.UserId);
        if (remarks.Count == 0)
            return $"Заметок и банов у {target.Value.Ckey} нет.";

        var sb = new StringBuilder();
        sb.AppendLine($"Заметки и баны {target.Value.Ckey}:");
        var count = 0;
        foreach (var note in remarks.OrderByDescending(n => n.CreatedAt))
        {
            if (count >= 15 || sb.Length > 3000)
                break;

            count++;
            var kind = note switch
            {
                BanNoteRecord => "бан",
                AdminWatchlistRecord => "вотчлист",
                AdminMessageRecord => "сообщение",
                _ => "заметка",
            };
            var severity = note switch
            {
                BanNoteRecord ban => ban.Severity,
                AdminNoteRecord adminNote => adminNote.Severity,
                _ => NoteSeverity.None,
            };
            var secret = note is AdminNoteRecord { Secret: true } ? "секретная | " : string.Empty;
            var message = note.Message.Replace('\n', ' ').Replace('\r', ' ');
            if (message.Length > 200)
                message = message[..200] + "…";

            sb.AppendLine($"{note.CreatedAt:yyyy-MM-dd} | {kind} | {severity} | {secret}{message}");
        }

        return sb.ToString();
    }

    /// <summary>Warn: a note for the staff plus a message to the player in ahelp.</summary>
    private async Task<string> WarnAdminAsync(AdminTicket ticket, AdminAction action)
    {
        if (!_ahelpWarningsEnabled)
            return "Варны выключены на сервере.";

        var reason = PickAdminReason(action);
        if (reason.Length == 0)
            return "Не указана причина варна.";

        var target = await ResolveAdminTargetAsync(action.Ckey ?? ticket.PlayerName);
        if (target == null)
            return "Игрок не найден.";

        var now = DateTime.UtcNow;
        if (_ahelpWarnTimes.TryGetValue(target.Value.UserId, out var last) && now - last < AdminWarnCooldown)
            return $"Варн {target.Value.Ckey} уже выдавался меньше 10 минут назад.";

        var note = "[ИИ-админ] Предупреждение: " + reason;
        try
        {
            await _adminNotes.AddAiRemark(target.Value.UserId.UserId, NoteType.Note, note, NoteSeverity.Medium,
                false, null);
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Neuro ahelp: failed to write the AI note: {e.Message}");
            return "Не удалось записать заметку, варн не выдан.";
        }

        _ahelpWarnTimes[target.Value.UserId] = DateTime.UtcNow;

        await SendAdminAHelpLineAsync(target.Value.UserId, "Предупреждение: " + reason);
        await RunAdminMainThreadAsync(() => _adminLog.Add(LogType.AdminMessage, LogImpact.Medium,
            $"[ИИ-админ] варн {target.Value.Ckey}: {reason}"));

        return $"Варн выдан {target.Value.Ckey}.";
    }

    /// <summary>Server ban with guardrails: online target, evidence in this ticket, one ban max.</summary>
    private async Task<string> BanAdminAsync(AdminTicket ticket, AdminAction action)
    {
        if (!_ahelpBansEnabled)
            return "Баны выключены на сервере.";

        var reason = PickAdminReason(action);
        if (reason.Length == 0)
            return "Не указана причина бана.";

        if (ticket.BanIssued)
            return "В этом тикете бан уже выдавался.";

        var target = await ResolveAdminTargetAsync(action.Ckey ?? ticket.PlayerName);
        if (target == null)
            return "Игрок не найден.";

        if (!ticket.Evidence.Contains(target.Value.UserId))
            return "Нет логов с участием цели. Сначала сделай read_logs по этой цели и убедись в нарушении.";

        var online = await RunAdminMainThreadAsync(() => _playerManager.TryGetSessionById(target.Value.UserId, out _));
        if (!online)
            return "Цель не в сети, серверный бан не выдан.";

        var maxHours = Math.Max(1, _ahelpMaxBanHours);
        var hours = Math.Clamp(action.Hours ?? 0, 1, maxHours);
        var banReason = "[ИИ-админ] " + reason;

        await RunAdminMainThreadAsync(() =>
        {
            var info = new CreateServerBanInfo(banReason);
            info.AddUser(target.Value.UserId, target.Value.Ckey);
            info.WithMinutes(hours * 60);
            info.WithSeverity(NoteSeverity.High);
            _banManager.CreateServerBan(info);

            // BanManager already announces the ban itself, so no extra admin alert here.
            _adminLog.Add(LogType.AdminMessage, LogImpact.High,
                $"[ИИ-админ] серверный бан {target.Value.Ckey} на {hours} ч: {reason}");
        });

        ticket.BanIssued = true;

        return $"Бан {target.Value.Ckey} на {hours} ч выдан.";
    }

    /// <summary>Hand the ticket to the live staff and answer the player.</summary>
    private async Task<string> EscalateAdminAsync(AdminTicket ticket, AdminAction action)
    {
        var reason = PickAdminReason(action);
        if (reason.Length == 0)
            reason = "требуется решение администратора";

        await RunAdminMainThreadAsync(() =>
        {
            _chatManager.SendAdminAlert($"[ИИ-админ] эскалация по тикету {ticket.PlayerName}: {reason}");
            _adminLog.Add(LogType.AdminMessage, LogImpact.Medium,
                $"[ИИ-админ] эскалация {ticket.PlayerName}: {reason}");
        });

        var reply = (action.Message ?? string.Empty).Trim();
        if (reply.Length == 0)
            reply = "Передал ваш вопрос администраторам.";

        await ReplyAdminAsync(ticket, reply);
        return "Эскалация отправлена администраторам.";
    }

    /// <summary>Model is unavailable: escalate once and tell the player, without spamming.</summary>
    private async Task FailAdminTicketAsync(AdminTicket ticket, string reason)
    {
        if (ticket.Replied)
            return;

        _sawmill.Warning($"Neuro ahelp: admin agent failed for {ticket.PlayerName}: {reason}");
        BroadcastAHelpStatus(ticket.UserId, NeuroAHelpState.Failed, $"не разобрал обращение: {reason}");

        try
        {
            await RunAdminMainThreadAsync(() =>
            {
                _chatManager.SendAdminAlert($"[ИИ-админ] обращение {ticket.PlayerName} передано администраторам: {reason}");
                _adminLog.Add(LogType.AdminMessage, LogImpact.Medium,
                    $"[ИИ-админ] не смог разобрать тикет {ticket.PlayerName}: {reason}");
            });

            await ReplyAdminAsync(ticket, "Не смог разобрать обращение, передал администраторам.");
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Neuro ahelp: cannot report the failed ticket {ticket.PlayerName}: {e.Message}");
        }
    }

    /// <summary>Turns the raw model reply into the final ahelp line and sends it to the player.</summary>
    private async Task ReplyAdminAsync(AdminTicket ticket, string text)
    {
        if (ticket.Replied)
            return;

        text = CleanAdminReply(text);
        if (text.Length == 0)
            return;

        if (!TryScreenText($"ahelp/{ticket.PlayerName}", text, blockAiTalk: false))
            return;

        var sent = await RunAdminMainThreadAsync(() =>
        {
            // A live admin could have answered while the model was thinking: then the AI stays quiet.
            // Форсированный тикет (/ai answer) админ запустил явно - его ответ живого админа не снимает.
            if (!ticket.Forced &&
                _ahelpLastStaffMessage.TryGetValue(ticket.UserId, out var staffAt) && staffAt > ticket.AskedAt)
            {
                _sawmill.Info($"Neuro ahelp: admin answered {ticket.PlayerName} first, AI reply dropped.");
                BroadcastAHelpStatus(ticket.UserId, NeuroAHelpState.Silent, "тихо: тикет взял администратор");
                return false;
            }

            if (!_playerManager.TryGetSessionById(ticket.UserId, out _))
                return false;

            var line = $"[color={AHelpColor}][bold]{FormattedMessage.EscapeText(_ahelpName)}[/bold][/color]: " +
                       FormattedMessage.EscapeText(text);

            _bwoink.SendPlayerChannelSystemMessage(ticket.UserId, line);
            _adminLog.Add(LogType.AdminMessage, LogImpact.Low,
                $"Neuro ahelp answered {ticket.PlayerName}: {text}");

            // Keep our own line in the ticket context for follow-up messages.
            _ahelpAssistantLines.Enqueue((ticket.UserId, text));
            return true;
        });

        if (sent)
        {
            ticket.Replied = true;
            BroadcastAHelpStatus(ticket.UserId, NeuroAHelpState.Done, "ответ отправлен");
        }
    }

    /// <summary>A short ahelp line from the AI, without ending the ticket (used by warn).</summary>
    private async Task SendAdminAHelpLineAsync(NetUserId userId, string text)
    {
        text = CleanAdminReply(text);
        if (text.Length == 0)
            return;

        if (!TryScreenText($"ahelp/{userId}", text, blockAiTalk: false))
            return;

        await RunAdminMainThreadAsync(() =>
        {
            if (!_playerManager.TryGetSessionById(userId, out _))
                return;

            var line = $"[color={AHelpColor}][bold]{FormattedMessage.EscapeText(_ahelpName)}[/bold][/color]: " +
                       FormattedMessage.EscapeText(text);

            _bwoink.SendPlayerChannelSystemMessage(userId, line);
        });
    }

    private string CleanAdminReply(string text)
    {
        text = text.Replace('—', '-').Replace('–', '-');
        // Markdown-артефакты: выделения жирным/курсивом и ограждения кода игроку не нужны.
        text = text.Replace('*', ' ').Replace('`', '"');
        // Модель иногда начинает ответ с собственного имени: в чате имя и так нарисовано.
        var namePrefix = _ahelpName + ":";
        while (text.TrimStart().StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase))
            text = text.TrimStart()[namePrefix.Length..].TrimStart();

        text = string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();
        return TrimAHelpReply(text, Math.Min(_ahelpMaxReplyChars, MaxAHelpReplyLength));
    }

    private static string PickAdminReason(AdminAction action)
    {
        var text = (action.Reason ?? action.Message ?? string.Empty).Trim();
        return text.Replace('—', '-').Replace('–', '-');
    }

    private static bool TryParseAdminAction(string raw, out AdminAction action)
    {
        action = new AdminAction();

        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start < 0 || end <= start)
            return false;

        var json = raw[start..(end + 1)];
        try
        {
            var parsed = JsonSerializer.Deserialize<AdminAction>(json, AdminActionJson);
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.Action))
                return false;

            action = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Ticket history from the shared ahelp context. Must run on the main thread.</summary>
    private string BuildAdminTranscript(NetUserId userId)
    {
        if (!_ahelpContext.TryGetValue(userId, out var context) || context.Count == 0)
            return "(сообщений пока нет)";

        var sb = new StringBuilder();
        foreach (var (speaker, text) in context)
        {
            var who = speaker switch
            {
                AHelpSpeaker.Player => "Игрок",
                AHelpSpeaker.Staff => "Администратор",
                _ => _ahelpName,
            };

            var line = text.Replace('\n', ' ').Replace('\r', ' ');
            if (line.Length > 400)
                line = line[..400] + "…";

            sb.AppendLine($"{who}: {line}");
        }

        return sb.ToString();
    }

    private string BuildAdminUserPrompt(AdminTicket ticket, string transcript)
    {
        var maxSteps = Math.Clamp(_ahelpMaxActionSteps, 1, 12);
        var sb = new StringBuilder();
        sb.AppendLine($"Обращение в админ-хелп. Игрок: {ticket.PlayerName}.");
        sb.AppendLine("История тикета (старые сообщения сверху):");
        sb.AppendLine(transcript);
        sb.AppendLine();
        sb.AppendLine("Журнал твоих действий и результатов:");
        if (ticket.Journal.Count == 0)
        {
            sb.AppendLine("(пока пусто)");
        }
        else
        {
            foreach (var line in ticket.Journal)
                sb.AppendLine(line);
        }

        sb.AppendLine();
        sb.AppendLine($"Сейчас шаг {Math.Min(ticket.Steps, maxSteps)} из {maxSteps}.");
        sb.AppendLine("Верни РОВНО ОДИН JSON-объект следующего действия, без текста вокруг и без markdown.");
        return sb.ToString();
    }

    /// <summary>
    /// The AI administrator system prompt: role, duties, rules, action protocol, knowledge file and
    /// server extra instructions. Roughly 8-12k characters together with the knowledge file.
    /// </summary>
    private string BuildAdminSystemPrompt()
    {
        var wiki = EffectiveWikiUrl;
        var discord = _cfg.GetCVar(CCVars.InfoLinksDiscord);
        var maxHours = Math.Max(1, _ahelpMaxBanHours);

        var prompt = new StringBuilder();
        prompt.AppendLine($"Ты - «{_ahelpName}», ИИ-администратор сервера «Мини-Станция»: русский МРП-сервер по Space Station 14 (форк Goob-Station с мини-картами).");
        prompt.AppendLine("Ты ведешь админ-хелп (ahelp) и подменяешь живого администратора, пока его нет за клавиатурой.");
        prompt.AppendLine();
        prompt.AppendLine("Обязанности:");
        prompt.AppendLine("1. Разбирать конфликты игроков и выяснять, что произошло на самом деле.");
        prompt.AppendLine("2. Проверять слова игроков по логам раунда и заметкам, а не верить на слово.");
        prompt.AppendLine("3. Помогать по механике, правилам и возможностям сборки.");
        prompt.AppendLine($"4. Выдавать предупреждения (варны) и, в крайнем случае, временные баны до {maxHours} часов.");
        prompt.AppendLine();
        prompt.AppendLine("Правила:");
        prompt.AppendLine("- Сначала собери факты: read_logs и get_notes, только потом решение.");
        prompt.AppendLine("- Варн всегда раньше бана. Обычный конфликт или грубость разбирай варном и объяснением.");
        prompt.AppendLine("- Бан только при доказанном серьезном нарушении: массовый грифер, читы, намеренная порча раунда, повторные нарушения после варна.");
        prompt.AppendLine("- Не бань антагонистов за их игровую роль и за RP: антагонист делает то, что ему разрешает роль.");
        prompt.AppendLine("- Не раскрывай игроку содержимое логов, заметок, служебные данные, роли и антагонистов.");
        prompt.AppendLine("- Не выдавай предметы, не телепортируй, не снимай и не меняй наказания.");
        prompt.AppendLine("- Если ситуация спорная, данных не хватает или нужна ручная проверка, используй escalate.");
        prompt.AppendLine("- Говори коротко, спокойно и по делу. Без длинных тире, без звездочек и описаний действий.");
        prompt.AppendLine("- Игроку пиши 1-4 коротких предложения по-русски, только по сути обращения.");
        prompt.AppendLine("- Ты ИИ-администратор, но не обязан это подчеркивать; на прямой вопрос отвечай честно.");
        prompt.AppendLine();
        prompt.AppendLine("Протокол действий:");
        prompt.AppendLine("Каждый твой ответ - РОВНО ОДИН JSON-объект, без markdown и без текста вокруг. В одном ответе только одно действие.");
        prompt.AppendLine("Действия и поля:");
        prompt.AppendLine("- {\"action\":\"list_players\"}: список онлайн игроков (ckey, имя персонажа, профессия).");
        if (_ahelpBuildSearch)
            prompt.AppendLine("- {\"action\":\"search_build\",\"query\":\"хлеб\"}: поиск по фактам этой сборки (прототипы, русские названия, гайдбук, правила сервера). Ищи по точному названию предмета, профессии, механики или словам из правила, по-русски или по-английски. Вернет файл и строки из сборки.");
        if (_ahelpWebEnabled)
            prompt.AppendLine("- {\"action\":\"read_url\",\"url\":\"https://...\"}: прочитать страницу вики как текст. Разрешены только домены вики и разрешенные сервером домены. Для MediaWiki-вики добавляй ?action=raw к адресу страницы, так чище.");
        prompt.AppendLine("- {\"action\":\"read_logs\",\"ckey\":\"ivan\",\"minutes\":10,\"types\":[\"Chat\",\"Damaged\"],\"search\":\"убили\"}: логи текущего раунда по игроку. minutes 1..60 (по умолчанию 10). types можно не указывать, тогда берутся основные боевые и чат-логи. search ищет по тексту лога.");
        prompt.AppendLine("- {\"action\":\"get_notes\",\"ckey\":\"ivan\"}: заметки и баны игрока.");
        prompt.AppendLine("- {\"action\":\"warn\",\"ckey\":\"ivan\",\"reason\":\"...\"}: предупреждение игроку и заметка. Не чаще одного раза в 10 минут на игрока.");
        prompt.AppendLine($"- {{\"action\":\"ban\",\"ckey\":\"ivan\",\"hours\":3,\"reason\":\"...\"}}: серверный бан. hours 1..{maxHours}. Цель должна быть онлайн, а в этом тикете уже должен быть успешный read_logs по ней. Максимум один бан за тикет.");
        prompt.AppendLine("- {\"action\":\"escalate\",\"reason\":\"...\",\"message\":\"...\"}: передать администраторам и ответить игроку.");
        prompt.AppendLine("- {\"action\":\"reply\",\"message\":\"...\"}: ответ игроку. 1-4 коротких предложения, без логов и заметок.");
        prompt.AppendLine("- {\"action\":\"done\"}: закрыть тикет без ответа.");
        prompt.AppendLine();
        prompt.AppendLine("Порядок работы: list_players -> read_logs по цели -> get_notes -> решение (warn, reply или ban).");
        prompt.AppendLine("Не делай больше 3-4 read_logs подряд и не повторяй одно и то же действие без новой информации.");
        prompt.AppendLine("Типы логов: Chat, AdminMessage, Damaged, MeleeHit, BulletHit, AttackUnarmedClick, AttackUnarmedWide, Throw, Pickup, Drop, DisarmedAction, InteractHand, Mind.");
        prompt.AppendLine("Если игрок спрашивает про механику, отвечай по фактам сборки и не выдумывай механик.");
        if (_ahelpBuildSearch)
        {
            prompt.AppendLine("Перед ответом про предмет, профессию, рецепт или режим проверь себя через search_build: сначала ищи русское название, при неудаче - английское. Нашел факт - отвечай по нему. Ничего не нашел - честно скажи, что не уверен, и эскалируй.");
            prompt.AppendLine("Про правила сервера тоже проверяй search_build: текст правил лежит в сборке и ищется по словам.");
        }

        prompt.AppendLine("Ссылки игроку давай ТОЛЬКО те, что перечислены в этом промпте, или полученные через read_url. Никогда не придумывай и не изменяй адреса: неверная ссылка хуже отсутствия.");

        if (!string.IsNullOrWhiteSpace(wiki) || !string.IsNullOrWhiteSpace(discord))
        {
            prompt.Append("Полезные ссылки для игроков (единственные разрешенные адреса):");
            if (!string.IsNullOrWhiteSpace(wiki))
                prompt.Append($" вики {wiki} (проверить содержание страницы можно через read_url);");
            if (!string.IsNullOrWhiteSpace(discord))
                prompt.Append($" Discord {discord};");
            prompt.AppendLine(" гайдбук в игре открывается по Numpad 0.");
        }

        var knowledge = GetAHelpKnowledge();
        if (!string.IsNullOrWhiteSpace(knowledge))
        {
            prompt.AppendLine();
            prompt.AppendLine("Факты про эту сборку (используй их, а не догадки):");
            prompt.AppendLine(knowledge);
        }

        if (!string.IsNullOrWhiteSpace(_ahelpExtraPrompt))
        {
            prompt.AppendLine();
            prompt.AppendLine("Дополнительные указания сервера:");
            prompt.AppendLine(_ahelpExtraPrompt);
        }

        return prompt.ToString();
    }
}
