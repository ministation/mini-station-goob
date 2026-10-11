// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Collections.Concurrent;
using Robust.Shared.Network;

namespace Content.Shared._Mini.CoinShop;

/// <summary>
/// Server-side cache of coin-purchased OOC nickname colors (sponsor colors always take priority).
/// Expiry is stored as unix-epoch day numbers; stale entries are simply ignored on read.
/// </summary>
public static class CoinOocColorCache
{
    private static readonly ConcurrentDictionary<NetUserId, (string Hex, int ExpireDay)> Colors = new();

    public static int Today => (int) (DateTime.UtcNow - DateTime.UnixEpoch).TotalDays;

    public static bool TryGet(NetUserId userId, out string hex)
    {
        hex = string.Empty;

        if (!Colors.TryGetValue(userId, out var entry) || entry.ExpireDay <= Today)
            return false;

        hex = entry.Hex;
        return true;
    }

    public static bool TryGetExpire(NetUserId userId, out int expireDay)
    {
        if (Colors.TryGetValue(userId, out var entry) && entry.ExpireDay > Today)
        {
            expireDay = entry.ExpireDay;
            return true;
        }

        expireDay = 0;
        return false;
    }

    public static void Set(NetUserId userId, string hex, int expireDay) =>
        Colors[userId] = (hex, expireDay);

    public static void Remove(NetUserId userId) =>
        Colors.TryRemove(userId, out _);
}
