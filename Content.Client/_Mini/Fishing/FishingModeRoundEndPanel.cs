// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System;

using Content.Client._Mini.Fishing;
using Content.Client.Stylesheets;
using Content.Shared._Mini.DailyQuests;
using Content.Shared._Mini.Fishing;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client.RoundEnd;

public sealed partial class RoundEndSummaryWindow
{
    private const int FishingManifestTop = 10;
    private const int FishingManifestBestShown = 3;

    private static readonly Color FishingRankColor = Color.FromHex("#F0D890");

    /// <summary>
    /// Fishing mode round leaderboard, shown on top of the player manifest.
    /// </summary>
    private Control? TryMakeFishingTop()
    {
        var leaders = _entityManager.System<FishingModeUiSystem>().RoundEndLeaders;
        if (leaders.Length == 0)
            return null;

        var panel = new PanelContainer
        {
            StyleClasses = { StyleNano.StyleClassBackgroundBaseDark },
            Margin = new Thickness(0, 0, 0, 8),
        };

        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8, 6, 8, 6),
        };
        panel.AddChild(column);

        column.AddChild(new Label
        {
            Text = Loc.GetString("fishing-mode-round-end-title"),
            StyleClasses = { StyleNano.StyleClassLabelHeading },
            Margin = new Thickness(0, 0, 0, 4),
        });

        var shown = Math.Min(FishingManifestTop, leaders.Length);
        for (var i = 0; i < shown; i++)
        {
            var leader = leaders[i];
            column.AddChild(new Label
            {
                Text = Loc.GetString("fishing-mode-round-end-line",
                    ("rank", i + 1),
                    ("name", leader.Name),
                    ("fish", leader.Fish),
                    ("score", leader.Score)),
                FontColorOverride = i < FishingManifestBestShown ? FishingRankColor : Color.White,
            });

            if (i >= FishingManifestBestShown || leader.BestFish.Length == 0)
                continue;

            column.AddChild(new Label
            {
                Text = Loc.GetString("fishing-mode-round-end-best",
                    ("rarity", Loc.GetString(leader.BestFishRarity.GetLocId())),
                    ("fish", leader.BestFish)),
                FontColorOverride = leader.BestFishRarity.GetColor(),
                Margin = new Thickness(16, -2, 0, 2),
            });
        }

        return panel;
    }
}
