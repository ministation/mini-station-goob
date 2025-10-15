using Robust.Shared.Configuration;
using Content.Shared.Atmos;
using Robust.Shared;

namespace Content.Shared.ADT.CCVar;

[CVarDefs]
public sealed class ADTCCVars
{
    /*
    * Radial menu
    */
    public static readonly CVarDef<bool> CenterRadialMenu =
        CVarDef.Create("radialmenu.center", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    #region Admin

    /// <summary>
    /// Включает или отключает уведомления администраторов о сообщениях,
    /// содержащих оскорбительные выражения в адрес родственников.
    /// </summary>
    public static readonly CVarDef<bool> ChatFilterAdminAlertEnable =
        CVarDef.Create("admin.chat_filter_admina_alert", false, CVar.SERVER | CVar.ARCHIVE);

    #endregion

    /// <summary>
    /// Включает или отключает отображение дополнительной лобби-панели в пользовательском интерфейсе.
    /// При значении true панель отображается, при false - скрывается.
    /// </summary>
    public static readonly CVarDef<bool> ExtraLobbyPanelEnabled =
        CVarDef.Create("ui.show_lobby_panel", true, CVar.REPLICATED | CVar.SERVER);


    /// <summary>
    /// Кол-во предыдущих карт, которые будут исключены из голосования.
    /// </summary>
    public static readonly CVarDef<int> MapVoteRecentBanDepth =
        CVarDef.Create("game.map_vote_recent_ban_depth", 1, CVar.SERVER | CVar.ARCHIVE);
}

