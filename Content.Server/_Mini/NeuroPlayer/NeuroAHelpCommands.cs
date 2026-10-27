// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Mini.NeuroPlayer;

/// <summary>Какую команду /ai ввел админ в ахелпе.</summary>
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
    /// <summary>Выключить ассистента совсем (до рестарта сервера или /ai global on).</summary>
    GlobalOff,
    /// <summary>Включить ассистента обратно.</summary>
    GlobalOn,
}

/// <summary>Разобранная команда: что сделать и с каким числом минут.</summary>
public sealed record NeuroAHelpCommand(NeuroAHelpCommandKind Kind, int Minutes);

/// <summary>
/// Мини: команды админов нейропомощнику, вводимые прямо в ахелпе ("/ai off", "/ai status").
/// Разбор детерминированный, без LLM; строки нечувствительны к регистру.
/// </summary>
public static class NeuroAHelpCommands
{
    public const string Prefix = "/ai";
    public const string PrefixRu = "/нейро";

    /// <summary>Является ли строка командой ассистенту (проверяется до отправки игроку и в вебхук).</summary>
    public static bool IsCommand(string text)
    {
        return MatchesPrefix(text, Prefix) || MatchesPrefix(text, PrefixRu);
    }

    /// <summary>
    /// Разбирает команду. minutes знает только Off; отрицательное число или мусор - ошибка.
    /// </summary>
    public static bool TryParse(string text, out NeuroAHelpCommand command, out string error)
    {
        command = new NeuroAHelpCommand(NeuroAHelpCommandKind.None, 0);
        error = string.Empty;

        var body = MatchesPrefix(text, PrefixRu)
            ? text[PrefixRu.Length..]
            : MatchesPrefix(text, Prefix)
                ? text[Prefix.Length..]
                : null;

        if (body == null)
        {
            error = "Это не команда ассистенту.";
            return false;
        }

        // Регистр, ё/е и лишние пробелы не должны ломать разбор.
        body = body.Trim().Replace('ё', 'е').ToLowerInvariant();

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

        if (body == "answer" || body == "ответ")
        {
            command = command with { Kind = NeuroAHelpCommandKind.Answer };
            return true;
        }

        if (body == "reset" || body == "сброс")
        {
            command = command with { Kind = NeuroAHelpCommandKind.Reset };
            return true;
        }

        if (body == "global off" || body == "офф глобально" || body == "globalOff")
        {
            command = command with { Kind = NeuroAHelpCommandKind.GlobalOff };
            return true;
        }

        if (body == "global on" || body == "вкл глобально" || body == "globalOn")
        {
            command = command with { Kind = NeuroAHelpCommandKind.GlobalOn };
            return true;
        }

        if (body == "off" || body == "выкл")
        {
            command = command with { Kind = NeuroAHelpCommandKind.Off };
            return true;
        }

        // "off 45" / "выкл 45" - мут на N минут; число может прийти и до слова ("/ai 45 off").
        if (body.StartsWith("off") || body.StartsWith("выкл") ||
            body.EndsWith("off") || body.EndsWith("выкл"))
        {
            var minutesPart = body
                .Replace("off", string.Empty)
                .Replace("выкл", string.Empty)
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

            error = "Не поняла минуты. Пример: /ai off 45 (или /ai off - до конца раунда).";
            return false;
        }

        error = "Неизвестная команда. Список: /ai help.";
        return false;
    }

    /// <summary>Префикс с границей слова: "/ai" ловится, "/airlock rule" - нет.</summary>
    private static bool MatchesPrefix(string text, string prefix)
    {
        if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        return text.Length == prefix.Length || !char.IsLetterOrDigit(text[prefix.Length]);
    }
}

/// <summary>
/// Событие, которое BwoinkSystem поднимает, когда админ пишет команду ассистенту в ахелпе.
/// Сообщение до игрока и в вебхук не доходит.
/// </summary>
public sealed record AHelpAdminCommandEvent(ICommonSession Sender, string Text, NetUserId Channel);
