// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;

namespace Content.Server.GameTicking;

/// <summary>
///     Prepares the round end manifest for the Discord webhook.
/// </summary>
public static partial class DiscordRoundEndFormatter
{
    /// <summary>
    ///     Discord rejects message content longer than this many characters.
    /// </summary>
    public const int MaxMessageLength = 2000;

    /// <summary>
    ///     Removes SS14 markup tags. In SS14 markup \[ and \] are literal brackets.
    /// </summary>
    public static string StripMarkup(string text)
    {
        var noTags = TagRegex().Replace(text, string.Empty);
        return EscapedBracketRegex().Replace(noTags, "$1");
    }

    /// <summary>
    ///     Cuts text to at most <paramref name="limit"/> characters, preferably at a line end, marking the cut with an
    ///     ellipsis.
    /// </summary>
    public static string TruncateAtLine(string text, int limit)
    {
        if (text.Length <= limit)
            return text;

        if (limit <= 1)
            return "…";

        var cut = limit - 1; // room for the ellipsis
        var newline = text.LastIndexOf('\n', cut);
        if (newline >= 0)
            cut = newline;

        // Never cut between the halves of a surrogate pair: Discord rejects such content.
        if (cut > 0 && char.IsHighSurrogate(text[cut - 1]))
            cut--;

        return text[..cut] + "…";
    }

    [GeneratedRegex(@"(?<!\\)\[[^\[\]\n]*\]")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\\([\[\]])")]
    private static partial Regex EscapedBracketRegex();
}
