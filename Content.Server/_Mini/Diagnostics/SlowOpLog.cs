// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Diagnostics;
using Robust.Shared.Log;

namespace Content.Server._Mini.Diagnostics;

/// <summary>
/// Times known-heavy blocking operations (map/grid loads) and warns above a threshold, so
/// freezes name themselves in the log instead of appearing as bare "Cannot keep up!" gaps.
/// </summary>
public static class SlowOpLog
{
    public static readonly TimeSpan WarnThreshold = TimeSpan.FromMilliseconds(500);

    public static void Run(string name, Action action)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            action();
        }
        finally
        {
            sw.Stop();
            if (sw.Elapsed > WarnThreshold)
                Logger.GetSawmill("mini.slowop").Warning($"{name} blocked the main thread for {sw.Elapsed.TotalMilliseconds:F0} ms");
        }
    }

    public static void Run(ISawmill sawmill, string name, Action action)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            action();
        }
        finally
        {
            sw.Stop();
            if (sw.Elapsed > WarnThreshold)
                sawmill.Warning($"{name} blocked the main thread for {sw.Elapsed.TotalMilliseconds:F0} ms");
        }
    }
}