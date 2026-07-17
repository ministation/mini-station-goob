// SPDX-FileCopyrightText: 2026 Casha
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT
using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Client.Lobby.UI.Roles;
using Content.Client.Players.PlayTimeTracking;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Shared._Mini.AntagTokens;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;
using static Robust.Client.UserInterface.Controls.LayoutContainer;

namespace Content.Client._Mini.AntagTokens;

public sealed class AntagTokenWindow : DefaultWindow
{
    private static readonly Color WindowBackgroundColor = Color.FromHex("#0e0c14");
    private static readonly Color HeroPanelColor = Color.FromHex("#1a1622").WithAlpha(0.9f);
    private static readonly Color AccentColor = Color.FromHex("#8c7da8");
    private static readonly Color CardBackgroundColor = Color.FromHex("#1e1a26").WithAlpha(0.8f);
    private static readonly Color CardBorderColor = Color.Transparent;
    private static readonly Color PurchasedCardColor = Color.FromHex("#1e4d3a").WithAlpha(0.7f);
    private static readonly Color PurchasedBorderColor = Color.FromHex("#3fb950").WithAlpha(0.8f);
    private static readonly Color BuyButtonUnavailableBg = Color.FromHex("#1e1521");
    private static readonly Color BuyButtonTimerText = Color.FromHex("#972d1c");

    private static readonly StyleBoxFlat BuyButtonUnavailableStyle = new()
    {
        BackgroundColor = BuyButtonUnavailableBg,
        ContentMarginLeftOverride = 8,
        ContentMarginRightOverride = 8,
        ContentMarginTopOverride = 6,
        ContentMarginBottomOverride = 6
    };

    private const int GridColumns = 3;
    private const string CoinIconPath = "/Textures/_Mini/Interface/Coin.png";

    public event Action<string>? OnPurchasePressed;
    public event Action? OnClearPressed;

    [Dependency] private readonly IEntitySystemManager _entitySystems = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IClientPreferencesManager _preferencesManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly JobRequirementsManager _requirements = default!;

    private readonly IResourceCache _resourceCache;
    private readonly Texture _coinTexture;

    private Label _balanceValueLabel = null!;
    private Label _capValueLabel = null!;
    private Label _depositLabel = null!;
    private Button _clearButton = null!;
    private ScrollContainer _roleScroll = null!;
    private GridContainer _roleGrid = null!;
    private Control? _loadingOverlay;
    private readonly Dictionary<string, RoleBuyButton> _roleBuyButtons = new();
    private readonly Dictionary<string, int> _lastServerCooldownRemaining = new();
    private readonly Dictionary<string, bool> _playtimeBlockedByRole = new();

    private sealed class RoleBuyButton
    {
        public required Button Button;
        public required Control NormalContent;
        public required AntagTokenRoleEntry Entry;
    }

    public AntagTokenWindow()
    {
        IoCManager.InjectDependencies(this);
        _resourceCache = IoCManager.Resolve<IResourceCache>();
        _coinTexture = _resourceCache.GetTexture(CoinIconPath);

        Title = Loc.GetString("antag-token-window-title");
        MinSize = new Vector2(1000, 650);
        MaxSize = new Vector2(1000, float.PositiveInfinity);
        SetSize = new Vector2(1000, 650);

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
            Text = Loc.GetString("antag-token-window-roles-title"),
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

        _roleScroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            HScrollEnabled = false
        };
        gridPanel.AddChild(_roleScroll);

        _roleGrid = new GridContainer
        {
            Columns = GridColumns,
            HSeparationOverride = 16,
            VSeparationOverride = 16,
            HorizontalAlignment = HAlignment.Stretch
        };
        _roleScroll.AddChild(_roleGrid);

        _clearButton.OnPressed += _ => OnClearPressed?.Invoke();
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        var mode = base.GetDragModeFor(relativeMousePos);
        if (mode == DragMode.Move)
            return DragMode.Move;

        return mode & ~(DragMode.Left | DragMode.Right);
    }

    public void SetLoading(bool loading)
    {
        if (!loading)
            return;

        if (_loadingOverlay != null)
            return;

        _roleBuyButtons.Clear();
        _roleGrid.RemoveAllChildren();

        _roleScroll.RemoveChild(_roleGrid);

        var center = new CenterContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true
        };
        center.AddChild(new Label
        {
            Text = Loc.GetString("antag-token-window-loading"),
            Modulate = Color.FromHex("#8b949e")
        });
        _roleScroll.AddChild(center);
        _loadingOverlay = center;

        _balanceValueLabel.Text = Loc.GetString("antag-token-window-loading");
        _capValueLabel.Text = Loc.GetString("antag-token-window-loading");
        _depositLabel.Text = Loc.GetString("antag-token-window-loading");
        _clearButton.Disabled = true;
    }

    public void RefreshPurchaseCooldowns(IReadOnlyDictionary<string, int> cooldownsByRole)
    {
        foreach (var (roleId, ui) in _roleBuyButtons)
        {
            var cd = cooldownsByRole.GetValueOrDefault(roleId, 0);
            if (cd > 0)
            {
                ui.Button.RemoveAllChildren();
                ui.Button.AddChild(new Label
                {
                    Text = FormatCooldownHms(cd),
                    Modulate = BuyButtonTimerText,
                    StyleClasses = { "LabelHeading" },
                    HorizontalAlignment = HAlignment.Center,
                    VerticalAlignment = VAlignment.Center
                });
                ui.Button.Disabled = true;
                ui.Button.StyleBoxOverride = BuyButtonUnavailableStyle;
            }
            else
            {
                ui.Button.RemoveAllChildren();
                ui.Button.AddChild(ui.NormalContent);
                ui.Button.Disabled = IsButtonDisabled(ui.Entry, cooldownsByRole);
                ui.Button.StyleBoxOverride = ui.Button.Disabled ? BuyButtonUnavailableStyle : null;
            }
        }
    }

    public void UpdateState(AntagTokenState state)
    {
        RestoreGridFromLoading();

        _lastServerCooldownRemaining.Clear();
        foreach (var r in state.Roles)
            _lastServerCooldownRemaining[r.RoleId] = r.PurchaseCooldownSecondsRemaining;

        _balanceValueLabel.Text = state.Balance.ToString();
        _capValueLabel.Text = state.MonthlyCap.HasValue
            ? $"{state.MonthlyEarned} / {state.MonthlyCap.Value}"
            : $"{state.MonthlyEarned}";

        _depositLabel.Text = state.ActiveDepositRoleId != null &&
                             _entitySystems.GetEntitySystem<AntagTokenListingSystem>().TryGetListing(state.ActiveDepositRoleId, out var role)
            ? Loc.GetString("antag-token-window-deposit", ("role", Loc.GetString(role.NameLocKey)))
            : Loc.GetString("antag-token-window-no-deposit");

        _clearButton.Disabled = state.ActiveDepositRoleId == null;

        _roleBuyButtons.Clear();
        _playtimeBlockedByRole.Clear();
        _roleGrid.RemoveAllChildren();
        foreach (var roleEntry in state.Roles)
        {
            _roleGrid.AddChild(CreateRoleCard(roleEntry));
        }
    }

    private void RestoreGridFromLoading()
    {
        if (_loadingOverlay == null)
            return;

        _roleScroll.RemoveChild(_loadingOverlay);
        _loadingOverlay.Dispose();
        _loadingOverlay = null;

        if (_roleGrid.Parent != _roleScroll)
            _roleScroll.AddChild(_roleGrid);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            RestoreGridFromLoading();

        base.Dispose(disposing);
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

        var titleRow = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            HorizontalExpand = true
        };
        titleRow.AddChild(new Label
        {
            Text = Loc.GetString("antag-token-window-title"),
            StyleClasses = { "LabelHeading" },
            Modulate = Color.White,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center
        });
        _clearButton = new Button
        {
            Text = Loc.GetString("antag-token-window-clear"),
            MinSize = new Vector2(120, 38),
            Modulate = Color.White,
            HorizontalAlignment = HAlignment.Right,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(1, 0, 1, 0)
        };
        titleRow.AddChild(_clearButton);
        left.AddChild(titleRow);

        left.AddChild(new Label
        {
            Text = Loc.GetString("antag-token-window-subtitle"),
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
            Text = "Баланс:",
            Modulate = Color.FromHex("#8b949e")
        });

        var balanceValueContainer = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 4
        };

        _balanceValueLabel = new Label
        {
            Modulate = AccentColor
        };
        balanceValueContainer.AddChild(_balanceValueLabel);
        balanceBox.AddChild(balanceValueContainer);
        infoRow.AddChild(balanceBox);

        balanceValueContainer.AddChild(new TextureRect
        {
            Texture = _coinTexture,
            MinSize = new Vector2(16, 16),
            MaxSize = new Vector2(16, 16),
            TextureScale = new Vector2(0.3f, 0.3f),
            VerticalAlignment = VAlignment.Center
        });

        var capBox = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 6
        };
        capBox.AddChild(new Label
        {
            Text = "Лимит:",
            Modulate = Color.FromHex("#8b949e")
        });

        var capValueContainer = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 4
        };

        _capValueLabel = new Label
        {
            Modulate = Color.FromHex("#adbac7")
        };
        capValueContainer.AddChild(_capValueLabel);
        capBox.AddChild(capValueContainer);
        infoRow.AddChild(capBox);

        capValueContainer.AddChild(new TextureRect
        {
            Texture = _coinTexture,
            MinSize = new Vector2(16, 16),
            MaxSize = new Vector2(16, 16),
            TextureScale = new Vector2(0.3f, 0.3f),
            VerticalAlignment = VAlignment.Center
        });

        // В очереди (без монетки)
        var depositBox = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 6
        };
        depositBox.AddChild(new Label
        {
            Text = "В очереди:",
            Modulate = Color.FromHex("#8b949e")
        });
        _depositLabel = new Label
        {
            Modulate = Color.FromHex("#adbac7")
        };
        depositBox.AddChild(_depositLabel);
        infoRow.AddChild(depositBox);

        return panel;
    }

    private Control CreateRoleCard(AntagTokenRoleEntry entry)
    {
        _entitySystems.GetEntitySystem<AntagTokenListingSystem>().TryGetListing(entry.RoleId, out var roleDef);

        var panel = new PanelContainer
        {
            MinSize = new Vector2(290, 0),
            MaxSize = new Vector2(290, 1000),
            VerticalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = entry.Purchased ? PurchasedCardColor : CardBackgroundColor,
                BorderColor = entry.Purchased ? PurchasedBorderColor : CardBorderColor,
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

        var playtimeBlocked = false;
        FormattedMessage? playtimeReason = null;
        ProtoId<AntagPrototype>? linkedAntagId = null;
        var profile = _preferencesManager.Preferences?.SelectedCharacter as HumanoidCharacterProfile;

        if (roleDef?.AntagId != null &&
            _prototypeManager.TryIndex<AntagPrototype>(roleDef.AntagId, out var antagProto))
        {
            linkedAntagId = antagProto.ID;
            if (!_requirements.IsAllowed(antagProto, profile, out playtimeReason))
            {
                playtimeBlocked = true;
                _playtimeBlockedByRole[entry.RoleId] = true;
            }
        }

        if (roleDef != null &&
            !string.IsNullOrWhiteSpace(roleDef.IconPath) &&
            _resourceCache.TryGetResource<TextureResource>(new ResPath(roleDef.IconPath), out var textureResource))
        {
            imageBox.AddChild(new TextureRect
            {
                Texture = textureResource.Texture,
                MinSize = new Vector2(96, 96),
                MaxSize = new Vector2(96, 96),
                Stretch = TextureRect.StretchMode.KeepAspectCentered
            });
        }
        else
        {
            imageBox.AddChild(new Label { Text = "?", Modulate = Color.White });
        }

        root.AddChild(new Label
        {
            Text = roleDef == null ? entry.RoleId : Loc.GetString(roleDef.NameLocKey),
            StyleClasses = { "LabelHeading" },
            Modulate = Color.White,
            HorizontalAlignment = HAlignment.Center,
            MaxWidth = 268
        });

        if (entry.TagLocKey != null)
        {
            root.AddChild(new Label
            {
                Text = Loc.GetString(entry.TagLocKey),
                Modulate = AccentColor,
                HorizontalAlignment = HAlignment.Center,
                MaxWidth = 268
            });
        }

        var statusLocKey = playtimeBlocked
            ? "antag-store-status-not-enough-playtime"
            : entry.StatusLocKey;

        if (statusLocKey != null)
        {
            root.AddChild(new Label
            {
                Text = Loc.GetString(statusLocKey),
                Modulate = entry.Purchased ? PurchasedBorderColor : Color.FromHex("#e5534b"),
                HorizontalAlignment = HAlignment.Center,
                MaxWidth = 268
            });
        }

        root.AddChild(new Control { VerticalExpand = true });

        var buyButton = new Button
        {
            MinSize = new Vector2(268, 40),
            MaxSize = new Vector2(268, 40),
            Disabled = IsButtonDisabled(entry, null),
        };
        ApplyButtonTooltip(buyButton, entry, playtimeReason);

        var roleId = entry.RoleId;
        buyButton.OnPressed += _ => OnPurchasePressed?.Invoke(roleId);

        var buttonContent = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center
        };

        if (playtimeBlocked && playtimeReason != null)
        {
            var lockIcon = new RoleLockIcon(24f);
            lockIcon.SetRequirements(playtimeReason);
            buttonContent.AddChild(lockIcon);
            buyButton.Disabled = true;
        }
        else if (entry.Purchased)
        {
            buttonContent.AddChild(new Label
            {
                Text = "✓",
                Modulate = Color.White,
                StyleClasses = { "LabelHeading" },
                VerticalAlignment = VAlignment.Center
            });
        }
        else if (entry.FreeUnlocks > 0 || entry.FreePurchaseAvailable)
        {
            buttonContent.AddChild(new Label
            {
                Text = Loc.GetString("antag-token-window-button-free"),
                StyleClasses = { "LabelHeading" },
                Modulate = Color.White,
                VerticalAlignment = VAlignment.Center
            });
        }
        else
        {
            buttonContent.AddChild(new Label
            {
                Text = entry.Cost.ToString(),
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

        buyButton.AddChild(buttonContent);
        buyButton.StyleBoxOverride = buyButton.Disabled ? BuyButtonUnavailableStyle : null;
        root.AddChild(buyButton);

        _roleBuyButtons[entry.RoleId] = new RoleBuyButton
        {
            Button = buyButton,
            NormalContent = buttonContent,
            Entry = entry
        };

        return panel;
    }

    private static string FormatCooldownHms(int totalSeconds)
    {
        var t = TimeSpan.FromSeconds(totalSeconds);
        return $"{(int) t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
    }

    private bool IsButtonDisabled(AntagTokenRoleEntry entry, IReadOnlyDictionary<string, int>? localCooldowns)
    {
        var localCd = localCooldowns?.GetValueOrDefault(entry.RoleId) ?? entry.PurchaseCooldownSecondsRemaining;
        if (localCd > 0)
            return true;

        var effectiveAvailable = entry.Available
            || (_lastServerCooldownRemaining.GetValueOrDefault(entry.RoleId) > 0
                && localCd == 0
                && entry.StatusLocKey == null);

        if (entry.Purchased || !effectiveAvailable || !entry.CanAfford)
            return true;

        if (_playtimeBlockedByRole.GetValueOrDefault(entry.RoleId))
            return true;

        if (entry.StatusLocKey == "antag-store-status-has-other-deposit")
            return true;

        return entry.Mode == AntagPurchaseMode.LobbyDeposit && entry.Saturated;
    }

    private static void ApplyButtonTooltip(Button button, AntagTokenRoleEntry entry, FormattedMessage? playtimeReason = null)
    {
        if (playtimeReason != null)
        {
            var tooltip = new Tooltip();
            tooltip.SetMessage(playtimeReason);
            button.TooltipSupplier = _ => tooltip;
            return;
        }

        button.ToolTip = GetPlainButtonTooltip(entry);
    }

    private static string GetPlainButtonTooltip(AntagTokenRoleEntry entry)
    {
        if (entry.StatusLocKey != null)
            return Loc.GetString(entry.StatusLocKey);

        if (entry.FreeUnlocks > 0 || entry.FreePurchaseAvailable)
            return Loc.GetString("antag-token-window-tooltip-free");

        return entry.Mode switch
        {
            AntagPurchaseMode.GhostRule => Loc.GetString("antag-token-window-tooltip-ghost"),
            AntagPurchaseMode.LobbyDeposit => Loc.GetString("antag-token-window-tooltip-deposit"),
            _ => Loc.GetString("antag-token-window-button-unavailable")
        };
    }
}
