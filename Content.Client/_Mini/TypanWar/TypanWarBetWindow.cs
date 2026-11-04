// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._Mini.TypanWar;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Content.Client._Mini.TypanWar;

/// <summary>
/// Window for betting on the war outcome: pick a faction, then an amount.
/// </summary>
public sealed class TypanWarBetWindow : FancyWindow
{
    private readonly BoxContainer _root;
    private string? _builtKey;

    public TypanWarBetWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("typan-war-bet-window-title");
        MinWidth = 380;

        _root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(12, 4, 12, 6),
        };
        ContentsContainer.AddChild(_root);
    }

    public void OpenPrepared()
    {
        if (!IsOpen)
            OpenCentered();
    }

    public void Refresh(bool bettingOpen, TypanWarSide? mySide, int myAmount, int[] amounts)
    {
        var key = $"{bettingOpen}:{mySide}:{myAmount}:{string.Join(",", amounts)}";
        if (_builtKey == key)
            return;

        _builtKey = key;
        _root.RemoveAllChildren();

        if (!bettingOpen && mySide == null)
        {
            _root.AddChild(new Label
            {
                Text = Loc.GetString("typan-war-bet-closed"),
                FontColorOverride = Color.FromHex("#adbac7"),
            });
            return;
        }

        if (mySide != null)
        {
            var placedRow = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 6,
            };
            placedRow.AddChild(new Label
            {
                Text = Loc.GetString("typan-war-bet-placed-label"),
                FontColorOverride = Color.FromHex("#adbac7"),
                VerticalAlignment = VAlignment.Center,
            });
            placedRow.AddChild(CreateActiveBetChip(mySide.Value, myAmount));
            placedRow.AddChild(new Label
            {
                Text = Loc.GetString("typan-war-bet-payout-note"),
                FontColorOverride = Color.FromHex("#F0D890"),
                VerticalAlignment = VAlignment.Center,
            });
            _root.AddChild(placedRow);
            return;
        }

        _root.AddChild(new Label
        {
            Text = Loc.GetString("typan-war-bet-payout-note"),
            FontColorOverride = Color.FromHex("#adbac7"),
        });

        AddBetGroup(TypanWarSide.Nanotrasen, amounts);
        AddBetGroup(TypanWarSide.Typan, amounts);
    }

    private void AddBetGroup(TypanWarSide side, int[] amounts)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
        };
        row.AddChild(new Label
        {
            Text = Loc.GetString(side == TypanWarSide.Nanotrasen ? "typan-war-bet-side-nt" : "typan-war-bet-side-typan"),
            FontColorOverride = FactionAccent(side),
            VerticalAlignment = VAlignment.Center,
            MinWidth = 60,
        });

        foreach (var amount in amounts)
            row.AddChild(CreateBetChip(side, amount));

        _root.AddChild(row);
    }

    private Button CreateBetChip(TypanWarSide side, int amount)
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

    private Control CreateActiveBetChip(TypanWarSide side, int amount)
    {
        var accent = FactionAccent(side);
        var chip = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = accent.WithAlpha(0.45f),
                BorderColor = accent,
                BorderThickness = new Thickness(1),
            }
        };

        chip.AddChild(new Label
        {
            Text = Loc.GetString(side == TypanWarSide.Nanotrasen ? "typan-war-bet-side-nt" : "typan-war-bet-side-typan")
                + $" · {amount}",
            Margin = new Thickness(8, 2),
        });

        return chip;
    }

    private static Color FactionAccent(TypanWarSide side) =>
        side == TypanWarSide.Nanotrasen ? Color.FromHex("#4A7FD4") : Color.FromHex("#C84848");
}
