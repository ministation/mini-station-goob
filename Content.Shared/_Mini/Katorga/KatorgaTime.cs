// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Mini.Katorga;

/// <summary>
///     Единый формат срока каторги: серверные сообщения и клиентские панели показывают одно и то же.
/// </summary>
public static class KatorgaTime
{
    public static string Format(TimeSpan time)
    {
        time = time.Duration();
        if (time.TotalDays >= 1)
            return string.Format("{0} д {1} ч", (int) time.TotalDays, time.Hours);
        if (time.TotalHours >= 1)
            return string.Format("{0} ч {1} мин", (int) time.TotalHours, time.Minutes);
        if (time.TotalMinutes >= 1)
            return string.Format("{0} мин {1} с", (int) time.TotalMinutes, time.Seconds);
        return string.Format("{0} с", (int) time.TotalSeconds);
    }
}
