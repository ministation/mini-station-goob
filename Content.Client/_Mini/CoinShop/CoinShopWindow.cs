// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Numerics;
using Content.Client.Resources;
using Content.Shared._Mini.CoinShop;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;

namespace Content.Client._Mini.CoinShop;

public sealed class CoinShopWindow : DefaultWindow
{
    private static readonly Color WindowBackgroundColor = Color.FromHex("#0e0c14");
    private static readonly Color CardBackgroundColor = Color.FromHex("#1e1a26").WithAlpha(0.8f);
    private static readonly Color OwnedBorderColor = Color.FromHex("#3fb950").WithAlpha(0.8f);
    private static readonly Color ActiveBorderColor = Color.FromHex("#e0ad47");

    private const string CoinIconPath = "/Textures/_Mini/Interface/Coin.png";

    private static readonly Color[] RarityColors =
    [
        Color.FromHex("#adbac7"),
        Color.FromHex("#3fb950"),
        Color.FromHex("#a86ed7"),
        Color.FromHex("#e0ad47"),
    ];

    private readonly CoinShopSystem _system;
    private readonly Texture _coinTexture;

    private Label _balanceValueLabel = null!;
    private GridContainer _cosmeticGrid = null!;
    private BoxContainer _colorRow = null!;
    private GridContainer _ghostGrid = null!;
    private Label _rollResultLabel = null!;

    public CoinShopWindow(CoinShopSystem system)
    {
        _system = system;
        _coinTexture = IoCManager.Resolve<IResourceCache>().GetTexture(CoinIconPath);

        Title = Loc.GetString("coin-shop-title");
        MinSize = new Vector2(900, 600);
        SetSize = new Vector2(900, 600);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 12,
            Margin = new Thickness(20, 12)
        };

        var backdrop = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = WindowBackgroundColor }
        };
        Contents.AddChild(backdrop);
        backdrop.AddChild(root);

        root.AddChild(BuildHero());
        root.AddChild(BuildSectionHeader(Loc.GetString("coin-shop-cosmetics-header")));
        root.AddChild(BuildCosmeticGrid());
        root.AddChild(BuildSectionHeader(Loc.GetString("coin-shop-colors-header")));
        root.AddChild(BuildColorRow());
        root.AddChild(BuildSectionHeader(Loc.GetString("coin-shop-ghosts-header")));
        root.AddChild(BuildGhostGrid());
        root.AddChild(BuildLootboxPanel());
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        var mode = base.GetDragModeFor(relativeMousePos);
        if (mode == DragMode.Move)
            return DragMode.Move;
        return mode & ~(DragMode.Left | DragMode.Right);
    }

    public void UpdateState(CoinShopStateEvent state)
    {
        _balanceValueLabel.Text = state.Balance.ToString();

        _cosmeticGrid.RemoveAllChildren();
        foreach (var entry in state.Cosmetics)
            _cosmeticGrid.AddChild(CreateCosmeticCard(entry, state.SelectedCosmetic));

        _colorRow.RemoveAllChildren();
        foreach (var entry in state.Colors)
            _colorRow.AddChild(CreateColorButton(entry));

        _ghostGrid.RemoveAllChildren();
        foreach (var entry in state.Ghosts)
            _ghostGrid.AddChild(CreateGhostCard(entry));
    }

    public void ShowRollResult(CoinShopRollResultEvent ev)
    {
        var key = ev.Duplicate
            ? "coin-shop-roll-duplicate"
            : "coin-shop-roll-new";

        _rollResultLabel.Text = Loc.GetString(key,
            ("item", ev.ItemName),
            ("refund", ev.Refund),
            ("rarity", Loc.GetString($"coin-shop-rarity-{ev.Rarity.ToString().ToLower()}")));
        _rollResultLabel.Modulate = RarityColors[(int) ev.Rarity];
    }

    private Control BuildHero()
    {
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#1a1622").WithAlpha(0.9f) }
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            Margin = new Thickness(12, 8)
        };

        row.AddChild(new TextureRect
        {
            Texture = _coinTexture,
            MinSize = new Vector2(32, 32),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            VerticalAlignment = VAlignment.Center
        });

        _balanceValueLabel = new Label
        {
            Text = "0",
            StyleClasses = { "LabelBig" },
            VerticalAlignment = VAlignment.Center
        };
        row.AddChild(_balanceValueLabel);

        row.AddChild(new Control { HorizontalExpand = true });

        row.AddChild(new Label
        {
            Text = Loc.GetString("coin-shop-subtitle"),
            Modulate = Color.FromHex("#adbac7"),
            VerticalAlignment = VAlignment.Center
        });

        panel.AddChild(row);
        return panel;
    }

    private Control BuildSectionHeader(string text)
    {
        return new Label
        {
            Text = text,
            StyleClasses = { "LabelHeading" },
            Margin = new Thickness(4, 8, 0, 0)
        };
    }

    private Control BuildCosmeticGrid()
    {
        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true
        };

        _cosmeticGrid = new GridContainer
        {
            Columns = 4,
            HSeparationOverride = 10,
            VSeparationOverride = 10
        };
        scroll.AddChild(_cosmeticGrid);
        return scroll;
    }

    private Control CreateCosmeticCard(CoinShopItemEntry entry, string? selected)
    {
        var card = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = CardBackgroundColor,
                BorderColor = entry.Id == selected ? OwnedBorderColor : Color.Transparent,
                BorderThickness = new Thickness(1)
            }
        };

        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(10, 8)
        };

        var nameLabel = new Label
        {
            Text = entry.Name,
            Modulate = RarityColors[(int) entry.Rarity],
            ClipText = true
        };
        box.AddChild(nameLabel);

        var priceRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6
        };

        priceRow.AddChild(new TextureRect
        {
            Texture = _coinTexture,
            MinSize = new Vector2(16, 16),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            VerticalAlignment = VAlignment.Center
        });
        priceRow.AddChild(new Label { Text = entry.Price.ToString(), VerticalAlignment = VAlignment.Center });
        box.AddChild(priceRow);

        if (entry.Owned)
        {
            var isWorn = entry.Id == selected;
            var equipButton = new Button
            {
                Text = Loc.GetString(isWorn ? "coin-shop-unequip" : "coin-shop-equip")
            };
            equipButton.OnPressed += _ => _system.RequestSelectCosmetic(isWorn ? null : entry.Id);
            box.AddChild(equipButton);
        }
        else
        {
            var buyButton = new Button { Text = Loc.GetString("coin-shop-buy") };
            buyButton.OnPressed += _ => _system.RequestBuyCosmetic(entry.Id);
            box.AddChild(buyButton);
        }

        card.AddChild(box);
        return card;
    }

    private Control BuildColorRow()
    {
        _colorRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 10
        };
        return _colorRow;
    }

    private Control BuildGhostGrid()
    {
        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            MaxHeight = 170
        };

        _ghostGrid = new GridContainer
        {
            Columns = 4,
            HSeparationOverride = 10,
            VSeparationOverride = 10
        };
        scroll.AddChild(_ghostGrid);
        return scroll;
    }

    private Control CreateGhostCard(CoinShopGhostEntry entry)
    {
        var card = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = CardBackgroundColor,
                BorderColor = entry.Selected ? OwnedBorderColor : Color.Transparent,
                BorderThickness = new Thickness(1)
            }
        };

        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Margin = new Thickness(10, 8)
        };

        var icon = new TextureRect { MinSize = new Vector2(32, 32), Stretch = TextureRect.StretchMode.KeepAspectCentered };
        var tex = _system.GetGhostIcon(entry.IconRsiPath);
        if (tex != null)
            icon.Texture = tex;
        box.AddChild(icon);

        box.AddChild(new Label
        {
            Text = entry.Name,
            ToolTip = entry.Description,
            ClipText = true
        });

        if (entry.Owned)
        {
            var isSelected = entry.Selected;
            var button = new Button
            {
                Text = Loc.GetString(isSelected ? "coin-shop-ghost-selected" : "coin-shop-ghost-select")
            };
            button.OnPressed += _ =>
                _system.RequestSelectGhost(isSelected ? null : entry.Id);
            box.AddChild(button);
        }
        else
        {
            var priceRow = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 6
            };
            priceRow.AddChild(new TextureRect
            {
                Texture = _coinTexture,
                MinSize = new Vector2(16, 16),
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                VerticalAlignment = VAlignment.Center
            });
            priceRow.AddChild(new Label { Text = entry.Price.ToString(), VerticalAlignment = VAlignment.Center });
            box.AddChild(priceRow);

            var buyButton = new Button { Text = Loc.GetString("coin-shop-buy") };
            buyButton.OnPressed += _ => _system.RequestBuyGhost(entry.Id);
            box.AddChild(buyButton);
        }

        card.AddChild(box);
        return card;
    }

    private Control CreateColorButton(CoinShopColorEntry entry)
    {
        var button = new Button
        {
            Text = entry.Active ? Loc.GetString("coin-shop-color-active") : $"{entry.Price}",
            ToolTip = Loc.GetString("coin-shop-color-tooltip")
        };

        button.StyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.TryFromHex(entry.ColorHex, out var parsed) ? parsed : Color.Gray,
            BorderColor = entry.Active ? ActiveBorderColor : Color.FromHex("#2d3748"),
            BorderThickness = new Thickness(2)
        };

        button.OnPressed += _ => _system.RequestBuyOocColor(entry.Id);
        return button;
    }

    private Control BuildLootboxPanel()
    {
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#10141c"),
                BorderColor = Color.FromHex("#2d3748").WithAlpha(0.3f),
                BorderThickness = new Thickness(1)
            }
        };

        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(12, 8)
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 10
        };

        row.AddChild(new Label
        {
            Text = Loc.GetString("coin-shop-lootbox-description"),
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true
        });

        var lootboxButton = new Button { Text = Loc.GetString("coin-shop-lootbox-button") };
        lootboxButton.OnPressed += _ => _system.RequestLootbox();
        row.AddChild(lootboxButton);
        box.AddChild(row);

        _rollResultLabel = new Label { Visible = false };
        box.AddChild(_rollResultLabel);

        panel.AddChild(box);
        return panel;
    }
}
