// SPDX-FileCopyrightText: 2026 Casha
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT
using System;
using System.Numerics;
using Content.Client._Mini.UserInterface;
using Content.Client.Resources;
using Content.Client.UserInterface;
using Content.Shared._Mini.AntagTokens;
using Content.Shared._Mini.DailyQuests;
using Content.Shared.StatusIcon;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.Utility;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Mini.DailyQuests;

public sealed class DailyQuestCardControl : BoxContainer
{
    public const float RewardQuestCardHeight = 170f;
    public const float CompactQuestCardHeight = CharacterMenuCardStyle.CompactCardHeight;

    private static readonly Color AccentColor = Color.FromHex("#9a8fb5");
    private static readonly Color ClaimReadyColor = Color.FromHex("#6b9e7a");
    private static readonly Color CardBackgroundColor = Color.FromHex("#201e28").WithAlpha(0.8f);
    private static readonly Color CurrentCardColor = Color.FromHex("#2c2a3a").WithAlpha(0.95f);
    private static readonly Color PurchasedCardColor = Color.FromHex("#2d2a38").WithAlpha(0.85f);

    private static readonly StyleBoxFlat ReplaceButtonStyle = new()
    {
        BackgroundColor = Color.FromHex("#121824"),
        BorderColor = AccentColor.WithAlpha(0.95f),
        BorderThickness = new Thickness(1),
        ContentMarginLeftOverride = 10,
        ContentMarginRightOverride = 10,
        ContentMarginTopOverride = 3,
        ContentMarginBottomOverride = 3,
    };

    private readonly IResourceCache _resourceCache;
    private readonly IPrototypeManager _prototypeManager;
    private readonly Texture _antagCoinTexture;
    private readonly PixelTiledProgressBar _progressBar;
    private readonly Label _progressLabel;
    private readonly Label _statusLabel;
    private readonly bool _compact;
    private string? _boundQuestId;
    private bool _boundClaimed;
    private bool _boundShowReplace;
    private int _boundReplaceCost;
    private PanelContainer? _cardPanel;
    private StyleBoxFlat? _cardStyle;
    private Button? _replaceButton;
    private Action? _replaceHandler;

    public string? BoundQuestId => _boundQuestId;

    public DailyQuestCardControl(bool compact = false)
    {
        _compact = compact;

        IoCManager.InjectDependencies(this);
        _resourceCache = IoCManager.Resolve<IResourceCache>();
        _prototypeManager = IoCManager.Resolve<IPrototypeManager>();
        _antagCoinTexture = _resourceCache.TryGetResource<TextureResource>(
                new ResPath(AntagTokenCatalog.CurrencyIconPath), out var antagRes)
            ? antagRes.Texture
            : _resourceCache.GetTexture("/Textures/_Mini/Interface/Coin.png");

        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = compact ? 4 : 6;
        HorizontalExpand = true;

        _progressBar = new PixelTiledProgressBar();
        _progressLabel = new Label { Modulate = CharacterMenuCardStyle.ProgressTextColor, HorizontalAlignment = HAlignment.Right };
        _statusLabel = new Label { HorizontalAlignment = HAlignment.Center, Modulate = Color.FromHex("#e8eef8") };
    }

    public void SetReplaceHandler(Action? handler)
    {
        _replaceHandler = handler;
        AttachReplaceHandler();
    }

    private void AttachReplaceHandler()
    {
        if (_replaceButton == null)
            return;

        _replaceButton.OnPressed -= OnReplaceButtonPressed;
        _replaceButton.OnPressed += OnReplaceButtonPressed;
    }

    private void OnReplaceButtonPressed(BaseButton.ButtonEventArgs _)
    {
        _replaceHandler?.Invoke();
    }

    public void SetInteractable(bool interactable)
    {
        if (_replaceButton == null)
            return;

        _replaceButton.Disabled = !interactable;
        _replaceButton.Modulate = interactable
            ? Color.FromHex("#eef2fb")
            : Color.FromHex("#8b93a8");
    }

    public void SetRerollPulse(float strength)
    {
        if (_cardPanel == null || _cardStyle == null)
            return;

        var glow = Math.Clamp(strength, 0f, 1f);
        _cardStyle.BorderColor = Color.InterpolateBetween(AccentColor.WithAlpha(0.35f), ClaimReadyColor, glow);
        _cardStyle.BackgroundColor = Color.InterpolateBetween(CurrentCardColor, Color.FromHex("#353347"), glow * 0.6f);
        _cardPanel.PanelOverride = _cardStyle;
        _cardPanel.Modulate = Color.White.WithAlpha(0.82f + 0.18f * glow);
    }

    public void SetQuest(DailyQuestEntry quest, float smoothTimeExtra = 0f, bool forceRebuild = false)
    {
        var showReplace = !_compact && CanShowReplace(quest);

        if (!forceRebuild
            && _boundQuestId == quest.QuestId
            && ChildCount > 0
            && _boundClaimed == quest.IsClaimed
            && _boundShowReplace == showReplace
            && _boundReplaceCost == quest.ReplaceCostCoins)
        {
            UpdateProgressDisplay(quest, smoothTimeExtra);
            UpdateStatusLabel(quest);
            return;
        }

        _boundQuestId = quest.QuestId;
        _boundClaimed = quest.IsClaimed;
        _boundShowReplace = showReplace;
        _boundReplaceCost = quest.ReplaceCostCoins;
        _cardPanel = null;
        _cardStyle = null;
        _replaceButton = null;
        DetachReusedControls();
        RemoveAllChildren();

        var isClaimed = quest.IsClaimed;
        var isComplete = quest.IsCompleted && !isClaimed;

        if (!_compact)
        {
            _cardStyle = new StyleBoxFlat
            {
                BackgroundColor = isClaimed ? PurchasedCardColor : CurrentCardColor,
                BorderColor = isComplete || isClaimed
                    ? ClaimReadyColor.WithAlpha(0.5f)
                    : AccentColor.WithAlpha(0.35f),
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 8,
                ContentMarginTopOverride = 4,
                ContentMarginRightOverride = 8,
                ContentMarginBottomOverride = 3
            };

            _cardPanel = new PanelContainer
            {
                HorizontalExpand = true,
                VerticalExpand = false,
                MinSize = new Vector2(0, RewardQuestCardHeight),
                MaxSize = new Vector2(float.PositiveInfinity, RewardQuestCardHeight),
                PanelOverride = _cardStyle
            };

            var column = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                SeparationOverride = 2,
                HorizontalExpand = true,
                VerticalExpand = false,
            };
            _cardPanel.AddChild(column);
            AddChild(_cardPanel);
            PopulateContent(column, quest, isClaimed, showReplace);
            return;
        }

        var compactPanel = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = false,
            RectClipContent = true,
            MinSize = new Vector2(0, CompactQuestCardHeight),
            Margin = new Thickness(0, 0, 0, 0),
            PanelOverride = CharacterMenuCardStyle.CreateCompactPanelStyle(isComplete || isClaimed),
        };

        var compactColumn = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 4,
            HorizontalExpand = true
        };
        compactPanel.AddChild(compactColumn);
        AddChild(compactPanel);
        PopulateContent(compactColumn, quest, isClaimed, showReplace: false);
    }

    private void PopulateContent(BoxContainer column, DailyQuestEntry quest, bool isClaimed, bool showReplace)
    {
        if (!_compact)
        {
            var topRow = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                SeparationOverride = 8,
                VerticalAlignment = VAlignment.Center,
            };
            column.AddChild(topRow);

            var questIcon = TryGetQuestIcon(quest);
            if (questIcon != null)
            {
                topRow.AddChild(new TextureRect
                {
                    Texture = questIcon,
                    MinSize = new Vector2(22, 22),
                    TextureScale = new Vector2(1.15f),
                    Stretch = TextureRect.StretchMode.KeepAspectCentered,
                    VerticalAlignment = VAlignment.Center
                });
            }

            var title = new MarqueeLabel
            {
                Text = quest.Title,
                Modulate = Color.White,
                HorizontalExpand = true,
                MinSize = new Vector2(0, 18),
                VerticalAlignment = VAlignment.Center,
            };
            title.SetStyleClass("LabelHeading");
            topRow.AddChild(title);

            var rarityLabel = CreateRarityLabel(quest);
            topRow.AddChild(rarityLabel);

            var rewardRow = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                SeparationOverride = 2,
                VerticalAlignment = VAlignment.Center,
            };
            rewardRow.AddChild(new Label
            {
                Text = $"+{quest.RewardCoins}",
                Modulate = AccentColor,
                VerticalAlignment = VAlignment.Center,
            });
            rewardRow.AddChild(new TextureRect
            {
                Texture = _antagCoinTexture,
                MinSize = new Vector2(18, 18),
                TextureScale = new Vector2(0.38f, 0.38f),
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                VerticalAlignment = VAlignment.Center,
            });
            topRow.AddChild(rewardRow);

            column.AddChild(new MarqueeLabel
            {
                Text = quest.Description,
                Modulate = Color.FromHex("#c5d3ed"),
                HorizontalExpand = true,
                MinSize = new Vector2(0, 16),
            });

            if (!string.IsNullOrWhiteSpace(quest.RoleHint))
            {
                column.AddChild(new MarqueeLabel
                {
                    Text = Loc.GetString("daily-quest-role-hint", ("role", quest.RoleHint)),
                    Modulate = Color.FromHex("#9fb4d8"),
                    HorizontalExpand = true,
                    MinSize = new Vector2(0, 16),
                });
            }
        }
        else
        {
            var header = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                SeparationOverride = 8,
                VerticalAlignment = VAlignment.Center,
            };
            column.AddChild(header);

            var questIcon = TryGetQuestIcon(quest);
            if (questIcon != null)
            {
                header.AddChild(new TextureRect
                {
                    Texture = questIcon,
                    MinSize = new Vector2(CharacterMenuCardStyle.IconSize, CharacterMenuCardStyle.IconSize),
                    TextureScale = new Vector2(1f),
                    Stretch = TextureRect.StretchMode.KeepAspectCentered,
                    VerticalAlignment = VAlignment.Center
                });
            }

            header.AddChild(CharacterMenuCardStyle.CreateTitleMarqueeHost(quest.Title));
            header.AddChild(CreateRarityLabel(quest));

            column.AddChild(CharacterMenuCardStyle.CreateBodyMarqueeHost(quest.Description));
        }

        UpdateProgressDisplay(quest, 0f);
        column.AddChild(_progressBar);
        column.AddChild(_progressLabel);

        if (!_compact)
        {
            _statusLabel.HorizontalAlignment = HAlignment.Center;
            column.AddChild(_statusLabel);
            UpdateStatusLabel(quest);

            if (showReplace)
            {
                var replaceRow = new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    HorizontalExpand = true,
                    HorizontalAlignment = HAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0),
                };
                _replaceButton = new Button
                {
                    Text = quest.ReplaceCostCoins > 0 ? null : Loc.GetString("daily-quest-replace"),
                    MinSize = new Vector2(168, 26),
                    HorizontalAlignment = HAlignment.Center,
                    StyleClasses = { "OpenBoth" },
                    StyleBoxOverride = ReplaceButtonStyle,
                    Modulate = Color.FromHex("#eef2fb"),
                };
                _replaceButton.MouseFilter = MouseFilterMode.Stop;

                if (quest.ReplaceCostCoins > 0)
                {
                    // Платная замена: рядом с надписью показываем цену числом и монеткой.
                    var priceContent = new BoxContainer
                    {
                        Orientation = LayoutOrientation.Horizontal,
                        SeparationOverride = 6,
                        HorizontalAlignment = HAlignment.Center,
                        VerticalAlignment = VAlignment.Center,
                    };
                    priceContent.AddChild(new Label
                    {
                        Text = Loc.GetString("daily-quest-replace"),
                        StyleClasses = { "button" },
                        VerticalAlignment = VAlignment.Center,
                    });
                    priceContent.AddChild(new Label
                    {
                        Text = quest.ReplaceCostCoins.ToString(),
                        StyleClasses = { "button" },
                        VerticalAlignment = VAlignment.Center,
                    });
                    priceContent.AddChild(new TextureRect
                    {
                        Texture = _antagCoinTexture,
                        MinSize = new Vector2(18, 18),
                        TextureScale = new Vector2(0.38f, 0.38f),
                        Stretch = TextureRect.StretchMode.KeepAspectCentered,
                        VerticalAlignment = VAlignment.Center,
                    });
                    _replaceButton.AddChild(new CenterContainer { Children = { priceContent } });
                }
                replaceRow.AddChild(_replaceButton);
                AttachReplaceHandler();
                column.AddChild(replaceRow);
            }
        }
    }

    private static Label CreateRarityLabel(DailyQuestEntry quest)
    {
        var label = new Label
        {
            Text = Loc.GetString(quest.Rarity.GetLocId()),
            Modulate = quest.Rarity.GetColor(),
            VerticalAlignment = VAlignment.Center,
            HorizontalAlignment = HAlignment.Right,
        };
        label.StyleClasses.Add("LabelSmall");
        return label;
    }

    private void DetachReusedControls()
    {
        _progressBar.Orphan();
        _progressLabel.Orphan();
        _statusLabel.Orphan();
    }

    private static bool CanShowReplace(DailyQuestEntry quest)
    {
        return quest.CanReplace;
    }

    private void UpdateStatusLabel(DailyQuestEntry quest)
    {
        if (_compact)
            return;

        var nextQuestText = TryFormatNextQuestTimer(quest);

        if (quest.IsClaimed)
        {
            var claimedText = Loc.GetString("daily-quest-status-claimed", ("amount", quest.RewardCoins));
            _statusLabel.Text = nextQuestText == null
                ? claimedText
                : Loc.GetString("daily-quest-status-claimed-timer", ("status", claimedText), ("time", nextQuestText));
            _statusLabel.Modulate = ClaimReadyColor;
            return;
        }

        if (quest.IsCompleted)
        {
            var completeText = Loc.GetString("daily-quest-status-complete");
            _statusLabel.Text = nextQuestText == null
                ? completeText
                : Loc.GetString("daily-quest-status-complete-timer", ("status", completeText), ("time", nextQuestText));
            _statusLabel.Modulate = ClaimReadyColor;
            return;
        }

        _statusLabel.Text = Loc.GetString("daily-quest-status-active");
        _statusLabel.Modulate = Color.FromHex("#e8eef8");
    }

    private static string? TryFormatNextQuestTimer(DailyQuestEntry quest)
    {
        if (quest.NextQuestResetUtc is not { } resetUtc)
            return null;

        var remaining = resetUtc - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
            return null;

        return FormatDuration((float)remaining.TotalSeconds);
    }

    private void UpdateProgressDisplay(DailyQuestEntry quest, float smoothTimeExtra = 0f)
    {
        if (_compact && (quest.IsClaimed || quest.IsCompleted))
        {
            var nextQuestText = TryFormatNextQuestTimer(quest);
            _progressBar.Value = 1f;
            if (nextQuestText == null)
            {
                _progressLabel.Text = quest.IsClaimed
                    ? Loc.GetString("daily-quest-status-claimed", ("amount", quest.RewardCoins))
                    : Loc.GetString("daily-quest-status-complete");
            }
            else
            {
                _progressLabel.Text = Loc.GetString("daily-quest-status-next-quest", ("time", nextQuestText));
            }

            return;
        }

        var current = GetDisplayProgress(quest, smoothTimeExtra);
        var progressRatio = quest.TargetProgress <= 0
            ? 1f
            : Math.Clamp(current / quest.TargetProgress, 0f, 1f);

        _progressBar.Value = progressRatio;
        _progressLabel.Text = FormatProgressLabel(quest, smoothTimeExtra);
    }

    private static float GetDisplayProgress(DailyQuestEntry quest, float smoothTimeExtra)
    {
        if (!quest.IsTimeBased || quest.IsCompleted || quest.IsClaimed)
            return quest.CurrentProgress;

        return Math.Min(quest.CurrentProgress + smoothTimeExtra, quest.TargetProgress);
    }

    private static string FormatProgressLabel(DailyQuestEntry quest, float smoothTimeExtra = 0f)
    {
        if (quest.IsTimeBased)
        {
            var current = GetDisplayProgress(quest, smoothTimeExtra);
            return $"{FormatDuration(current)} / {FormatDuration(quest.TargetProgress)}";
        }

        return Loc.GetString("daily-quest-progress",
            ("current", quest.CurrentProgress),
            ("target", quest.TargetProgress));
    }

    private static string FormatDuration(float totalSeconds)
    {
        if (totalSeconds <= 0f)
            return "00:00";

        if (totalSeconds >= 3600f)
        {
            var span = TimeSpan.FromSeconds(totalSeconds);
            return $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";
        }

        var minutes = (int)(totalSeconds / 60f);
        var seconds = (int)(totalSeconds % 60);
        return $"{minutes:00}:{seconds:00}";
    }

    private Texture? TryGetQuestIcon(DailyQuestEntry quest)
    {
        if (!string.IsNullOrWhiteSpace(quest.IconId) &&
            _prototypeManager.TryIndex<JobIconPrototype>(quest.IconId, out var jobIcon))
        {
            return jobIcon.Icon.Frame0();
        }

        if (!string.IsNullOrWhiteSpace(quest.IconSprite))
        {
            if (!string.IsNullOrWhiteSpace(quest.IconState))
            {
                var specifier = new SpriteSpecifier.Rsi(new ResPath(quest.IconSprite), quest.IconState);
                return specifier.Frame0();
            }

            if (_resourceCache.TryGetResource<TextureResource>(new ResPath(quest.IconSprite), out var texture))
                return texture.Texture;
        }

        return null;
    }
}
