// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._Mini.MiniCCVars;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Server._Mini.NeuroPlayer;

/// <summary>
/// Verification tools for the ahelp agent: search_build (full-text search over the mounted build
/// content - prototypes, ru locale, guidebook, server rules) and read_url (a tightly whitelisted
/// web reader for the wiki). Both are read-only and run on the agent's thread pool thread.
/// </summary>
/// <remarks>
/// The index holds one lowercase copy of every indexed file (~50 MB for this build), built lazily
/// on the first search and rebuilt after a day, so live content patches are picked up without a
/// restart guarantee. The web reader follows redirects manually and re-validates every hop against
/// the domain allowlist: HttpClient with auto-redirect would let a page bounce the reader into an
/// unapproved host.
/// </remarks>
public sealed partial class NeuroPlayerSystem
{
    private const int BuildIndexTtlHours = 24;
    private const int MaxIndexedFileChars = 1_500_000;
    private const int MaxSearchFiles = 8;
    private const int MaxSearchResultChars = 4200;
    private const int SearchWindowLines = 12;
    private const int MaxWebResultChars = 4200;
    private const int MaxRedirectHops = 3;

    private static readonly string[] IndexRoots =
    {
        "/ServerInfo/_Mini/",
        "/ServerInfo/",
        "/Locale/ru-RU/",
        "/Prototypes/",
    };

    private static readonly string[] IndexExtensions = { ".yml", ".ftl", ".xml", ".txt", ".md" };

    private static readonly Regex ScriptStyleRegex = new(
        @"<(script|style|noscript)\b[^>]*>.*?</\1>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BlockTagRegex = new(
        @"</?(br|p|div|li|tr|h[1-6]|table|ul|ol|section|article|blockquote|pre|header|footer)\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TagRegex = new(@"<[^>]+>", RegexOptions.Compiled);

    [Dependency] private readonly IResourceManager _res = default!;

    private bool _ahelpBuildSearch = true;
    private bool _ahelpWebEnabled;
    private string _ahelpWebDomains = string.Empty;

    /// <summary>Effective wiki link: neuroahelp.wiki_url overrides infolinks.wiki.</summary>
    private string _ahelpWikiOverride = string.Empty;
    private string _infolinksWiki = string.Empty;

    private readonly SemaphoreSlim _buildIndexLock = new(1, 1);
    private List<BuildIndexFile>? _buildIndex;
    private DateTime _buildIndexAtUtc = DateTime.MinValue;
    private bool _buildIndexWarned;

    private HttpClient? _webClient;

    private sealed record BuildIndexFile(string Path, string Text, int Group);

    private string EffectiveWikiUrl =>
        string.IsNullOrWhiteSpace(_ahelpWikiOverride) ? _infolinksWiki : _ahelpWikiOverride;

    private void InitializeBuildSearch()
    {
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpBuildSearch, v => _ahelpBuildSearch = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpWebEnabled, v => _ahelpWebEnabled = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpWebDomains, v => _ahelpWebDomains = v, true);
        Subs.CVar(_cfg, MiniCCVars.NeuroAHelpWikiUrl, v => _ahelpWikiOverride = v, true);
        Subs.CVar(_cfg, CCVars.InfoLinksWiki, v => _infolinksWiki = v, true);
    }

    /// <summary>search_build action: full-text search over the build content.</summary>
    private async Task<string> SearchBuildForAdminAsync(AdminAction action)
    {
        if (!_ahelpBuildSearch)
            return "Поиск по сборке выключен на сервере.";

        var query = (action.Search ?? action.Message ?? string.Empty).Trim();
        if (query.Length < 2)
            return "Укажи query - что искать: название предмета, профессии, механики или слова из правила.";

        if (query.Length > 120)
            query = query[..120];

        List<BuildIndexFile> index;
        try
        {
            index = await EnsureBuildIndexAsync();
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Neuro ahelp: build index failed: {e.Message}");
            return "Индекс сборки сейчас недоступен.";
        }

        var terms = query.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .Take(6)
            .ToList();

        var result = new StringBuilder();
        var filesShown = 0;

        foreach (var file in index.OrderBy(f => f.Group).ThenBy(f => f.Path, StringComparer.Ordinal))
        {
            if (filesShown >= MaxSearchFiles)
                break;

            var lines = FindBuildMatchWindow(file, terms);
            if (lines.Length == 0)
                continue;

            filesShown++;
            if (result.Length + lines.Length > MaxSearchResultChars)
            {
                result.AppendLine("…(результат обрезан, уточни запрос)");
                break;
            }

            result.AppendLine(lines);
        }

        if (filesShown == 0)
            return $"По запросу «{query}» в сборке ничего не найдено. Попробуй русское или английское название короче.";

        return result.ToString().TrimEnd();
    }

    /// <summary>
    /// Returns a formatted window (up to two) around matches in one file, or an empty string when
    /// the file does not contain every term.
    /// </summary>
    private static string FindBuildMatchWindow(BuildIndexFile file, List<string> terms)
    {
        // Match only files containing all terms, so multi-word queries ("хлеб рецепты") stay relevant.
        foreach (var term in terms)
        {
            if (!file.Text.Contains(term, StringComparison.Ordinal))
                return string.Empty;
        }

        var textLines = file.Text.Split('\n');
        var scored = new List<(int Index, int Hits)>();
        for (var i = 0; i < textLines.Length; i++)
        {
            var hits = terms.Count(t => textLines[i].Contains(t));
            if (hits > 0)
                scored.Add((i, hits));
        }

        if (scored.Count == 0)
            return string.Empty;

        // Best lines first, then two non-overlapping windows around them.
        scored.Sort((a, b) => b.Hits.CompareTo(a.Hits));
        var centers = new List<int>();
        foreach (var (index, _) in scored)
        {
            if (centers.All(c => Math.Abs(c - index) > SearchWindowLines))
                centers.Add(index);
            if (centers.Count >= 2)
                break;
        }

        centers.Sort();

        var sb = new StringBuilder();
        sb.AppendLine($"{file.Path} (совпадений: {scored.Count}):");
        foreach (var center in centers)
        {
            var from = Math.Max(0, center - 2);
            var to = Math.Min(textLines.Length - 1, center + SearchWindowLines);
            for (var i = from; i <= to; i++)
            {
                var line = textLines[i].TrimEnd('\r');
                if (line.Length > 200)
                    line = line[..200] + "…";

                sb.AppendLine($"  {i + 1}: {line}");
            }

            sb.AppendLine("  …");
        }

        return sb.ToString().TrimEnd();
    }

    private async Task<List<BuildIndexFile>> EnsureBuildIndexAsync()
    {
        if (_buildIndex != null && DateTime.UtcNow - _buildIndexAtUtc < TimeSpan.FromHours(BuildIndexTtlHours))
            return _buildIndex;

        await _buildIndexLock.WaitAsync();
        try
        {
            if (_buildIndex != null && DateTime.UtcNow - _buildIndexAtUtc < TimeSpan.FromHours(BuildIndexTtlHours))
                return _buildIndex;

            var index = await Task.Run(BuildIndex);
            _buildIndex = index;
            _buildIndexAtUtc = DateTime.UtcNow;

            if (!_buildIndexWarned)
            {
                _buildIndexWarned = true;
                _sawmill.Info($"Neuro ahelp: build index ready, {index.Count} files.");
            }

            return index;
        }
        finally
        {
            _buildIndexLock.Release();
        }
    }

    private List<BuildIndexFile> BuildIndex()
    {
        var files = new List<BuildIndexFile>();
        for (var group = 0; group < IndexRoots.Length; group++)
        {
            var root = new ResPath(IndexRoots[group]);
            IEnumerable<ResPath> paths;
            try
            {
                paths = _res.ContentFindFiles(root).ToList();
            }
            catch (Exception e)
            {
                _sawmill.Warning($"Neuro ahelp: cannot enumerate {IndexRoots[group]}: {e.Message}");
                continue;
            }

            foreach (var path in paths)
            {
                if (!IndexExtensions.Contains(path.Extension.ToLowerInvariant()))
                    continue;

                if (!_res.TryContentFileRead(path, out var stream))
                    continue;

                using (stream)
                {
                    string text;
                    try
                    {
                        using var reader = new StreamReader(stream, Encoding.UTF8);
                        text = reader.ReadToEnd();
                    }
                    catch
                    {
                        continue;
                    }

                    if (text.Length > MaxIndexedFileChars)
                        text = text[..MaxIndexedFileChars];

                    files.Add(new BuildIndexFile(path.ToString(), text.ToLowerInvariant(), group));
                }
            }
        }

        return files;
    }

    /// <summary>read_url action: fetch a whitelisted page and return plain text.</summary>
    private async Task<string> ReadUrlForAdminAsync(AdminAction action)
    {
        if (!_ahelpWebEnabled)
            return "Чтение интернета выключено на сервере (neuroahelp.web_enabled).";

        var raw = (action.Url ?? string.Empty).Trim();
        if (raw.Length == 0)
            return "Укажи url - адрес страницы, которую нужно прочитать.";

        if (raw.Length > 500 || !Uri.TryCreate(raw, UriKind.Absolute, out var uri))
            return "Некорректный url.";

        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            return "Некорректный url: нужен http/https.";

        if (!IsWebHostAllowed(uri.Host) || IsPrivateWebHost(uri.Host))
            return $"Домен «{uri.Host}» не разрешен. Разрешены только домены вики и neuroahelp.web_domains.";

        try
        {
            var (finalUri, text) = await FetchWebTextAsync(uri);
            var header = $"Страница {finalUri} (текст без разметки):";
            if (text.Length + header.Length <= MaxWebResultChars)
                return header + "\n" + text;

            return header + "\n" + text[..(MaxWebResultChars - header.Length - 15)] + "…[обрезано]";
        }
        catch (Exception e)
        {
            _sawmill.Info($"Neuro ahelp: read_url failed for {uri}: {e.Message}");
            return $"Не удалось прочитать страницу: {e.Message}";
        }
    }

    /// <summary>Allowlist: neuroahelp.web_domains plus the host of the effective wiki link.</summary>
    private bool IsWebHostAllowed(string host)
    {
        var normalized = NormalizeWebHost(host);

        var wiki = EffectiveWikiUrl;
        if (Uri.TryCreate(wiki, UriKind.Absolute, out var wikiUri) &&
            string.Equals(NormalizeWebHost(wikiUri.Host), normalized, StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var raw in _ahelpWebDomains.Split(',', ';'))
        {
            var domain = NormalizeWebHost(raw);
            if (domain.Length == 0)
                continue;

            if (string.Equals(normalized, domain, StringComparison.Ordinal) ||
                normalized.EndsWith('.' + domain))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeWebHost(string host)
    {
        return host.Trim().TrimEnd('.').ToLowerInvariant();
    }

    /// <summary>Blocks IP literals and host names that point at loopback or private networks.</summary>
    private static bool IsPrivateWebHost(string host)
    {
        var normalized = NormalizeWebHost(host);
        if (normalized == "localhost" || normalized.EndsWith(".localhost"))
            return true;

        if (!IPAddress.TryParse(normalized, out var ip))
            return false;

        if (IPAddress.IPv6Loopback.Equals(ip))
            return true;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] is 0 or 10 or 127 or 169 or 172 or 192 ||
                   (b[0] == 100 && b[1] is >= 64 and <= 127);
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = ip.GetAddressBytes();
            return (b[0] & 0xFE) == 0xFC || // fc00::/7 unique local
                   b[0] == 0xFE && (b[1] & 0xC0) == 0x80; // fe80::/10 link local
        }

        return false;
    }

    private async Task<(Uri FinalUri, string Text)> FetchWebTextAsync(Uri uri)
    {
        var current = uri;
        for (var hop = 0; hop <= MaxRedirectHops; hop++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.UserAgent.ParseAdd("MiniStation-AhelpBot/1.0 (ahelp assistant)");
            request.Headers.TryAddWithoutValidation("Accept-Language", "ru,en;q=0.5");

            using var response = await WebHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            var code = (int) response.StatusCode;
            if (code is 301 or 302 or 303 or 307 or 308)
            {
                var location = response.Headers.Location;
                if (location == null)
                    throw new HttpRequestException("переход без Location.");

                var next = location.IsAbsoluteUri ? location : new Uri(current, location);
                if (next.Scheme != Uri.UriSchemeHttps && next.Scheme != Uri.UriSchemeHttp)
                    throw new HttpRequestException("переход по не-http схеме.");

                if (!IsWebHostAllowed(next.Host) || IsPrivateWebHost(next.Host))
                    throw new HttpRequestException($"редирект на запрещенный домен «{next.Host}».");

                current = next;
                continue;
            }

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"{code} {response.StatusCode}.");

            var media = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (media.StartsWith("image/") || media.StartsWith("video/") || media.StartsWith("audio/"))
                throw new HttpRequestException($"это не текст ({media}).");

            var content = await response.Content.ReadAsStringAsync();
            var text = media.Contains("html", StringComparison.OrdinalIgnoreCase) ? HtmlToText(content) : content;
            if (text.Length > MaxWebResultChars)
                text = text[..MaxWebResultChars];

            return (current, text);
        }

        throw new HttpRequestException($"больше {MaxRedirectHops} переходов.");
    }

    private HttpClient WebHttpClient =>
        _webClient ??= new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(15),
            MaxResponseContentBufferSize = 2 * 1024 * 1024,
        };

    /// <summary>Very small HTML to text conversion: drops scripts/styles, keeps line structure.</summary>
    private static string HtmlToText(string html)
    {
        var text = ScriptStyleRegex.Replace(html, " ");
        text = BlockTagRegex.Replace(text, "\n");
        text = TagRegex.Replace(text, " ");
        text = WebUtility.HtmlDecode(text);

        var lines = text.Split('\n')
            .Select(l => Regex.Replace(l, @"\s+", " ").Trim())
            .Where(l => l.Length > 0);

        return string.Join('\n', lines);
    }
}
