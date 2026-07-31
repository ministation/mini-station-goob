using Content.Shared.Administration;
using Content.Shared.CCVar.CVarAccess;
using Robust.Shared.Configuration;

namespace Content.Goobstation.Shared.MisandryBox.Thunderdome;

[CVarDefs]
public sealed class ThunderdomeCVars
{
    public static readonly CVarDef<bool> ThunderdomeEnabled =
        CVarDef.Create("thunderdome.enabled", false, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<bool> ThunderdomeRefill =
        CVarDef.Create("thunderdome.refill", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Comma-separated list of allowed species in Thunderdome.
    /// </summary>
    public static readonly CVarDef<string> AllowedSpecies =
        CVarDef.Create("thunderdome.allowed_species",
            "Human,Reptilian,Dwarf,Moth,Diona,Arachnid,Slime,Felinid,Oni,Harpy,Vulpkanin,Tajaran",
            CVar.SERVER | CVar.REPLICATED);

    // CorvaxGoob-Thunderdome-start
    [CVarControl(AdminFlags.Admin, min: 10, max: 180)]
    public static readonly CVarDef<int> ActivationDelay =
        CVarDef.Create("thunderdome.activation_delay", Random.Shared.Next(50, 91), CVar.SERVER | CVar.REPLICATED | CVar.NOTIFY);

    public static readonly CVarDef<bool> ActivationDelayEnabled =
        CVarDef.Create("thunderdome.activation_delay_enabled", false, CVar.SERVER | CVar.REPLICATED);
    // CorvaxGoob-Thunderdome-end

    /// <summary>
    /// Number of days to retain global Thunderdome stats before cleanup.
    /// Stats older than this will be removed during periodic cleanup.
    /// </summary>
    public static readonly CVarDef<int> GlobalStatsRetentionDays =
        CVarDef.Create("thunderdome.global_stats_retention_days", 30, CVar.SERVER);

    /// <summary>
    /// Time window (in seconds) after receiving player damage during which suicide/ghost credits the attacker.
    /// If player ghosts/suicides within this window, the attacker gets credit for the kill.
    /// </summary>
    public static readonly CVarDef<float> SuicidePenaltyWindow =
        CVarDef.Create("thunderdome.suicide_penalty_window", 10f, CVar.SERVER);
}
