using Robust.Shared.Configuration;

namespace Content.Shared._White;

[CVarDefs]
public sealed class WhiteCVars
{
     #region GhostRespawn
    public static readonly CVarDef<double> GhostRespawnTime =
        CVarDef.Create("ghost.respawn_time", 10d, CVar.SERVERONLY);

    public static readonly CVarDef<int> GhostRespawnMaxPlayers =
        CVarDef.Create("ghost.respawn_max_players", 20, CVar.SERVERONLY);

    #endregion
}
