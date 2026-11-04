// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._Mini.TypanWar;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Mini.TypanWar;

/// <summary>
/// Отдельное окно ставок на исход войны. Открывается кнопкой на HUD в фазе подготовки.
/// </summary>
public sealed class TypanWarBetWindow : FancyWindow
{
    private readonly BoxContainer _choices;
    private readonly BoxContainer _chips;
    private readonly Label _placedLabel;

    private int[]? _amounts;

    public TypanWarBetWindow()
    {
        Title = Loc.GetString("typan-war-bet-window-title");
        MinWidth = 400;
        MinHeight = 120;

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
            SeparationOverride = 6,
        };
        ContentsContainer.AddChild(root);

        root.AddChild(new Label
        {
            Text = Loc.GetString("typan-war-bet-payout-note"),
            FontColorOverride = Color.FromHex("#F0D890"),
        });

        _choices = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
        };
        root.AddChild(_choices);

        _chips = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
        };
        _choices.AddChild(_chips);

        _placedLabel = new Label
        {
            Visible = false,
            Margin = new Thickness(2),
        };
        root.AddChild(_placedLabel);
    }

    /// <summary>
    /// Shows the choice buttons or, once a bet is placed, that bet.
    /// </summary>
    public void ApplyState(TypanWarSide? mySide, int myAmount, int[] amounts)
    {
        if (amounts.Length == 0)
            amounts = [5, 10, 25];

        var hasBet = mySide != null;
        _choices.Visible = !hasBet;
        _placedLabel.Visible = hasBet;

        if (hasBet)
        {
            var accent = FactionAccent(mySide!.Value);
            _placedLabel.Text = Loc.GetString("typan-war-bet-placed-label")
                + " "
                + Loc.GetString(SideNameKey(mySide.Value))
                + $" · {myAmount}";
            _placedLabel.FontColorOverride = accent;
            return;
        }

        if (_amounts == null || !amounts.SequenceEqual(_amounts))
        {
            _amounts = amounts;
            RebuildChips(amounts);
        }
    }

    private void RebuildChips(int[] amounts)
    {
        _chips.RemoveAllChildren();

        foreach (var side in new[] { TypanWarSide.Nanotrasen, TypanWarSide.Typan })
        {
            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 6,
            };
            row.AddChild(new Label
            {
                Text = Loc.GetString(SideNameKey(side)),
                FontColorOverride = FactionAccent(side),
                VerticalAlignment = VAlignment.Center,
                MinWidth = 76,
            });

            foreach (var amount in amounts)
                row.AddChild(CreateChip(side, amount));

            _chips.AddChild(row);
        }
    }

    private static Button CreateChip(TypanWarSide side, int amount)
    {
        var accent = FactionAccent(side);
        var button = new Button
        {
            Text = amount.ToString(),
            MinSize = new Vector2(46, 24),
            MaxSize = new Vector2(46, 24),
        };

        button.StyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = accent.WithAlpha(0.25f),
            BorderColor = accent,
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 6,
            ContentMarginRightOverride = 6,
        };

        button.OnPressed += _ =>
            IoCManager.Resolve<IEntityManager>().System<TypanWarUiSystem>().RequestBet(side, amount);
        return button;
    }

    private static string SideNameKey(TypanWarSide side) =>
        side == TypanWarSide.Nanotrasen ? "typan-war-bet-side-nt" : "typan-war-bet-side-typan";

    private static Color FactionAccent(TypanWarSide side) =>
        side == TypanWarSide.Nanotrasen ? Color.FromHex("#4A7FD4") : Color.FromHex("#C84848");
}
