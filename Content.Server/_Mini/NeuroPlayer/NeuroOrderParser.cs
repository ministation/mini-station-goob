// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Text;

namespace Content.Server._Mini.NeuroPlayer;

/// <summary>Короткий приказ игрока нейроигроку.</summary>
public enum NeuroOrderKind
{
    None,
    Follow,
    Stop,
    CleanTrash,
}

/// <summary>
/// Мини: детерминированный разбор коротких русских команд ("иди за мной", "стой", "убери мусор").
/// LLM здесь не нужен: приказ срабатывает сразу и ничего не стоит. Фразы ищутся по границам слов,
/// чтобы "идем" не ловило "придем", а регистр, пунктуация и ё/е игрока не мешали.
/// </summary>
public static class NeuroOrderParser
{
    private static readonly string[] FollowPhrases =
    [
        "пошли со мной",
        "пойдем со мной",
        "идем со мной",
        "иди со мной",
        "иди за мной",
        "следуй за мной",
        "за мной",
        "пошли",
        "идем",
        "помоги мне",
        "нужна помощь",
        "нужна твоя помощь",
        "пошли помогать",
    ];

    private static readonly string[] StopPhrases =
    [
        "стой",
        "стоп",
        "отстань",
        "хватит",
        "остановись",
        "жди здесь",
        "жди тут",
        "не иди",
    ];

    private static readonly string[] CleanPhrases =
    [
        "убери мусор",
        "уберись",
        "убери тут",
        "убери здесь",
        "убери в баре",
        "убери бар",
        "собери мусор",
        "выброси мусор",
    ];

    /// <summary>
    /// Распознает приказ в реплике. Stop проверяется первым: "не иди за мной" - это стоп, а не follow.
    /// </summary>
    public static bool TryParse(string message, out NeuroOrderKind kind)
    {
        kind = NeuroOrderKind.None;
        if (string.IsNullOrWhiteSpace(message))
            return false;

        var text = Normalize(message);

        if (ContainsAny(text, StopPhrases))
        {
            kind = NeuroOrderKind.Stop;
            return true;
        }

        if (ContainsAny(text, CleanPhrases))
        {
            kind = NeuroOrderKind.CleanTrash;
            return true;
        }

        if (ContainsAny(text, FollowPhrases))
        {
            kind = NeuroOrderKind.Follow;
            return true;
        }

        return false;
    }

    /// <summary>Нижний регистр, ё -> е, вся пунктуация становится пробелами, пробелы схлопнуты.</summary>
    private static string Normalize(string message)
    {
        var sb = new StringBuilder(message.Length + 2);
        sb.Append(' ');
        var lastWasSpace = true;

        foreach (var ch in message)
        {
            if (!char.IsLetterOrDigit(ch))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }

                continue;
            }

            var c = char.ToLowerInvariant(ch);
            sb.Append(c == 'ё' ? 'е' : c);
            lastWasSpace = false;
        }

        if (!lastWasSpace)
            sb.Append(' ');

        return sb.ToString();
    }

    private static bool ContainsAny(string paddedText, string[] phrases)
    {
        foreach (var phrase in phrases)
        {
            if (paddedText.Contains(' ' + phrase + ' ', StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
