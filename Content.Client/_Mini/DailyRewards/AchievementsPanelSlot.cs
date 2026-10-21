// SPDX-FileCopyrightText: 2026 Casha
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.UserInterface;

namespace Content.Client._Mini.DailyRewards;

/// <summary>
/// Shared slot for the achievements panel of the rewards menu. Content.Client cannot reference the
/// Goobstation content module, so the module fills this slot from its client system (which is always
/// initialised before the rewards menu can be opened). Null means "no achievements UI in this build".
/// </summary>
public static class AchievementsPanelSlot
{
    public static Func<Control?>? Factory { get; set; }
}
