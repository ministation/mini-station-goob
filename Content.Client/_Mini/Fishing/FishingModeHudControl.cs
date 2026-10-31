// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System;

using Content.Shared._Mini.Fishing;
using Content.Client.UserInterface;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Shared.Maths;

namespace Content.Client._Mini.Fishing;

/// <summary>
/// Fishing mode HUD: the three leaders with their fish score bars, in the same
/// pixel-art bar style as the vote timer and the war HUD. Sits at the top of the
/// screen below the top menu bar.
/// </summary>
public sealed class FishingModeHudControl : PanelContainer
{
    public const float PreferredWidth = 520f;

    private const int LeaderCount = 3;

    private static readonly Color PanelBackground = Color.FromHex("#14141C").WithAlpha(0.72f);
    private static readonly Color BorderColor = Color.FromHex("#6E6A82").WithAlpha(0.45f);
    private static readonly Color TextColor = Color.FromHex("#E8E6F0");
    private static readonly Color MutedTextColor = Color.FromHex("#9A97A8");

    private static readonly Color[] RankColors =
    {
        Color.FromHex("#F0D890"),
        Color.FromHex("#C9D4E0"),
        Color.FromHex("#D08A4B"),
    };

    private readonly Label _emptyLabel;
    private readonly LeaderRow[] _rows = new LeaderRow[LeaderCount];

    private sealed class LeaderRow
    {
        public BoxContainer Container = null!;
        public Label Name = null!;
        public PixelTiledProgressBar Bar = null!;
        public Label Count = null!;
    }

    public FishingModeHudControl()
    {
        MinHeight = 42;
        MaxHeight = 128;
        MouseFilter = MouseFilterMode.Ignore;
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = PanelBackground,
            BorderThickness = new Thickness(1),
            BorderColor = BorderColor,
            ContentMarginLeftOverride = 10,
            ContentMarginRightOverride = 10,
            ContentMarginTopOverride = 5,
            ContentMarginBottomOverride = 5,
        };

        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 3,
        };
        AddChild(column);

        column.AddChild(new Label
        {
            Text = Loc.GetString("fishing-mode-hud-title"),
            FontColorOverride = TextColor,
        });

        _emptyLabel = new Label
        {
            Text = Loc.GetString("fishing-mode-hud-no-fishers"),
            FontColorOverride = MutedTextColor,
            Visible = false,
        };
        column.AddChild(_emptyLabel);

        for (var i = 0; i < LeaderCount; i++)
            _rows[i] = BuildRow(column, i);

        Visible = false;
    }

    private static LeaderRow BuildRow(BoxContainer column, int index)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
        };
        column.AddChild(row);

        var rank = new Label
        {
            Text = Loc.GetString("fishing-mode-hud-rank", ("rank", index + 1)),
            FontColorOverride = RankColors[index],
            MinWidth = 24,
            MaxWidth = 24,
        };
        row.AddChild(rank);

        var name = new Label
        {
            FontColorOverride = TextColor,
            MinWidth = 150,
            MaxWidth = 150,
            ClipText = true,
            VerticalAlignment = VAlignment.Center,
        };
        row.AddChild(name);

        // The same tiled pixel bar the votes and daily quests use.
        var bar = new PixelTiledProgressBar(
            MiniSliderStyles.LongWhiteTrackPath,
            scale: 2f,
            fillModulate: RankColors[index].WithAlpha(0.9f))
        {
            Value = 0f,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true,
        };
        row.AddChild(bar);

        var count = new Label
        {
            FontColorOverride = RankColors[index],
            MinWidth = 64,
            MaxWidth = 64,
            Align = Label.AlignMode.Right,
        };
        row.AddChild(count);

        return new LeaderRow
        {
            Container = row,
            Name = name,
            Bar = bar,
            Count = count,
        };
    }

    public void Update(bool active, FishingModeLeader[] leaders)
    {
        Visible = active;
        if (!active)
            return;

        _emptyLabel.Visible = leaders.Length == 0;

        // The leader's bar is always full, the others are relative to the leader.
        var leaderScore = leaders.Length > 0 ? Math.Max(1, leaders[0].Score) : 1;

        for (var i = 0; i < LeaderCount; i++)
        {
            var row = _rows[i];
            if (i >= leaders.Length)
            {
                row.Container.Visible = false;
                continue;
            }

            var leader = leaders[i];
            row.Container.Visible = true;
            row.Name.Text = leader.Name;
            row.Count.Text = Loc.GetString("fishing-mode-hud-fish", ("count", leader.Fish));
            row.Bar.Value = Math.Clamp(leader.Score / (float) leaderScore, 0f, 1f);
        }
    }
}
