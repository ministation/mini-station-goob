using Robust.Shared.Maths;

namespace Content.Goobstation.UIKit.UserInterface.Controls;

/// <summary>
/// Mom can we have shitlets?
/// We have sheetlets at home
/// Shitlets at home:
/// </summary>
public static class ThunderdomeTheme
{
    public static readonly Color Accent = Color.FromHex("#e8a0a0");
    public static readonly Color AccentHover = Color.FromHex("#ffc8c8");
    public static readonly Color AccentDim = Color.FromHex("#a06060");

    // Dark opaque body — red accent only on borders/tabs, text stays light for readability.
    public static readonly Color BodyBg = Color.FromHex("#1a1214").WithAlpha(0.94f);
    public static readonly Color TitleBarBg = Color.FromHex("#2a181c").WithAlpha(0.96f);
    public static readonly Color CardBg = Color.FromHex("#24181c").WithAlpha(0.92f);
    public static readonly Color CardHoverBg = Color.FromHex("#3a2228").WithAlpha(0.95f);

    public static readonly Color Border = Color.FromHex("#c04040");

    public static readonly Color ButtonBg = Color.FromHex("#3a2024");
    public static readonly Color ButtonHoverBg = Color.FromHex("#5a3038");
    public static readonly Color ButtonDisabledBg = Color.FromHex("#1e1418");
    public static readonly Color ButtonDisabledFg = Color.FromHex("#776666");
    public static readonly Color ButtonText = Color.FromHex("#f0e8e8");

    public static readonly Color HeaderText = Color.FromHex("#f2d0d0");
    public static readonly Color SubText = Color.FromHex("#c8b0b0");
}
