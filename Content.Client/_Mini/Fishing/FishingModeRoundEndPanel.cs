// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using System.Numerics;

using Content.Client._Mini.Fishing;
using Content.Client.Administration.UI.CustomControls;
using Content.Client.Stylesheets;
using Content.Shared._Mini.Fishing;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client.RoundEnd;

public sealed partial class RoundEndSummaryWindow
{
    private const int FishingManifestPlaces = 3;

    private static readonly Color[] FishingPlaceColors =
    {
        Color.FromHex("#F0D890"),
        Color.FromHex("#C9D1DA"),
        Color.FromHex("#D08A4E"),
    };

    /// <summary>
    /// Fishing mode leaderboard, rendered as the first rows of the player manifest, where antagonists go.
    /// </summary>
    private IEnumerable<Control> MakeFishingTopRows()
    {
        var leaders = _entityManager.System<FishingModeUiSystem>().RoundEndLeaders;
        if (leaders.Length == 0)
            yield break;

        yield return new Label
        {
            Text = Loc.GetString("fishing-mode-round-end-title"),
            StyleClasses = { StyleNano.StyleClassLabelSubText },
            Margin = new Thickness(2, 0, 0, 4),
        };

        var shown = Math.Min(FishingManifestPlaces, leaders.Length);
        for (var i = 0; i < shown; i++)
            yield return MakeFishingRow(leaders[i], i);

        yield return new HSeparator { Margin = new Thickness(0, 2, 0, 8) };
    }

    private static Control MakeFishingRow(FishingModeLeader leader, int place)
    {
        var panel = new PanelContainer
        {
            StyleClasses = { StyleNano.StyleClassBackgroundBaseDark },
            Margin = new Thickness(0, 0, 0, 4),
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(8, 3, 8, 3),
        };
        panel.AddChild(row);

        row.AddChild(new Label
        {
            Text = $"{place + 1}.",
            StyleClasses = { StyleNano.StyleClassLabelHeading },
            FontColorOverride = FishingPlaceColors[place],
            MinSize = new Vector2(26, 0),
            VerticalAlignment = Control.VAlignment.Center,
        });

        row.AddChild(new Label
        {
            Text = leader.Name,
            StyleClasses = { StyleNano.StyleClassLabelHeading },
            HorizontalExpand = true,
            ClipText = true,
            VerticalAlignment = Control.VAlignment.Center,
        });

        row.AddChild(new Label
        {
            Text = Loc.GetString("fishing-mode-hud-fish", ("count", leader.Fish)),
            StyleClasses = { StyleNano.StyleClassLabelSubText },
            VerticalAlignment = Control.VAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0),
        });

        if (leader.Reward > 0)
        {
            row.AddChild(new Label
            {
                Text = Loc.GetString("fishing-mode-round-end-reward", ("amount", leader.Reward)),
                StyleClasses = { StyleNano.StyleClassLabelSubText },
                FontColorOverride = FishingPlaceColors[0],
                VerticalAlignment = Control.VAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
            });
        }

        return panel;
    }
}
