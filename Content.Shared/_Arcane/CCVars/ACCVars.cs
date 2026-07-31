using Robust.Shared.Configuration;

namespace Content.Shared._Arcane.CCVars;

[CVarDefs]
public sealed partial class ACCVars
{
    /// <summary>
    /// Включена ли автоматическая очистка мусора.
    /// </summary>
    public static readonly CVarDef<bool> AutoCleaningEnabled =
        CVarDef.Create("optimization.auto_cleaning", true, CVar.SERVERONLY | CVar.ARCHIVE);
    /// <summary>
    /// На каком расстоянии от игрока NPC будет замораживаться.
    /// </summary>
    public static readonly CVarDef<int> NpcSleepRange =
        CVarDef.Create("npc.sleep_range", 30, CVar.SERVERONLY);

    /// <summary>
    /// Максимум infinity dorms, которые один игрок может создать.
    /// </summary>
    public static readonly CVarDef<int> MaxUserInfinityDorms =
        CVarDef.Create("infinity_dorms.max_per_user", 2, CVar.SERVER | CVar.REPLICATED);
}
