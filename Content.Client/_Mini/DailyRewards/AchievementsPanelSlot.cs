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
