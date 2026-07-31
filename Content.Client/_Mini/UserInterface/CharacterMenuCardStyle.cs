// SPDX-FileCopyrightText: 2026 Casha
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT
using System.Numerics;
using Content.Client.UserInterface;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._Mini.UserInterface;

/// <summary>
/// Shared visual style for compact quest and objective cards in the character menu.
/// </summary>
public static class CharacterMenuCardStyle
{
    public const float CompactCardHeight = 108f;
    public const float TitleLineHeight = 18f;
    public const float BodyLineHeight = 16f;
    public const float IconSize = 20f;

    public static readonly Color AccentColor = Color.FromHex("#8B00FF");
    public static readonly Color ClaimReadyColor = Color.FromHex("#6b9e7a");
    public static readonly Color CardBackgroundColor = Color.FromHex("#1A1224");
    public static readonly Color TitleTextColor = Color.White;
    public static readonly Color BodyTextColor = Color.FromHex("#D4C4F0");
    public static readonly Color BriefingTextColor = Color.FromHex("#E9D5FF");
    public static readonly Color ObjectiveBorderColor = Color.FromHex("#5B3A7A");
    public static readonly Color ProgressTextColor = Color.FromHex("#E9D5FF");

    public static StyleBoxFlat CreateObjectivePanelStyle(bool complete) => new()
    {
        BackgroundColor = CardBackgroundColor.WithAlpha(0.55f),
        BorderColor = complete
            ? ClaimReadyColor.WithAlpha(0.4f)
            : ObjectiveBorderColor.WithAlpha(0.35f),
        BorderThickness = new Thickness(1),
        ContentMarginLeftOverride = 8,
        ContentMarginTopOverride = 6,
        ContentMarginRightOverride = 8,
        ContentMarginBottomOverride = 6,
    };

    public static StyleBoxFlat CreateCompactPanelStyle(bool highlighted) => new()
    {
        BackgroundColor = CardBackgroundColor.WithAlpha(0.55f),
        BorderColor = highlighted
            ? ClaimReadyColor.WithAlpha(0.4f)
            : AccentColor.WithAlpha(0.3f),
        BorderThickness = new Thickness(1),
        ContentMarginLeftOverride = 8,
        ContentMarginTopOverride = 6,
        ContentMarginRightOverride = 8,
        ContentMarginBottomOverride = 6,
    };

    public static MarqueeLabel CreateTitleMarquee(string text)
    {
        var marquee = new MarqueeLabel
        {
            Text = text,
            Modulate = TitleTextColor,
            HorizontalExpand = true,
            MinSize = new Vector2(0, TitleLineHeight),
            VerticalAlignment = Control.VAlignment.Center,
        };
        marquee.SetStyleClass("LabelSubText");
        return marquee;
    }

    public static PanelContainer WrapMarquee(MarqueeLabel marquee) => new()
    {
        HorizontalExpand = true,
        RectClipContent = true,
        MinSize = marquee.MinSize,
        Children = { marquee },
    };

    public static PanelContainer CreateTitleMarqueeHost(string text) =>
        WrapMarquee(CreateTitleMarquee(text));

    public static PanelContainer CreateBodyMarqueeHost(string text) => new()
    {
        HorizontalExpand = true,
        RectClipContent = true,
        MinSize = new Vector2(0, BodyLineHeight),
        Children =
        {
            new MarqueeLabel
            {
                Text = text,
                Modulate = BodyTextColor,
                HorizontalExpand = true,
                MinSize = new Vector2(0, BodyLineHeight),
            },
        },
    };

    public static PanelContainer CreateBriefingMarqueeHost(string text) => new()
    {
        HorizontalExpand = true,
        RectClipContent = true,
        MinSize = new Vector2(0, TitleLineHeight),
        Children =
        {
            new MarqueeLabel
            {
                Text = text,
                Modulate = BriefingTextColor,
                HorizontalExpand = true,
                MinSize = new Vector2(0, TitleLineHeight),
            },
        },
    };
}
