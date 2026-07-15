public class SponsorColor
{
    /// <summary>
    /// Markup color hex for sponsor nicknames (6-digit RGB only — Alpha / black breaks AHelp markup).
    /// </summary>
    public static string GetColorForNickname(int DonateLvl)
    {
        return DonateLvl switch
        {
            1 => "#b4e07e",
            2 => "#bebaba",
            3 => "#e0ad47",
            4 => "#a86ed7",
            5 => "#e78459",
            _ => "#ffffff",
        };
    }
}
