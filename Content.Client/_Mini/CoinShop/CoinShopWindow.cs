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
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._Mini.CoinShop;

public sealed class CoinShopWindow : DefaultWindow
{
    private const string CoinIconPath = "/Textures/_Mini/Interface/Coin.png";

    // Ghost-shop design language (GhostShopWindow).
    private static readonly Color WindowBackgroundColor = Color.FromHex("#0e0c14");
    private static readonly Color HeroPanelColor = Color.FromHex("#1a1622");
    private static readonly Color AccentColor = Color.FromHex("#8c7da8");
    private static readonly Color CardBackgroundColor = Color.FromHex("#1e1a26");
    private static readonly Color SelectedCardColor = Color.FromHex("#1e4d3a").WithAlpha(0.7f);
    private static readonly Color SelectedBorderColor = Color.FromHex("#3fb950").WithAlpha(0.8f);
    private static readonly Color SubtitleColor = Color.FromHex("#8b949e");

    private static readonly Color[] RarityColors =
    [
        Color.FromHex("#c5d3ed"), // Common
        Color.FromHex("#69b4f2"), // Rare
        Color.FromHex("#b085f5"), // Epic
        Color.FromHex("#f9fc60"), // Legendary
    ];

    private readonly CoinShopSystem _system;
    private readonly Texture _coinTexture;

    private Label _balanceValueLabel = null!;
    private BoxContainer _cosmeticRow = null!;
    private GridContainer _colorGrid = null!;
    private BoxContainer _ghostRow = null!;
    private Label _rollResultLabel = null!;

    public CoinShopWindow(CoinShopSystem system)
    {
        _system = system;
        _coinTexture = IoCManager.Resolve<IResourceCache>().GetTexture(CoinIconPath);

        Title = Loc.GetString("coin-shop-title");
        MinSize = new Vector2(1000, 650);
        MaxSize = new Vector2(1000, float.PositiveInfinity);
        SetSize = new Vector2(1000, 720);

        var backdrop = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = WindowBackgroundColor }
        };
        Contents.AddChild(backdrop);

        var masterScroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
        };
        backdrop.AddChild(masterScroll);

        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 14,
            Margin = new Thickness(16, 12),
        };
        masterScroll.AddChild(root);

        root.AddChild(BuildHero());
        root.AddChild(BuildSectionHeader(Loc.GetString("coin-shop-cosmetics-header")));
        root.AddChild(BuildCosmeticRow());
        root.AddChild(BuildSectionHeader(Loc.GetString("coin-shop-colors-header")));
        root.AddChild(BuildColorGrid());
        root.AddChild(BuildSectionHeader(Loc.GetString("coin-shop-ghosts-header")));
        root.AddChild(BuildGhostRow());
        root.AddChild(BuildLootboxPanel());

        SetRollResultVisible(false);
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        var mode = base.GetDragModeFor(relativeMousePos);
        if (mode == DragMode.Move)
            return DragMode.Move;
        return mode & ~(DragMode.Left | DragMode.Right);
    }

    private Control BuildHero()
    {
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = HeroPanelColor,
                BorderColor = AccentColor.WithAlpha(0.2f),
                BorderThickness = new Thickness(0, 0, 0, 1),
                ContentMarginLeftOverride = 20,
                ContentMarginTopOverride = 20,
                ContentMarginRightOverride = 20,
                ContentMarginBottomOverride = 20
            }
        };

        var left = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 8,
            HorizontalExpand = true
        };
        panel.AddChild(left);

        left.AddChild(new Label
        {
            Text = Loc.GetString("coin-shop-title"),
            StyleClasses = { "LabelHeading" },
            Modulate = Color.White,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center
        });

        left.AddChild(new Label
        {
            Text = Loc.GetString("coin-shop-subtitle"),
            Modulate = SubtitleColor
        });

        var balanceBox = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 6
        };
        balanceBox.AddChild(new Label
        {
            Text = Loc.GetString("coin-shop-balance-label"),
            Modulate = SubtitleColor
        });

        _balanceValueLabel = new Label
        {
            Modulate = AccentColor
        };
        balanceBox.AddChild(_balanceValueLabel);

        balanceBox.AddChild(new TextureRect
        {
            Texture = _coinTexture,
            MinSize = new Vector2(16, 16),
            MaxSize = new Vector2(16, 16),
            TextureScale = new Vector2(0.3f, 0.3f),
            VerticalAlignment = VAlignment.Center
        });

        left.AddChild(balanceBox);
        return panel;
    }

    private Control BuildSectionHeader(string text)
    {
        return new Label
        {
            Text = text,
            StyleClasses = { "LabelHeading" },
            Modulate = AccentColor,
            Margin = new Thickness(4, 0, 0, 0)
        };
    }

    private Control BuildHScrollRow(BoxContainer row, float height)
    {
        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            HScrollEnabled = true,
            VScrollEnabled = false,
            MaxHeight = height
        };
        scroll.AddChild(row);
        return scroll;
    }

    private Control BuildCosmeticRow()
    {
        _cosmeticRow = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 16
        };
        return BuildHScrollRow(_cosmeticRow, 300);
    }

    private Control BuildColorGrid()
    {
        _colorGrid = new GridContainer
        {
            Columns = 4,
            HSeparationOverride = 16,
            VSeparationOverride = 12,
            HorizontalExpand = true
        };
        return _colorGrid;
    }

    private Control BuildGhostRow()
    {
        _ghostRow = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 16
        };
        return BuildHScrollRow(_ghostRow, 350);
    }

    public void UpdateState(CoinShopStateEvent state)
    {
        _balanceValueLabel.Text = state.Balance.ToString();

        _cosmeticRow.RemoveAllChildren();
        foreach (var entry in state.Cosmetics)
            _cosmeticRow.AddChild(CreateCosmeticCard(entry, state.SelectedCosmetic));

        _colorGrid.RemoveAllChildren();
        foreach (var entry in state.Colors)
            _colorGrid.AddChild(CreateColorCard(entry));

        _ghostRow.RemoveAllChildren();
        foreach (var entry in state.Ghosts)
            _ghostRow.AddChild(CreateGhostCard(entry));
    }

    public void ShowRollResult(CoinShopRollResultEvent ev)
    {
        var key = ev.Duplicate
            ? "coin-shop-roll-duplicate"
            : "coin-shop-roll-new";

        SetRollResultVisible(true);
        _rollResultLabel.Text = Loc.GetString(key,
            ("item", ev.ItemName),
            ("refund", ev.Refund),
            ("rarity", Loc.GetString($"coin-shop-rarity-{ev.Rarity.ToString().ToLower()}")));
        _rollResultLabel.Modulate = RarityColors[(int) ev.Rarity];
    }

    private void SetRollResultVisible(bool visible)
    {
        if (_rollResultLabel != null)
            _rollResultLabel.Visible = visible;
    }

    private Control BuildIconBox(string iconRsi, Vector2 size)
    {
        var imageBox = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            MinSize = new Vector2(0, size.Y + 20)
        };

        var tex = _system.GetItemIcon(iconRsi);
        if (tex != null)
        {
            imageBox.AddChild(new TextureRect
            {
                Texture = tex,
                MinSize = size,
                MaxSize = size,
                Stretch = TextureRect.StretchMode.KeepAspectCentered
            });
        }
        else
        {
            imageBox.AddChild(new Label { Text = "?", Modulate = Color.White });
        }

        return imageBox;
    }

    private Control CreateCosmeticCard(CoinShopItemEntry entry, string? selected)
    {
        var isWorn = entry.Id == selected;

        var panel = new PanelContainer
        {
            MinSize = new Vector2(240, 290),
            MaxSize = new Vector2(240, 290),
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = isWorn ? SelectedCardColor : CardBackgroundColor,
                BorderColor = isWorn ? SelectedBorderColor : Color.Transparent,
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 16,
                ContentMarginTopOverride = 16,
                ContentMarginRightOverride = 16,
                ContentMarginBottomOverride = 16
            }
        };

        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6
        };
        panel.AddChild(root);

        root.AddChild(BuildIconBox(entry.IconRsi, new Vector2(96, 96)));

        root.AddChild(new Label
        {
            Text = entry.Name,
            StyleClasses = { "LabelHeading" },
            Modulate = RarityColors[(int) entry.Rarity],
            HorizontalAlignment = HAlignment.Center,
            MaxWidth = 208,
            ClipText = true,
            ToolTip = Loc.GetString($"coin-shop-rarity-{entry.Rarity.ToString().ToLower()}"),
        });

        root.AddChild(BuildPriceRow(entry.Price, center: true));

        root.AddChild(CreateActionButton(
            isWorn ? "coin-shop-unequip" : "coin-shop-equip",
            208,
            () => _system.RequestSelectCosmetic(isWorn ? null : entry.Id),
            isWorn));

        return panel;
    }

    private Control CreateGhostCard(CoinShopGhostEntry entry)
    {
        var panel = new PanelContainer
        {
            MinSize = new Vector2(290, 320),
            MaxSize = new Vector2(290, 320),
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = entry.Selected ? SelectedCardColor : CardBackgroundColor,
                BorderColor = entry.Selected ? SelectedBorderColor : Color.Transparent,
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 16,
                ContentMarginTopOverride = 16,
                ContentMarginRightOverride = 16,
                ContentMarginBottomOverride = 16
            }
        };

        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6
        };
        panel.AddChild(root);

        root.AddChild(BuildIconBox(entry.IconRsiPath, new Vector2(96, 96)));

        root.AddChild(new Label
        {
            Text = entry.Name,
            StyleClasses = { "LabelHeading" },
            Modulate = Color.White,
            HorizontalAlignment = HAlignment.Center,
            MaxWidth = 268,
            ClipText = true,
            ToolTip = entry.Description,
        });

        if (entry.Owned)
        {
            var isSelected = entry.Selected;
            root.AddChild(CreateActionButton(
                isSelected ? "coin-shop-ghost-selected" : "coin-shop-ghost-select",
                268,
                () => _system.RequestSelectGhost(isSelected ? null : entry.Id),
                isSelected));
        }
        else
        {
            root.AddChild(BuildPriceRow(entry.Price, center: true));
            root.AddChild(CreatePriceButton(entry.Price, 268,
                () => _system.RequestBuyGhost(entry.Id)));
        }

        return panel;
    }

    private Control CreateColorCard(CoinShopColorEntry entry)
    {
        var panel = new PanelContainer
        {
            MinSize = new Vector2(220, 200),
            MaxSize = new Vector2(220, 200),
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = CardBackgroundColor,
                BorderColor = entry.Active ? SelectedBorderColor : Color.Transparent,
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 14,
                ContentMarginTopOverride = 14,
                ContentMarginRightOverride = 14,
                ContentMarginBottomOverride = 14
            }
        };

        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 8
        };
        panel.AddChild(root);

        // Big color swatch.
        var swatch = new PanelContainer
        {
            MinSize = new Vector2(0, 64),
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.TryFromHex(entry.ColorHex, out var parsed) ? parsed : Color.Gray,
                BorderColor = Color.FromHex("#121824"),
                BorderThickness = new Thickness(2)
            }
        };
        root.AddChild(swatch);

        root.AddChild(new Label
        {
            Text = entry.Active ? Loc.GetString("coin-shop-color-active") : Loc.GetString("coin-shop-color-days", ("days", 30)),
            Modulate = entry.Active ? SelectedBorderColor : SubtitleColor,
            HorizontalAlignment = HAlignment.Center
        });

        if (!entry.Active)
        {
            root.AddChild(BuildPriceRow(entry.Price, center: true));
            root.AddChild(CreatePriceButton(entry.Price, 192, () => _system.RequestBuyOocColor(entry.Id)));
        }
        else
        {
            root.AddChild(new Control { VerticalExpand = true });
        }

        return panel;
    }

    private Control BuildLootboxPanel()
    {
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = HeroPanelColor,
                BorderColor = AccentColor.WithAlpha(0.2f),
                BorderThickness = new Thickness(0, 1, 0, 0),
                ContentMarginLeftOverride = 20,
                ContentMarginTopOverride = 16,
                ContentMarginRightOverride = 20,
                ContentMarginBottomOverride = 16
            }
        };

        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 20,
            VerticalAlignment = VAlignment.Center
        };

        var left = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true
        };

        left.AddChild(new Label
        {
            Text = Loc.GetString("coin-shop-lootbox-title"),
            StyleClasses = { "LabelHeading" },
            Modulate = Color.White
        });

        left.AddChild(new Label
        {
            Text = Loc.GetString("coin-shop-lootbox-description"),
            Modulate = SubtitleColor
        });

        _rollResultLabel = new Label { Visible = false };
        left.AddChild(_rollResultLabel);

        row.AddChild(left);
        row.AddChild(CreatePriceButton(25, 180, () => _system.RequestLootbox()));

        panel.AddChild(row);
        return panel;
    }

    private Control BuildPriceRow(int price, bool center)
    {
        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            HorizontalAlignment = center ? HAlignment.Center : HAlignment.Left
        };

        row.AddChild(new Label
        {
            Text = price.ToString(),
            Modulate = Color.White,
            VerticalAlignment = VAlignment.Center
        });

        row.AddChild(new TextureRect
        {
            Texture = _coinTexture,
            MinSize = new Vector2(16, 16),
            MaxSize = new Vector2(16, 16),
            TextureScale = new Vector2(0.4f, 0.4f),
            VerticalAlignment = VAlignment.Center
        });

        return row;
    }

    private Button CreateActionButton(string localeKey, float width, Action onPressed, bool disabled = false)
    {
        var button = new Button
        {
            MinSize = new Vector2(width, 40),
            MaxSize = new Vector2(width, 40),
            Disabled = disabled
        };

        var content = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center
        };

        content.AddChild(new Label
        {
            Text = Loc.GetString(localeKey),
            Modulate = Color.White,
            StyleClasses = { "LabelHeading" },
            VerticalAlignment = VAlignment.Center
        });

        button.AddChild(content);
        button.OnPressed += _ => onPressed();
        return button;
    }

    private Button CreatePriceButton(int price, float width, Action onPressed)
    {
        var button = new Button
        {
            MinSize = new Vector2(width, 40),
            MaxSize = new Vector2(width, 40)
        };

        var content = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center
        };

        content.AddChild(new Label
        {
            Text = price.ToString(),
            Modulate = Color.White,
            StyleClasses = { "LabelHeading" },
            VerticalAlignment = VAlignment.Center
        });

        content.AddChild(new TextureRect
        {
            Texture = _coinTexture,
            MinSize = new Vector2(16, 16),
            MaxSize = new Vector2(16, 16),
            TextureScale = new Vector2(0.4f, 0.4f),
            VerticalAlignment = VAlignment.Center
        });

        button.AddChild(content);
        button.OnPressed += _ => onPressed();
        return button;
    }
}
