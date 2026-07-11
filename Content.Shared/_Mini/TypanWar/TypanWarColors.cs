using Robust.Shared.Utility;

namespace Content.Shared._Mini.TypanWar;

/// <summary>
/// Faction palette for Typan War UI and announcements.
/// </summary>
public static class TypanWarColors
{
    public static readonly Color Nanotrasen = Color.FromHex("#5A9FFF");
    public static readonly Color Typan = Color.FromHex("#FF6666");
    public static readonly Color Neutral = Color.FromHex("#C8C4D8");

    public static Color ForCaptureOwner(TypanWarCaptureOwner owner) => owner switch
    {
        TypanWarCaptureOwner.Nanotrasen => Nanotrasen,
        TypanWarCaptureOwner.Typan => Typan,
        _ => Neutral,
    };

    public static Color ForWinner(TypanWarWinner winner) => winner switch
    {
        TypanWarWinner.Nanotrasen => Nanotrasen,
        TypanWarWinner.Typan => Typan,
        _ => Neutral,
    };

    public static string SenderLocId(TypanWarCaptureOwner owner) => owner switch
    {
        TypanWarCaptureOwner.Nanotrasen => "typan-war-sender-nt",
        TypanWarCaptureOwner.Typan => "typan-war-sender-typan",
        _ => "typan-war-sender",
    };
}
