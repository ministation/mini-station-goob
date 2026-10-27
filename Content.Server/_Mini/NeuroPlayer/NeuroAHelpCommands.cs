// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Mini.NeuroPlayer;

/// <summary>Какую команду ввел админ в ахелпе.</summary>
public enum NeuroAHelpCommandKind : byte
{
    None,
    Help,
    Status,
    /// <summary>Замутить ИИ в тикете (Minutes &gt; 0 - на столько минут, 0 - до конца раунда).</summary>
    Off,
    /// <summary>Размутить ИИ в тикете.</summary>
    On,
    /// <summary>Заставить ИИ ответить прямо сейчас, игнорируя тишину после ответа админа.</summary>
    Answer,
    /// <summary>Стереть контекст тикета.</summary>
    Reset,
    /// <summary>Выключить ассистента совсем (до рестарта сервера или включения обратно).</summary>
    GlobalOff,
    /// <summary>Включить ассистента обратно.</summary>
    GlobalOn,
}

/// <summary>Разобранная команда: что сделать и с каким числом минут.</summary>
public sealed record NeuroAHelpCommand(NeuroAHelpCommandKind Kind, int Minutes);

/// <summary>
/// Мини: команды админов нейропомощнику, вводимые прямо в ахелпе коротким слэшем: "/stop",
/// "/on", "/answer", "/reset", "/status", "/global off". Разбор детерминированный, без LLM;
/// строки нечувствительны к регистру. Префикс "/ai" оставлен как совместимый алиас ("/ai stop").
/// Нераспознанная строка командой не считается и уходит игроку как обычное сообщение.
/// </summary>
public static class NeuroAHelpCommands
{
    public const string Prefix = "/ai";
    public const string PrefixRu = "/нейро";

    /// <summary>Является ли строка командой ассистенту (проверяется до отправки игроку и в вебхук).</summary>
    public static bool IsCommand(string text)
    {
        return TryParse(text, out _, out _);
    }

    /// <summary>
    /// Разбирает команду: "/stop 30", "/on", "/answer", "/reset", "/status", "/global off|on".
    /// minutes знает только stop; отрицательное число или мусор - ошибка.
    /// </summary>
    public static bool TryParse(string text, out NeuroAHelpCommand command, out string error)
    {
        command = new NeuroAHelpCommand(NeuroAHelpCommandKind.None, 0);
        error = string.Empty;

        if (!text.StartsWith('/'))
        {
            error = "Это не команда ассистенту.";
            return false;
        }

        var body = text[1..].Trim();

        // Совместимость со старым префиксом: "/ai stop", "/нейро status".
        foreach (var alias in (string[])[Prefix[1..], PrefixRu[1..]])
        {
            if (body.StartsWith(alias, StringComparison.OrdinalIgnoreCase) &&
                (body.Length == alias.Length || !char.IsLetterOrDigit(body[alias.Length])))
            {
                body = body[alias.Length..].Trim();
                break;
            }
        }

        // Регистр, ё/е и лишние пробелы не должны ломать разбор.
        body = body.Replace('ё', 'е').ToLowerInvariant();

        if (body.Length == 0 || body == "help" || body == "помощь" || body == "?")
        {
            command = command with { Kind = NeuroAHelpCommandKind.Help };
            return true;
        }

        if (body == "status" || body == "статус")
        {
            command = command with { Kind = NeuroAHelpCommandKind.Status };
            return true;
        }

        if (body == "on" || body == "вкл")
        {
            command = command with { Kind = NeuroAHelpCommandKind.On };
            return true;
        }

        if (body == "answer" || body == "go" || body == "ответ")
        {
            command = command with { Kind = NeuroAHelpCommandKind.Answer };
            return true;
        }

        if (body == "reset" || body == "сброс")
        {
            command = command with { Kind = NeuroAHelpCommandKind.Reset };
            return true;
        }

        if (body == "global off" || body == "глобально выкл" || body == "globaloff")
        {
            command = command with { Kind = NeuroAHelpCommandKind.GlobalOff };
            return true;
        }

        if (body == "global on" || body == "глобально вкл" || body == "globalon")
        {
            command = command with { Kind = NeuroAHelpCommandKind.GlobalOn };
            return true;
        }

        // "stop" / "stop 45" / "45 stop" - ИИ молчит в тикете, число = минуты.
        if (body.StartsWith("stop") || body.StartsWith("стоп") ||
            body.EndsWith("stop") || body.EndsWith("стоп"))
        {
            var minutesPart = body
                .Replace("stop", string.Empty)
                .Replace("стоп", string.Empty)
                .Trim();
            if (minutesPart.Length == 0)
            {
                command = command with { Kind = NeuroAHelpCommandKind.Off };
                return true;
            }

            if (int.TryParse(minutesPart, out var minutes) && minutes >= 0)
            {
                command = command with { Kind = NeuroAHelpCommandKind.Off, Minutes = minutes };
                return true;
            }

            error = "Не поняла минуты. Пример: /stop 45 (или /stop - до конца раунда).";
            return false;
        }

        error = "Неизвестная команда. Список: /stop [минут], /on, /answer, /reset, /status, /global off|on.";
        return false;
    }
}

/// <summary>
/// Событие, которое BwoinkSystem поднимает, когда админ пишет команду ассистенту в ахелпе.
/// Сообщение до игрока и в вебхук не доходит.
/// </summary>
public sealed record AHelpAdminCommandEvent(ICommonSession Sender, string Text, NetUserId Channel);
