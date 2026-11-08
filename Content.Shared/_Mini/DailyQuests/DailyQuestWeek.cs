// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System;

namespace Content.Shared._Mini.DailyQuests;

/// <summary>
/// Helpers for the weekly quest period key (Monday 00:00 MSK calendar date).
/// </summary>
public static class DailyQuestWeek
{
  // Moscow has used a fixed UTC+3 offset year-round since 2011.
  private static readonly TimeSpan MoscowOffset = TimeSpan.FromHours(3);

  /// <summary>
  /// Start of the current quest week (Monday, MSK).
  /// </summary>
  public static DateTime GetCurrentWeekStart()
  {
    var moscowDate = ToMoscowDate(DateTime.UtcNow);
    var daysFromMonday = ((int)moscowDate.DayOfWeek + 6) % 7;
    return moscowDate.AddDays(-daysFromMonday);
  }

  /// <summary>
  /// Canonical storage key for <c>daily_quest_progress.quest_date</c>.
  /// Uses UTC midnight of the MSK calendar Monday so timestamptz round-trips reliably.
  /// </summary>
  public static DateTime ToStoredKey(DateTime weekStart)
    => DateTime.SpecifyKind(weekStart.Date, DateTimeKind.Utc);

  public static bool Matches(DateTime stored, DateTime currentWeekStart)
    => ToStoredKey(NormalizeToWeekStart(stored)) == ToStoredKey(currentWeekStart);

  public static DateTime GetNextResetUtc(DateTime weekStart)
  {
    var nextWeekStartMoscow = weekStart.Date.AddDays(7);
    return DateTime.SpecifyKind(nextWeekStartMoscow - MoscowOffset, DateTimeKind.Utc);
  }

  private static DateTime NormalizeToWeekStart(DateTime value)
  {
    var moscowDate = value.Kind == DateTimeKind.Utc
      ? ToMoscowDate(value)
      : value.Date;

    var daysFromMonday = ((int)moscowDate.DayOfWeek + 6) % 7;
    return moscowDate.AddDays(-daysFromMonday);
  }

  private static DateTime ToMoscowDate(DateTime utc)
    => utc.ToUniversalTime().Add(MoscowOffset).Date;
}
