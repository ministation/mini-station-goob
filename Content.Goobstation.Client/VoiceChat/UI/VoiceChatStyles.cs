// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Goobstation.Client.VoiceChat.UI;

/// <summary>
/// Mini Station look for the voice chat UI: dark flat cards, muted purple accent and the same
/// palette the rest of the Mini windows (token shop, rewards menu, character menu) use.
/// </summary>
public static class VoiceChatStyles
{
    public static readonly Color WindowBackground = Color.FromHex("#0e0c14");
    public static readonly Color PanelBackground = Color.FromHex("#1a1622").WithAlpha(0.9f);
    public static readonly Color CardBackground = Color.FromHex("#1e1a26").WithAlpha(0.85f);
    public static readonly Color CardBorder = Color.FromHex("#2d3748").WithAlpha(0.6f);
    public static readonly Color Accent = Color.FromHex("#9a8fb5");
    public static readonly Color Text = Color.FromHex("#d7e2f4");
    public static readonly Color MutedText = Color.FromHex("#9fb4d8");
    public static readonly Color Success = Color.FromHex("#6b9e7a");
    public static readonly Color Warning = Color.FromHex("#f0d878");
    public static readonly Color Danger = Color.FromHex("#f0a0a0");
    public static readonly Color Offline = Color.FromHex("#6f7480");
    public static readonly Color Idle = Color.FromHex("#c5d3ed");
    public static readonly Color Live = Color.FromHex("#6b9e7a");

    public static StyleBoxFlat Window() => new()
    {
        BackgroundColor = WindowBackground,
    };

    public static StyleBoxFlat Panel() => new()
    {
        BackgroundColor = PanelBackground,
        BorderColor = CardBorder,
        BorderThickness = new Thickness(1),
        ContentMarginLeftOverride = 14,
        ContentMarginTopOverride = 12,
        ContentMarginRightOverride = 14,
        ContentMarginBottomOverride = 12,
    };

    public static StyleBoxFlat Card() => new()
    {
        BackgroundColor = CardBackground,
        BorderColor = CardBorder,
        BorderThickness = new Thickness(1),
        ContentMarginLeftOverride = 12,
        ContentMarginTopOverride = 8,
        ContentMarginRightOverride = 12,
        ContentMarginBottomOverride = 8,
    };

    public static StyleBoxFlat CodeButtonStyle() => new()
    {
        BackgroundColor = Color.FromHex("#211c2b"),
        BorderColor = Accent.WithAlpha(0.45f),
        BorderThickness = new Thickness(1),
        ContentMarginLeftOverride = 8,
        ContentMarginTopOverride = 4,
        ContentMarginRightOverride = 8,
        ContentMarginBottomOverride = 4,
    };

    public static StyleBoxFlat ButtonStyle(bool accent = false) => new()
    {
        BackgroundColor = accent ? Color.FromHex("#2a2338") : Color.FromHex("#15121d"),
        BorderColor = accent ? Accent.WithAlpha(0.9f) : CardBorder,
        BorderThickness = new Thickness(1),
        ContentMarginLeftOverride = 12,
        ContentMarginTopOverride = 4,
        ContentMarginRightOverride = 12,
        ContentMarginBottomOverride = 4,
    };

    public static Button Button(string text, bool accent = false) => new()
    {
        Text = text,
        StyleBoxOverride = ButtonStyle(accent),
    };

    public static Label Heading(string text) => new()
    {
        Text = text,
        StyleClasses = { "LabelHeading" },
        FontColorOverride = Accent,
    };

    public static Label Body(string text) => new()
    {
        Text = text,
        FontColorOverride = Text,
    };

    public static Label Muted(string text) => new()
    {
        Text = text,
        StyleClasses = { "LabelSubText" },
        FontColorOverride = MutedText,
    };

    public static RichTextLabel WrappedMuted(string text)
    {
        var label = new RichTextLabel
        {
            HorizontalExpand = true,
            VerticalAlignment = Control.VAlignment.Top,
        };
        label.SetMessage(text, MutedText);
        return label;
    }

    public static PanelContainer CardHost(Control child, bool expand = true)
    {
        var panel = new PanelContainer
        {
            PanelOverride = Card(),
            HorizontalExpand = expand,
        };
        panel.AddChild(child);
        return panel;
    }

    public static PanelContainer Divider() => new()
    {
        HorizontalExpand = true,
        MinSize = new Vector2(0, 1),
        Margin = new Thickness(0, 6, 0, 6),
        PanelOverride = new StyleBoxFlat { BackgroundColor = CardBorder },
    };
}
