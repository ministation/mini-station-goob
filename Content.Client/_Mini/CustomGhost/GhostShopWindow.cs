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
using Robust.Shared.Timing;
using Robust.Shared.Utility;
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

    /// <summary>Иконка идущих часов для таймера тем за наигранное время.</summary>
    private static readonly SpriteSpecifier ClockIcon = new SpriteSpecifier.Rsi(
        new ResPath("/Textures/_Mini/Interface/daily_rewards.rsi"), "clock_animated");

    private readonly GhostShopSystem _system;
    private readonly Texture _coinTexture;

    private Label _balanceValueLabel = null!;
    private Label _statusLabel = null!;
    private BoxContainer _themeRow = null!;
    private BoxContainer _colorRow = null!;

    /// <summary>Хэллоуинское наигранное время с сервера, секунды (синхронизируется каждым состоянием).</summary>
    private double _playtimeSeconds;
    private double _tickAccumulator;

    /// <summary>Метка таймера в карточке темы за наигранное время -> сколько всего секунд нужно набрать.</summary>
    private readonly List<(Label Label, double RequiredSeconds)> _timerLabels = new();

    public GhostShopWindow(GhostShopSystem system)
    {
        _system = system;
        _coinTexture = IoCManager.Resolve<IResourceCache>().GetTexture(CoinIconPath);

        Title = "Магазин призраков";
        MinSize = new Vector2(820, 500);
        MaxSize = new Vector2(820, float.PositiveInfinity);
        SetSize = new Vector2(820, 560);

        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 10,
            Margin = new Thickness(16, 12)
        };

        var backdrop = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = WindowBackgroundColor }
        };
        Contents.AddChild(backdrop);
        backdrop.AddChild(root);

        root.AddChild(BuildHero());

        _statusLabel = new Label
        {
            Visible = false,
            Margin = new Thickness(4, 0, 0, 0),
            Modulate = Color.FromHex("#f0883e")
        };
        root.AddChild(_statusLabel);

        // Темы и OOC-цвет ника живут в отдельных вкладках, как достижения в наградах: раньше
        // обе ленты висели друг под другом, и окно растягивалось на весь экран.
        var tabs = new TabContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true
        };
        root.AddChild(tabs);

        _themeRow = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 16
        };
        var themeScroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            HScrollEnabled = true,
            VScrollEnabled = false
        };
        themeScroll.AddChild(_themeRow);
        tabs.AddChild(themeScroll);
        TabContainer.SetTabTitle(themeScroll, Loc.GetString("ghost-shop-window-subtitle"));

        _colorRow = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 16
        };
        var colorScroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            HScrollEnabled = true,
            VScrollEnabled = false
        };
        colorScroll.AddChild(_colorRow);
        tabs.AddChild(colorScroll);
        TabContainer.SetTabTitle(colorScroll, Loc.GetString("ghost-shop-color-header"));
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

        _statusLabel.Visible = !string.IsNullOrEmpty(state.Error);
        _statusLabel.Text = state.Error ?? "";

        // Сервер прислал свежий счётчик - локальный тик таймера стартует от него.
        _playtimeSeconds = state.PlaytimeSeconds;
        _timerLabels.Clear();

        _themeRow.RemoveAllChildren();
        foreach (var entry in state.Themes)
        {
            _themeRow.AddChild(CreateThemeCard(entry));
        }

        _colorRow.RemoveAllChildren();
        foreach (var entry in state.Colors)
            _colorRow.AddChild(CreateColorCard(entry));
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_timerLabels.Count == 0)
            return;

        // Таймер тикает раз в секунду; истина приходит с сервера при каждом обновлении окна.
        _tickAccumulator += args.DeltaSeconds;
        if (_tickAccumulator < 1)
            return;

        _tickAccumulator -= 1;
        _playtimeSeconds = Math.Max(0, _playtimeSeconds - 1);

        foreach (var (label, required) in _timerLabels)
            label.Text = FormatPlaytimeLeft(required - _playtimeSeconds);
    }

    /// <summary>Таймер без подписи: 10:00:00, рядом в карточке идут часы.</summary>
    private static string FormatPlaytimeLeft(double secondsLeft)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, secondsLeft));
        return ts.TotalHours >= 1
            ? $"{(int) ts.TotalHours}:{ts.Minutes:00}:{ts.Seconds:00}"
            : $"{ts.Minutes}:{ts.Seconds:00}";
    }

    private Control CreateColorCard(GhostColorEntry entry)
    {
        var panel = new PanelContainer
        {
            MinSize = new Vector2(200, 190),
            MaxSize = new Vector2(200, 190),
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
            SeparationOverride = 6
        };
        panel.AddChild(root);

        if (!string.IsNullOrEmpty(entry.Name))
        {
            root.AddChild(new Label
            {
                Text = entry.Name,
                StyleClasses = { "LabelHeading" },
                Modulate = Color.White,
                HorizontalAlignment = HAlignment.Center
            });
        }

        // Живое превью: как ник будет выглядеть в OOC - текст прямо цветом темы.
        var preview = new PanelContainer
        {
            MinSize = new Vector2(0, 44),
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#121824"),
                BorderColor = Color.TryFromHex(entry.ColorHex, out var border) ? border : Color.Gray,
                BorderThickness = new Thickness(2),
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 8
            }
        };
        preview.AddChild(new Label
        {
            Text = Loc.GetString("ghost-shop-color-preview"),
            Modulate = Color.TryFromHex(entry.ColorHex, out var parsed) ? parsed : Color.Gray,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center
        });
        root.AddChild(preview);

        var status = entry.Active
            ? Loc.GetString("ghost-shop-color-active-days", ("days", entry.DaysLeft))
            : entry.Owned ? Loc.GetString("ghost-shop-color-owned") : Loc.GetString("ghost-shop-color-days");

        root.AddChild(new Label
        {
            Text = status,
            Modulate = entry.Active ? SelectedBorderColor : SubtitleColor,
            HorizontalAlignment = HAlignment.Center
        });

        if (entry.Active)
        {
            root.AddChild(new Control { VerticalExpand = true });
            return panel;
        }

        if (entry.Owned)
        {
            var selectButton = new Button
            {
                MinSize = new Vector2(176, 36),
                MaxSize = new Vector2(176, 36)
            };

            var selectContent = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                SeparationOverride = 6,
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center
            };

            selectContent.AddChild(new Label
            {
                Text = Loc.GetString("ghost-shop-color-select"),
                Modulate = Color.White,
                StyleClasses = { "LabelHeading" },
                VerticalAlignment = VAlignment.Center
            });

            selectButton.AddChild(selectContent);
            selectButton.OnPressed += _ => _system.RequestSelectColor(entry.Id);
            root.AddChild(selectButton);
            return panel;
        }

        root.AddChild(new Control { VerticalExpand = true });

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
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center
        };
        content.AddChild(left);

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

    /// <summary>Идущие часы (RSI-анимация 64 px), ужатые до размера строки кнопки.</summary>
    private static Control CreateClockIcon()
    {
        var slot = new Control
        {
            MinSize = new Vector2(16, 16),
            MaxSize = new Vector2(16, 16),
            VerticalAlignment = VAlignment.Center
        };

        var clock = new AnimatedTextureRect
        {
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center
        };
        clock.SetFromSpriteSpecifier(ClockIcon);
        clock.DisplayRect.TextureScale = new Vector2(0.25f, 0.25f);
        slot.AddChild(clock);
        return slot;
    }

    private Control CreateThemeCard(GhostThemeEntry entry)
    {
        var panel = new PanelContainer
        {
            MinSize = new Vector2(290, 0),
            MaxSize = new Vector2(290, float.PositiveInfinity),
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
            SeparationOverride = 8
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
                Text = Loc.GetString("ghost-shop-btn-selected"),
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

            if (entry.PlaytimeHours > 0)
            {
                // Тема за наигранное время: пока часы не набраны - тикающий таймер остатка,
                // после набора - кнопка "Собрать". Монеты не участвуют.
                var requiredSeconds = entry.PlaytimeHours * 3600.0;
                var remaining = requiredSeconds - _playtimeSeconds;

                if (remaining > 0)
                {
                    actionButton.Disabled = true;
                    buttonContent.AddChild(CreateClockIcon());
                    var timerLabel = new Label
                    {
                        Text = FormatPlaytimeLeft(remaining),
                        Modulate = Color.White,
                        StyleClasses = { "LabelHeading" },
                        VerticalAlignment = VAlignment.Center
                    };
                    _timerLabels.Add((timerLabel, requiredSeconds));
                    buttonContent.AddChild(timerLabel);
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
                        Text = Loc.GetString("ghost-shop-btn-claim"),
                        Modulate = SelectedBorderColor,
                        StyleClasses = { "LabelHeading" },
                        VerticalAlignment = VAlignment.Center
                    });
                }
            }
            else
            {
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
        }

        actionButton.AddChild(buttonContent);
        root.AddChild(actionButton);

        return panel;
    }
}
