// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Server._CorvaxGoob.StationGoal;
using System.Linq;
using System.Text.RegularExpressions;

namespace Content.Server._Mini.Typan.StationGoal;

/// <summary>
/// Builds compact objective card title/description from station goal locale keys.
/// </summary>
public static partial class TypanStationGoalCardText
{
    [GeneratedRegex(@"Спецзадача\s*«([^»]+)»|Special\s+task\s*«([^»]+)»", RegexOptions.IgnoreCase)]
    private static partial Regex TaskTitleRegex();

    public static (string Title, string Description) Build(StationGoalPrototype goal, string stationName)
    {
        var baseKey = goal.Text;

        if (Loc.TryGetString($"{baseKey}-card-title", out var cardTitle)
            && Loc.TryGetString($"{baseKey}-card-desc", out var cardDesc))
        {
            return (cardTitle, cardDesc);
        }

        var fullText = Loc.GetString(baseKey, ("station", stationName));
        var plain = StripMarkup(fullText);

        var title = TryExtractTaskTitle(plain)
                    ?? Loc.GetString("typan-station-goal-objective-default-title");

        var description = BuildFallbackDescription(plain);
        return (title, description);
    }

    private static string? TryExtractTaskTitle(string plain)
    {
        var match = TaskTitleRegex().Match(plain);
        if (!match.Success)
            return null;

        var name = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        return Loc.GetString("typan-station-goal-objective-task-title", ("name", name));
    }

    private static string BuildFallbackDescription(string plain)
    {
        var start = plain.IndexOf("СПЕЦИАЛЬНАЯ ЗАДАЧА", StringComparison.Ordinal);
        if (start < 0)
            start = plain.IndexOf("SPECIAL TASK", StringComparison.OrdinalIgnoreCase);

        var end = plain.IndexOf("Слава Синдикату", StringComparison.Ordinal);
        if (end < 0)
            end = plain.IndexOf("Glory to the Syndicate", StringComparison.OrdinalIgnoreCase);

        if (start >= 0 && end > start)
        {
            var body = plain[start..end].Trim();
            var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var filtered = lines
                .Where(line => !line.Contains("████", StringComparison.Ordinal)
                               && !line.StartsWith("===", StringComparison.Ordinal)
                               && !line.StartsWith("---", StringComparison.Ordinal)
                               && line != "СПЕЦИАЛЬНАЯ ЗАДАЧА"
                               && !line.StartsWith("Солдаты ", StringComparison.Ordinal)
                               && !line.StartsWith("Soldiers ", StringComparison.OrdinalIgnoreCase)
                               && !line.StartsWith("Устав ", StringComparison.Ordinal)
                               && !line.StartsWith("Charter ", StringComparison.OrdinalIgnoreCase)
                               && !line.StartsWith("1.", StringComparison.Ordinal)
                               && !line.StartsWith("2.", StringComparison.Ordinal)
                               && !line.StartsWith("3.", StringComparison.Ordinal)
                               && !line.StartsWith("4.", StringComparison.Ordinal)
                               && !line.StartsWith("5.", StringComparison.Ordinal))
                .Take(8);

            var compact = string.Join('\n', filtered);
            if (!string.IsNullOrWhiteSpace(compact))
                return compact;
        }

        return Loc.GetString("typan-station-goal-objective-default-desc");
    }

    private static string StripMarkup(string text)
    {
        text = Regex.Replace(text, @"\[color=[^\]]+\]|\[/color\]", string.Empty, RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\[bold\]|\[/bold\]", string.Empty, RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\[italic\]|\[/italic\]", string.Empty, RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\[head=\d+\]|\[/head\]", string.Empty, RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\{""\[color=[^\""]+\] █[^""]+""\}", string.Empty);
        text = Regex.Replace(text, @"\{""\[head=\d+\][^""]+""\}", string.Empty);
        text = Regex.Replace(text, @"\{""\[bold\][^""]+""\}", string.Empty);
        return text.Trim();
    }
}
