// SPDX-FileCopyrightText: 2026 Mini Station contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
using Content.Client.UserInterface.RichText;
using Content.Goobstation.UIKit.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.Administration.UI.Bwoink;

/// <summary>
/// Mini: turns URLs in AHelp messages into clickable links.
/// The server escapes player input, so URLs arrive as plain text; every match is wrapped into a
/// self-closing <see cref="UriLinkTag"/> tag before the message is parsed as markup.
/// </summary>
public static class AHelpLinkHelper
{
    /// <summary>
    /// Longest visible link text. Longer URLs are shortened with an ellipsis (the full target stays
    /// in the tooltip): an inline control wider than the chat line would break word wrapping.
    /// </summary>
    public const int MaxDisplayLength = 48;

    // http(s):// and bare www. links. The character set excludes everything that could break markup
    // (brackets, quotes, backslashes) so the inserted tag is always well formed.
    private static readonly Regex UrlRegex = new(
        @"(?<![A-Za-z0-9_])(?<url>(?:https?://|www\.)[^\s<>""'\\\[\]{}]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Tags allowed in AHelp output: everything chat already supports, plus clickable links.
    /// </summary>
    public static readonly Type[] AllowedTags = [.. CustomRichTextEntry.DefaultTags, typeof(UriLinkTag)];

    /// <summary>
    /// Wraps all URLs found in <paramref name="markup"/> into <see cref="UriLinkTag"/> tags.
    /// URLs that are already inside a tag (for example in an attribute) are left untouched because
    /// tags never contain absolute URLs.
    /// </summary>
    public static string Linkify(string markup)
    {
        if (string.IsNullOrEmpty(markup))
            return markup;

        return UrlRegex.Replace(markup, match =>
        {
            var raw = match.Groups["url"].Value;
            var trimmed = TrimTrailingPunctuation(raw);
            if (trimmed.Length == 0)
                return match.Value;

            var target = trimmed.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                ? $"https://{trimmed}"
                : trimmed;

            if (!LooksLikeHttpUrl(target))
                return match.Value;

            var display = ShortenForDisplay(trimmed);
            var textAttribute = string.Equals(target, display, StringComparison.Ordinal)
                ? string.Empty
                : $" text=\"{FormattedMessage.EscapeStringParameter(display)}\"";

            var link = $"[{UriLinkTag.TagName}=\"{FormattedMessage.EscapeStringParameter(target)}\"{textAttribute} /]";

            // Keep the punctuation that was cut off from the link as normal text.
            return link + raw[trimmed.Length..];
        });
    }

    /// <summary>
    /// Removes sentence punctuation that directly follows a URL and an unbalanced closing bracket,
    /// so "see https://example.com/page, then" links only the URL itself.
    /// </summary>
    private static string TrimTrailingPunctuation(string url)
    {
        var end = url.Length;
        while (end > 0)
        {
            var c = url[end - 1];
            if (c is '.' or ',' or ';' or ':' or '!' or '?' or '\'' or '"')
            {
                end--;
                continue;
            }

            if (c == ')' && Count(url, end, ')') > Count(url, end, '('))
            {
                end--;
                continue;
            }

            break;
        }

        return url[..end];
    }

    private static string ShortenForDisplay(string text)
    {
        if (text.Length <= MaxDisplayLength)
            return text;

        return string.Concat(text.AsSpan(0, MaxDisplayLength - 3), "...");
    }

    private static int Count(string value, int length, char c)
    {
        var count = 0;
        for (var i = 0; i < length; i++)
        {
            if (value[i] == c)
                count++;
        }

        return count;
    }

    private static bool LooksLikeHttpUrl(string url)
    {
        var prefixLength = url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? "https://".Length
            : url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                ? "http://".Length
                : 0;

        // Require at least something that can be a host after the scheme.
        return prefixLength > 0
               && url.Length > prefixLength
               && url[prefixLength] is not ('.' or ',' or ';' or ':' or '!' or '?' or '/' or '#');
    }
}
