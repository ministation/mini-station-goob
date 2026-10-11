using System.Numerics;
using Content.Client.Resources;
using Content.Shared._Mini.CustomGhost;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._Mini.CustomGhost;

public sealed class GhostShopWindow : DefaultWindow
{
    private static readonly Color WindowBackgroundColor = Color.FromHex("#0e0c14");
    private static readonly Color HeroPanelColor = Color.FromHex("#1a1622").WithAlpha(0.9f);
    private static readonly Color AccentColor = Color.FromHex("#8c7da8");
    private static readonly Color SubtitleColor = Color.FromHex("#8b949e");
    private static readonly Color CardBackgroundColor = Color.FromHex("#1e1a26").WithAlpha(0.8f);
    private static readonly Color CardBorderColor = Color.Transparent;
    private static readonly Color SelectedCardColor = Color.FromHex("#1e4d3a").WithAlpha(0.7f);
    private static readonly Color SelectedBorderColor = Color.FromHex("#3fb950").WithAlpha(0.8f);

    private const string CoinIconPath = "/Textures/_Mini/Interface/Coin.png";

    private readonly GhostShopSystem _system;
    private readonly Texture _coinTexture;

    private Label _balanceValueLabel = null!;
    private BoxContainer _themeRow = null!;
    private BoxContainer _colorRow = null!;

    public GhostShopWindow(GhostShopSystem system)
    {
        _system = system;
        _coinTexture = IoCManager.Resolve<IResourceCache>().GetTexture(CoinIconPath);

        Title = "Магазин призраков";
        MinSize = new Vector2(1000, 700);
        MaxSize = new Vector2(1000, float.PositiveInfinity);
        SetSize = new Vector2(1000, 800);

        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 24,
            Margin = new Thickness(24, 16)
        };

        var backdrop = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = WindowBackgroundColor }
        };
        Contents.AddChild(backdrop);
        backdrop.AddChild(root);

        root.AddChild(BuildHero());

        root.AddChild(new Label
        {
            Text = Loc.GetString("ghost-shop-window-subtitle"),
            StyleClasses = { "LabelHeading" },
            Margin = new Thickness(4, 0, 0, 0),
            Modulate = Color.FromHex("#adbac7")
        });

        var gridPanel = new PanelContainer
        {
            VerticalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#10141c"),
                BorderColor = Color.FromHex("#2d3748").WithAlpha(0.3f),
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 12,
                ContentMarginTopOverride = 12,
                ContentMarginRightOverride = 12,
                ContentMarginBottomOverride = 12
            }
        };
        root.AddChild(gridPanel);

        var themeScroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            HScrollEnabled = true,
            VScrollEnabled = false
        };
        gridPanel.AddChild(themeScroll);

        _themeRow = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 16
        };
        themeScroll.AddChild(_themeRow);

        var colorHeader = new Label
        {
            Text = Loc.GetString("ghost-shop-color-header"),
            StyleClasses = { "LabelHeading" },
            Modulate = AccentColor,
            Margin = new Thickness(4, 12, 0, 0)
        };
        root.AddChild(colorHeader);

        // ScrollContainer reports a zero desired height by default — without these the
        // whole color section collapses into an empty strip under its header.
        var colorScroll = new ScrollContainer
        {
            HorizontalExpand = true,
            HScrollEnabled = true,
            VScrollEnabled = false,
            ReturnMeasure = true,
            MinHeight = 150,
            MaxHeight = 150
        };
        root.AddChild(colorScroll);

        _colorRow = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 16
        };
        colorScroll.AddChild(_colorRow);
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        var mode = base.GetDragModeFor(relativeMousePos);
        if (mode == DragMode.Move)
            return DragMode.Move;
        return mode & ~(DragMode.Left | DragMode.Right);
    }

    public void UpdateState(GhostShopStateEvent state)
    {
        _balanceValueLabel.Text = state.Balance.ToString();

        _themeRow.RemoveAllChildren();
        foreach (var entry in state.Themes)
        {
            _themeRow.AddChild(CreateThemeCard(entry));
        }

        _colorRow.RemoveAllChildren();
        foreach (var entry in state.Colors)
            _colorRow.AddChild(CreateColorCard(entry));
    }

    private Control CreateColorCard(GhostColorEntry entry)
    {
        var panel = new PanelContainer
        {
            MinSize = new Vector2(200, 120),
            MaxSize = new Vector2(200, 120),
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = entry.Active ? SelectedCardColor : CardBackgroundColor,
                BorderColor = entry.Active ? SelectedBorderColor : CardBorderColor,
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 12,
                ContentMarginTopOverride = 12,
                ContentMarginRightOverride = 12,
                ContentMarginBottomOverride = 12
            }
        };

        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 8
        };
        panel.AddChild(root);

        root.AddChild(new PanelContainer
        {
            MinSize = new Vector2(0, 44),
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.TryFromHex(entry.ColorHex, out var parsed) ? parsed : Color.Gray,
                BorderColor = Color.FromHex("#121824"),
                BorderThickness = new Thickness(2)
            }
        });

        root.AddChild(new Label
        {
            Text = entry.Active ? Loc.GetString("ghost-shop-color-active") : Loc.GetString("ghost-shop-color-days"),
            Modulate = entry.Active ? SelectedBorderColor : SubtitleColor,
            HorizontalAlignment = HAlignment.Center
        });

        if (entry.Active)
        {
            root.AddChild(new Control { VerticalExpand = true });
        }
        else
        {
            var button = new Button
            {
                MinSize = new Vector2(176, 36),
                MaxSize = new Vector2(176, 36)
            };

            var content = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                SeparationOverride = 6,
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center
            };

            content.AddChild(new Label
            {
                Text = entry.Price.ToString(),
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
            button.OnPressed += _ => _system.RequestBuyColor(entry.Id);
            root.AddChild(button);
        }

        return panel;
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

        var content = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 20
        };
        panel.AddChild(content);

        var left = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 8,
            HorizontalExpand = true
        };
        content.AddChild(left);

        left.AddChild(new Label
        {
            Text = "Магазин призраков",
            StyleClasses = { "LabelHeading" },
            Modulate = Color.White,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center
        });

        left.AddChild(new Label
        {
            Text = Loc.GetString("ghost-shop-hero-subtitle"),
            Modulate = Color.FromHex("#8b949e")
        });

        var infoRow = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 24
        };
        left.AddChild(infoRow);

        var balanceBox = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 6
        };
        balanceBox.AddChild(new Label
        {
            Text = Loc.GetString("ghost-shop-balance-label"),
            Modulate = Color.FromHex("#8b949e")
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

        infoRow.AddChild(balanceBox);

        return panel;
    }

    private Control CreateThemeCard(GhostThemeEntry entry)
    {
        var panel = new PanelContainer
        {
            MinSize = new Vector2(290, 0),
            MaxSize = new Vector2(290, 1000),
            VerticalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = entry.Selected ? SelectedCardColor : CardBackgroundColor,
                BorderColor = entry.Selected ? SelectedBorderColor : CardBorderColor,
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
            SeparationOverride = 8,
            VerticalExpand = true
        };
        panel.AddChild(root);

        var imageBox = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            MinSize = new Vector2(0, 140)
        };
        root.AddChild(imageBox);

        if (!string.IsNullOrEmpty(entry.IconRsiPath) && !string.IsNullOrEmpty(entry.IconRsiState))
        {
            var tex = _system.GetIconTexture(entry.IconRsiPath, entry.IconRsiState);
            if (tex != null)
            {
                imageBox.AddChild(new TextureRect
                {
                    Texture = tex,
                    MinSize = new Vector2(96, 96),
                    MaxSize = new Vector2(96, 96),
                    Stretch = TextureRect.StretchMode.KeepAspectCentered
                });
            }
            else
            {
                imageBox.AddChild(new Label { Text = "?", Modulate = Color.White });
            }
        }
        else
        {
            imageBox.AddChild(new Label { Text = "?", Modulate = Color.White });
        }

        root.AddChild(new Label
        {
            Text = entry.Name,
            StyleClasses = { "LabelHeading" },
            Modulate = Color.White,
            HorizontalAlignment = HAlignment.Center,
            MaxWidth = 268
        });

        if (!string.IsNullOrEmpty(entry.Description))
        {
            root.AddChild(new Label
            {
                Text = entry.Description,
                Modulate = AccentColor,
                HorizontalAlignment = HAlignment.Center,
                MaxWidth = 268
            });
        }

        root.AddChild(new Control { VerticalExpand = true });

        var actionButton = new Button
        {
            MinSize = new Vector2(268, 40),
            MaxSize = new Vector2(268, 40),
        };

        var buttonContent = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center
        };

        if (entry.Owned && entry.Selected)
        {
            actionButton.Disabled = true;
            buttonContent.AddChild(new Label
            {
                Text = "✓",
                Modulate = Color.White,
                StyleClasses = { "LabelHeading" },
                VerticalAlignment = VAlignment.Center
            });
        }
        else if (entry.Owned && !entry.Selected)
        {
            actionButton.OnPressed += _ => _system.RequestSelect(entry.Id);
            buttonContent.AddChild(new Label
            {
                Text = Loc.GetString("ghost-shop-btn-select"),
                Modulate = Color.White,
                StyleClasses = { "LabelHeading" },
                VerticalAlignment = VAlignment.Center
            });
        }
        else
        {
            actionButton.OnPressed += _ =>
            {
                actionButton.Disabled = true;
                _system.RequestBuy(entry.Id);
            };
            buttonContent.AddChild(new Label
            {
                Text = entry.Price.ToString(),
                Modulate = Color.White,
                StyleClasses = { "LabelHeading" },
                VerticalAlignment = VAlignment.Center
            });
            buttonContent.AddChild(new TextureRect
            {
                Texture = _coinTexture,
                MinSize = new Vector2(16, 16),
                MaxSize = new Vector2(16, 16),
                TextureScale = new Vector2(0.4f, 0.4f),
                Stretch = TextureRect.StretchMode.KeepCentered,
                VerticalAlignment = VAlignment.Center
            });
        }

        actionButton.AddChild(buttonContent);
        root.AddChild(actionButton);

        return panel;
    }
}
