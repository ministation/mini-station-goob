// SPDX-FileCopyrightText: 2026 Casha
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT
using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client._Mini.DailyRewards;
using Content.Client._Mini.DailyQuests;
using Content.Client.Resources;
using Content.Client.UserInterface;
using Content.Shared._Mini.AntagTokens;
using Content.Shared._Mini.DailyQuests;
using Content.Shared._Mini.DailyRewards;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._Mini.DailyRewards;

public sealed class DailyRewardWindow : DefaultWindow
{
    private const string ClockIconPath = "/Textures/_Mini/Interface/Clock.png";
    private const string CoinIconPath = "/Textures/_Mini/Interface/Coin.png";
    private const float RewardCardWidth = 132f;
    private static readonly string AntagCoinIconPath = AntagTokenCatalog.CurrencyIconPath;

    private static readonly Color WindowBackgroundColor = Color.FromHex("#0f1115");
    private static readonly Color HeroPanelColor = Color.FromHex("#1c1a24").WithAlpha(0.9f);
    private static readonly Color AccentColor = Color.FromHex("#9a8fb5");
    private static readonly Color ClaimReadyColor = Color.FromHex("#6b9e7a");
    private static readonly Color TimePanelColor = Color.FromHex("#4a7a5c").WithAlpha(0.3f);
    private static readonly Color PurchasedBorderColor = Color.FromHex("#4a7a5c").WithAlpha(0.15f);
    private static readonly Color CardBackgroundColor = Color.FromHex("#201e28").WithAlpha(0.8f);
    private static readonly Color CardBorderColor = Color.Transparent;
    private static readonly Color PurchasedCardColor = Color.FromHex("#2d2a38").WithAlpha(0.85f);
    private static readonly Color CurrentCardColor = Color.FromHex("#2c2a3a").WithAlpha(0.95f);
    private static readonly Color CurrentBorderColor = Color.Transparent;

    private static readonly Color FutureCardColor = CardBackgroundColor;
    private static readonly Color FutureBorderColor = CardBorderColor;
    private static readonly Color ClaimedCardColor = PurchasedCardColor;
    private static readonly Color ClaimedBorderColor = PurchasedBorderColor;

    public event Action? OnClaimPressed;

    private readonly DailyRewardUiSystem _uiSystem;
    private readonly IResourceCache _resourceCache;
    private readonly Label _streakValueLabel;
    private readonly Label _activeProgressLabel;
    private readonly Label _cooldownLabel;
    private readonly Label _expiryLabel;
    private readonly PixelTiledProgressBar _activeProgressBar;
    private readonly Button _claimButton;
    private readonly BoxContainer _rewardTrack;
    private readonly Texture _clockTexture;
    private readonly Texture _coinTexture;
    private readonly Texture _antagCoinTexture;
    private readonly GridContainer _questTrack;
    private readonly Label _questSectionLabel;
    private readonly List<DailyQuestCardControl> _questCards = new();
    private readonly List<PanelContainer> _questSlots = new();
    private float _questTimeSmooth;
    private float _replaceErrorTimer;
    private float _replacePendingTimer;
    private string? _replaceError;
    private string? _pendingReplaceQuestId;
    private int _pendingReplaceSlotIndex = -1;
    private DailyRewardUpdateMessage? _state;
    private Label? _currentRewardTimerLabel;
    private TextureRect? _currentRewardTimerIcon;

    public DailyRewardWindow()
    {
        IoCManager.InjectDependencies(this);
        _uiSystem = IoCManager.Resolve<IEntityManager>().System<DailyRewardUiSystem>();
        _resourceCache = IoCManager.Resolve<IResourceCache>();
        _clockTexture = _resourceCache.GetTexture(ClockIconPath);
        _coinTexture = _resourceCache.GetTexture(CoinIconPath);
        _antagCoinTexture = _resourceCache.TryGetResource<TextureResource>(new ResPath(AntagCoinIconPath), out var antagRes)
            ? antagRes.Texture
            : _resourceCache.GetTexture(CoinIconPath);

        Title = Loc.GetString("daily-reward-window-title");
        MinSize = new Vector2(1080, 760);
        SetSize = new Vector2(1120, 780);

        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(14),
            VerticalExpand = true,
            HorizontalExpand = true,
        };

        var backdrop = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = WindowBackgroundColor },
            VerticalExpand = true,
            HorizontalExpand = true,
        };
        Contents.AddChild(backdrop);
        backdrop.AddChild(root);

        root.AddChild(BuildHeroSection(out _streakValueLabel, out _activeProgressLabel, out _cooldownLabel, out _expiryLabel, out _activeProgressBar, out _claimButton));

        _questSectionLabel = new Label
        {
            Text = Loc.GetString("daily-quest-section-title"),
            StyleClasses = { "LabelHeading" },
            Margin = new Thickness(2, 0, 0, 0)
        };
        root.AddChild(_questSectionLabel);

        var questPanel = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = false,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#141820").WithAlpha(0.9f),
                BorderColor = Color.FromHex("#31415f"),
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 8,
                ContentMarginTopOverride = 6,
                ContentMarginRightOverride = 8,
                ContentMarginBottomOverride = 6,
            }
        };
        _questTrack = new GridContainer
        {
            Columns = 2,
            HorizontalExpand = true,
            VerticalExpand = false,
            MinSize = new Vector2(0, DailyQuestCardControl.RewardQuestCardHeight + 4),
        };
        questPanel.AddChild(_questTrack);
        root.AddChild(questPanel);

        root.AddChild(new Label
        {
            Text = Loc.GetString("daily-reward-window-road-title"),
            StyleClasses = { "LabelHeading" },
            Margin = new Thickness(2, 0, 0, 0)
        });

        var rewardsPanel = new PanelContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            MinSize = new Vector2(0, 200),
            MaxSize = new Vector2(float.PositiveInfinity, 240),
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#0f1623"),
                BorderColor = Color.FromHex("#31415f"),
                BorderThickness = new Thickness(2),
                ContentMarginLeftOverride = 12,
                ContentMarginTopOverride = 8,
                ContentMarginRightOverride = 12,
                ContentMarginBottomOverride = 8
            }
        };
        root.AddChild(rewardsPanel);

        var scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = true,
            VScrollEnabled = false,
            MinSize = new Vector2(0, 170),
            MaxSize = new Vector2(float.PositiveInfinity, 220),
        };
        rewardsPanel.AddChild(scroll);

        _rewardTrack = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 0,
            VerticalAlignment = VAlignment.Center,
            MinSize = new Vector2(0, 130),
        };
        scroll.AddChild(_rewardTrack);

        _claimButton.OnPressed += _ => OnClaimPressed?.Invoke();
    }

    public void UpdateState(DailyRewardUpdateMessage state)
    {
        TryFinishQuestReplace(state);
        var rebuildQuestSection = NeedsQuestSectionRebuild(_state?.DailyQuests, state.DailyQuests);
        _questTimeSmooth = 0f;
        _state = state;
        RefreshState(rebuildQuestSection);
    }

    private static bool NeedsQuestSectionRebuild(
        IReadOnlyList<DailyQuestEntry>? previous,
        IReadOnlyList<DailyQuestEntry>? next)
    {
        if (previous == null || next == null)
            return previous != next;

        if (previous.Count != next.Count)
            return true;

        for (var i = 0; i < next.Count; i++)
        {
            var a = previous[i];
            var b = next[i];
            if (a.QuestId != b.QuestId
                || a.IsClaimed != b.IsClaimed
                || a.IsCompleted != b.IsCompleted
                || a.CanReplace != b.CanReplace)
            {
                return true;
            }
        }

        return false;
    }

    private void TryFinishQuestReplace(DailyRewardUpdateMessage state)
    {
        if (string.IsNullOrWhiteSpace(_pendingReplaceQuestId) || _pendingReplaceSlotIndex < 0)
            return;

        if (state.DailyQuests == null || _pendingReplaceSlotIndex >= state.DailyQuests.Count)
            return;

        var quest = state.DailyQuests[_pendingReplaceSlotIndex];
        if (quest.QuestId != _pendingReplaceQuestId || !ShouldShowReplaceButton(quest))
            ClearQuestReplacePending();
    }

    public void AdvanceTimers(float frameTime)
    {
        if (_state == null)
            return;

        var step = TimeSpan.FromSeconds(frameTime);
        var timeUntilExpiration = MaxZero(_state.TimeUntilExpiration - step);
        var timeUntilNextClaim = MaxZero(_state.TimeUntilNextClaim - step);
        var currentActiveTime = _state.CurrentActiveTime;

        if (_state.IsTrackingActiveTime && currentActiveTime < _state.RequiredActiveTime)
            currentActiveTime = Min(currentActiveTime + step, _state.RequiredActiveTime);

        var canClaim = currentActiveTime >= _state.RequiredActiveTime && timeUntilNextClaim == TimeSpan.Zero;

        _state = new DailyRewardUpdateMessage(
            _state.CurrentStreak,
            _state.NextRewardDay,
            canClaim,
            _state.IsTrackingActiveTime,
            _state.HasLastClaim,
            timeUntilExpiration,
            timeUntilNextClaim,
            currentActiveTime,
            _state.RequiredActiveTime,
            _state.Rewards,
            _state.OnlineElapsed,
            _state.OnlineGrantedThresholds,
            _state.DailyQuests);

        UpdateActiveTimerUi();
        UpdateCurrentRewardTimerUi();
        _questTimeSmooth += frameTime;

        if (_replaceErrorTimer > 0f)
        {
            _replaceErrorTimer -= frameTime;
            if (_replaceErrorTimer <= 0f)
            {
                _replaceError = null;
                UpdateQuestSectionLabel();
            }
        }

        if (_replacePendingTimer > 0f)
        {
            _replacePendingTimer -= frameTime;
            if (_replacePendingTimer <= 0f)
                ClearQuestReplacePending();
        }

        UpdateQuestCardsSmooth();
    }

    private void UpdateQuestSectionLabel()
    {
        if (!string.IsNullOrWhiteSpace(_pendingReplaceQuestId))
        {
            _questSectionLabel.Text = Loc.GetString("daily-quest-replace-pending");
            _questSectionLabel.Modulate = Color.FromHex("#c5d3ed");
            return;
        }

        if (_replaceErrorTimer > 0f && !string.IsNullOrWhiteSpace(_replaceError))
        {
            _questSectionLabel.Text = _replaceError;
            _questSectionLabel.Modulate = Color.FromHex("#f0a0a0");
            return;
        }

        if (_state?.DailyQuests == null || _state.DailyQuests.Count == 0)
        {
            _questSectionLabel.Text = Loc.GetString("daily-quest-section-title");
            _questSectionLabel.Modulate = Color.White;
            return;
        }

        var done = 0;
        foreach (var quest in _state.DailyQuests)
        {
            if (quest.IsClaimed)
                done++;
        }

        _questSectionLabel.Text = Loc.GetString("daily-quest-section-summary",
            ("done", done),
            ("total", _state.DailyQuests.Count));
        _questSectionLabel.Modulate = Color.White;
    }

    public void BeginQuestReplace(string questId, int slotIndex = -1)
    {
        _pendingReplaceQuestId = questId;
        _pendingReplaceSlotIndex = slotIndex >= 0
            ? slotIndex
            : FindQuestSlotIndex(questId);

        foreach (var card in _questCards)
            card.SetInteractable(false);

        _replaceError = null;
        _replaceErrorTimer = 0f;
        _replacePendingTimer = 5f;
        _questSectionLabel.Text = Loc.GetString("daily-quest-replace-pending");
        _questSectionLabel.Modulate = Color.FromHex("#c5d3ed");
    }

    public void ClearQuestReplacePending()
    {
        _replacePendingTimer = 0f;
        _pendingReplaceQuestId = null;
        _pendingReplaceSlotIndex = -1;
        foreach (var card in _questCards)
            card.SetInteractable(true);

        UpdateQuestSectionLabel();
    }

    public void ShowQuestReplaceError(string message)
    {
        _replaceError = message;
        _replaceErrorTimer = 4f;
        UpdateQuestSectionLabel();
    }

    private int FindQuestSlotIndex(string questId)
    {
        for (var i = 0; i < _questCards.Count; i++)
        {
            if (_questCards[i].BoundQuestId == questId)
                return i;
        }

        return -1;
    }

    private void OnQuestReplaceClicked(int slotIndex)
    {
        if (_state?.DailyQuests == null || slotIndex < 0 || slotIndex >= _state.DailyQuests.Count)
            return;

        var quest = _state.DailyQuests[slotIndex];
        if (!ShouldShowReplaceButton(quest))
            return;

        BeginQuestReplace(quest.QuestId, slotIndex);
        _uiSystem.SendReplaceRequest(quest.QuestId, slotIndex);
    }

    private static bool ShouldShowReplaceButton(DailyQuestEntry quest)
    {
        return quest.CanReplace;
    }

    private void UpdateActiveTimerUi()
    {
        if (_state == null)
            return;

        var state = _state;

        var progressRatio = state.RequiredActiveTime <= TimeSpan.Zero
            ? 1f
            : Math.Clamp((float)(state.CurrentActiveTime.TotalSeconds / state.RequiredActiveTime.TotalSeconds), 0f, 1f);

        _activeProgressBar.Value = progressRatio;

        _activeProgressLabel.Text = progressRatio >= 1f
            ? Loc.GetString("daily-reward-window-active-ready")
            : Loc.GetString("daily-reward-window-active-progress",
                ("current", FormatActiveProgress(state.CurrentActiveTime)),
                ("required", FormatActiveProgress(state.RequiredActiveTime)));

        _cooldownLabel.Text = GetAvailabilityText(state);
        _expiryLabel.Text = state.HasLastClaim
            ? Loc.GetString("daily-reward-window-expiry", ("time", FormatCooldown(state.TimeUntilExpiration)))
            : Loc.GetString("daily-reward-window-expiry-idle");

        _claimButton.Disabled = !state.CanClaim;
        _claimButton.Text = Loc.GetString(state.CanClaim
            ? "daily-reward-window-claim-ready"
            : "daily-reward-window-claim-locked");

        UpdateCurrentRewardTimerUi();
    }

    private void UpdateCurrentRewardTimerUi()
    {
        if (_currentRewardTimerLabel == null || _state == null)
            return;

        var ready = _state.CanClaim;
        if (_currentRewardTimerIcon != null)
            _currentRewardTimerIcon.Visible = !ready;

        _currentRewardTimerLabel.Text = ready
            ? Loc.GetString("daily-reward-card-timer-ready")
            : FormatCooldown(_state.TimeUntilNextClaim);
    }

    private void RefreshState(bool rebuildQuestSection)
    {
        if (_state == null)
            return;

        var state = _state;

        _streakValueLabel.Text = Loc.GetString("daily-reward-window-streak-value",
            ("current", state.CurrentStreak),
            ("max", state.Rewards.Count));

        UpdateActiveTimerUi();

        if (rebuildQuestSection)
            RefreshQuestSection();
        else
            UpdateQuestCardsFromState();

        _rewardTrack.RemoveAllChildren();
        _currentRewardTimerLabel = null;
        _currentRewardTimerIcon = null;
        for (var i = 0; i < state.Rewards.Count; i++)
        {
            var reward = state.Rewards[i];
            _rewardTrack.AddChild(CreateRewardColumn(reward, state));

            if (i < state.Rewards.Count - 1)
                _rewardTrack.AddChild(CreateConnector(reward, state.Rewards[i + 1]));
        }
    }

    private Control CreateRewardColumn(DailyRewardEntry reward, DailyRewardUpdateMessage state)
    {
        var column = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 4,
            MinSize = new Vector2(RewardCardWidth + 6, 0)
        };

        column.AddChild(CreateRewardCard(reward));

        if (reward.IsCurrent)
        {
            var ready = state.CanClaim;

            var timerLabel = new Label
            {
                Text = ready
                    ? Loc.GetString("daily-reward-card-timer-ready")
                    : FormatCooldown(state.TimeUntilNextClaim),
                StyleClasses = { "LabelSmall" },
                Modulate = ClaimReadyColor,
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center
            };
            _currentRewardTimerLabel = timerLabel;

            var timerRow = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                SeparationOverride = 4,
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
                HorizontalExpand = true
            };

            var clockIcon = new TextureRect
            {
                Texture = _clockTexture,
                MinSize = new Vector2(12, 12),
                TextureScale = new Vector2(0.35f, 0.35f),
                Stretch = TextureRect.StretchMode.KeepCentered,
                VerticalAlignment = VAlignment.Center,
                Visible = !ready
            };
            _currentRewardTimerIcon = clockIcon;
            timerRow.AddChild(clockIcon);
            timerRow.AddChild(timerLabel);

            var timerPanel = new PanelContainer
            {
                Margin = new Thickness(0, 2, 6, 0),
                MinSize = new Vector2(RewardCardWidth, 22),
                MaxSize = new Vector2(RewardCardWidth, 22),
                PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = TimePanelColor.WithAlpha(0.9f),
                    BorderColor = Color.Transparent,
                    BorderThickness = new Thickness(0),
                    ContentMarginLeftOverride = 8,
                    ContentMarginTopOverride = 2,
                    ContentMarginRightOverride = 8,
                    ContentMarginBottomOverride = 2
                }
            };
            timerPanel.AddChild(timerRow);

            column.AddChild(timerPanel);
        }

        return column;
    }

    private void RefreshQuestSection()
    {
        if (_state == null)
            return;

        _questTrack.RemoveAllChildren();

        if (_state.DailyQuests == null || _state.DailyQuests.Count == 0)
        {
            _questCards.Clear();
            _questSlots.Clear();
            _questSectionLabel.Text = Loc.GetString("daily-quest-section-title");
            _questTrack.AddChild(new Label
            {
                Text = Loc.GetString("daily-quest-empty"),
                Modulate = Color.FromHex("#9fb4d8"),
                HorizontalExpand = true,
                HorizontalAlignment = HAlignment.Center
            });
            return;
        }

        UpdateQuestSectionLabel();

        EnsureQuestSlots(_state.DailyQuests.Count);

        for (var i = 0; i < _state.DailyQuests.Count; i++)
        {
            var quest = _state.DailyQuests[i];
            _questCards[i].SetQuest(quest, _questTimeSmooth);
            _questSlots[i].HorizontalExpand = true;
            _questSlots[i].VerticalExpand = false;
            _questTrack.AddChild(_questSlots[i]);
        }
    }

    private void EnsureQuestSlots(int count)
    {
        while (_questSlots.Count < count)
        {
            var slotIndex = _questSlots.Count;
            var card = new DailyQuestCardControl
            {
                HorizontalExpand = true,
                VerticalExpand = false,
            };
            card.SetReplaceHandler(() => OnQuestReplaceClicked(slotIndex));
            _questCards.Add(card);

            var slot = new PanelContainer
            {
                HorizontalExpand = true,
                VerticalExpand = false,
                MinSize = new Vector2(0, DailyQuestCardControl.RewardQuestCardHeight),
            };
            slot.AddChild(card);
            _questSlots.Add(slot);
        }
    }

    private void UpdateQuestCardsFromState()
    {
        if (_state?.DailyQuests == null)
            return;

        var count = Math.Min(_questCards.Count, _state.DailyQuests.Count);
        for (var i = 0; i < count; i++)
            _questCards[i].SetQuest(_state.DailyQuests[i], _questTimeSmooth);

        UpdateQuestSectionLabel();
    }

    private void UpdateQuestCardsSmooth()
    {
        if (_state?.DailyQuests == null)
            return;

        var count = Math.Min(_questCards.Count, _state.DailyQuests.Count);
        for (var i = 0; i < count; i++)
            _questCards[i].SetQuest(_state.DailyQuests[i], _questTimeSmooth);
    }

    private Control BuildHeroSection(
        out Label streakValueLabel,
        out Label activeProgressLabel,
        out Label cooldownLabel,
        out Label expiryLabel,
        out PixelTiledProgressBar activeProgressBar,
        out Button claimButton)
    {
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = HeroPanelColor,
                BorderColor = AccentColor,
                BorderThickness = new Thickness(2),
                ContentMarginLeftOverride = 14,
                ContentMarginTopOverride = 14,
                ContentMarginRightOverride = 14,
                ContentMarginBottomOverride = 14
            }
        };

        var content = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 10
        };
        panel.AddChild(content);

        var headerRow = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 12
        };
        content.AddChild(headerRow);

        var titleCol = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 4,
            HorizontalExpand = true
        };
        headerRow.AddChild(titleCol);

        titleCol.AddChild(new Label
        {
            Text = Loc.GetString("daily-reward-window-title"),
            StyleClasses = { "LabelHeading" },
            Modulate = Color.White
        });

        titleCol.AddChild(new Label
        {
            Text = Loc.GetString("daily-reward-window-subtitle"),
            Modulate = Color.FromHex("#c5d3ed")
        });

        var claimPanel = new PanelContainer
        {
            MinSize = new Vector2(190, 72),
            MaxSize = new Vector2(190, 72),
            VerticalAlignment = VAlignment.Top,
            HorizontalAlignment = HAlignment.Right,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#0f1725"),
                BorderColor = Color.FromHex("#455674"),
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 10,
                ContentMarginTopOverride = 8,
                ContentMarginRightOverride = 10,
                ContentMarginBottomOverride = 8
            }
        };
        headerRow.AddChild(claimPanel);

        var claimBox = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center
        };
        claimPanel.AddChild(claimBox);

        claimBox.AddChild(new Label
        {
            Text = Loc.GetString("daily-reward-window-claim-panel-title"),
            StyleClasses = { "LabelHeading" },
            Modulate = Color.White,
            HorizontalAlignment = HAlignment.Center
        });

        claimButton = new Button
        {
            Text = Loc.GetString("daily-reward-window-claim"),
            MinSize = new Vector2(168, 36),
            HorizontalExpand = true,
            Modulate = Color.White
        };
        claimBox.AddChild(claimButton);

        var streakPanel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#0f1725"),
                BorderColor = Color.FromHex("#455674"),
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 10,
                ContentMarginTopOverride = 6,
                ContentMarginRightOverride = 10,
                ContentMarginBottomOverride = 6
            }
        };
        content.AddChild(streakPanel);

        var streakBox = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 8
        };
        streakPanel.AddChild(streakBox);

        streakBox.AddChild(new Label
        {
            Text = Loc.GetString("daily-reward-window-streak") + ":",
            Modulate = Color.FromHex("#9fb4d8")
        });

        streakValueLabel = new Label { Modulate = AccentColor };
        streakBox.AddChild(streakValueLabel);

        activeProgressLabel = new Label { Modulate = Color.White };
        content.AddChild(CreateIconLabelRow(_clockTexture, activeProgressLabel));

        activeProgressBar = new PixelTiledProgressBar();
        content.AddChild(activeProgressBar);

        var statusRow = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 20
        };

        cooldownLabel = new Label { Modulate = Color.FromHex("#d7e2f4") };
        expiryLabel = new Label { Modulate = Color.FromHex("#9fb4d8") };
        statusRow.AddChild(cooldownLabel);
        statusRow.AddChild(expiryLabel);
        content.AddChild(statusRow);

        return panel;
    }

    private Control CreateRewardCard(DailyRewardEntry reward)
    {
        var state = GetCardState(reward);
        var (backgroundColor, borderColor) = state switch
        {
            DailyRewardCardState.Claimed => (ClaimedCardColor, ClaimedBorderColor),
            DailyRewardCardState.Current => (CurrentCardColor, CurrentBorderColor),
            _ => (FutureCardColor, FutureBorderColor)
        };

        var panel = new PanelContainer
        {
            MinSize = new Vector2(RewardCardWidth, 124),
            MaxSize = new Vector2(RewardCardWidth, 124),
            Margin = new Thickness(0, 0, 6, 0),
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = backgroundColor,
                BorderColor = borderColor,
                BorderThickness = new Thickness(2),
                ContentMarginLeftOverride = 8,
                ContentMarginTopOverride = 8,
                ContentMarginRightOverride = 8,
                ContentMarginBottomOverride = 8
            }
        };

        var box = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 4,
            VerticalAlignment = VAlignment.Center,
            HorizontalAlignment = HAlignment.Center,
            VerticalExpand = true
        };
        panel.AddChild(box);

        var header = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Center
        };
        box.AddChild(header);

        header.AddChild(new Label
        {
            Text = Loc.GetString("daily-reward-card-day", ("day", reward.Day)),
            StyleClasses = { "LabelHeading" },
            Modulate = Color.White
        });

        if (reward.HasReward)
        {
            var texture = TryGetRewardTexture(reward.IconPath) ?? _coinTexture;
            bool isCoinReward = reward.IconPath == CoinIconPath;

            if (isCoinReward)
            {
                var row = new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    SeparationOverride = 4,
                    HorizontalAlignment = HAlignment.Center,
                    VerticalAlignment = VAlignment.Center
                };

                var amountText = reward.RewardName?.TrimStart('+') ?? "1";
                row.AddChild(new Label
                {
                    Text = amountText,
                    Modulate = AccentColor,
                    HorizontalAlignment = HAlignment.Center,
                    VerticalAlignment = VAlignment.Center
                });

                row.AddChild(new TextureRect
                {
                    Texture = texture,
                    MinSize = new Vector2(20, 20),
                    TextureScale = new Vector2(0.35f, 0.35f),
                    Stretch = TextureRect.StretchMode.KeepAspectCentered,
                    VerticalAlignment = VAlignment.Center
                });

                box.AddChild(row);
            }
            else
            {
                box.AddChild(new TextureRect
                {
                    Texture = texture,
                    MinSize = new Vector2(40, 40),
                    TextureScale = new Vector2(0.5f, 0.5f),
                    Stretch = TextureRect.StretchMode.KeepAspectCentered,
                    HorizontalAlignment = HAlignment.Center
                });

                box.AddChild(new Label
                {
                    Text = Loc.GetString("daily-reward-card-token", ("name", reward.RewardName ?? "-")),
                    Modulate = AccentColor,
                    HorizontalAlignment = HAlignment.Center
                });
            }
        }

        return panel;
    }

    private Control CreateConnector(DailyRewardEntry left, DailyRewardEntry right)
    {
        var claimedAhead = left.IsClaimed && right.IsClaimed;
        var currentAhead = left.IsCurrent || right.IsCurrent;
        var color = claimedAhead
            ? ClaimedBorderColor
            : currentAhead
                ? CurrentBorderColor
                : FutureBorderColor;

        return new PanelContainer
        {
            MinSize = new Vector2(10, 2),
            Margin = new Thickness(0, 54, 0, 54),
            PanelOverride = new StyleBoxFlat { BackgroundColor = color }
        };
    }

    private Control CreateIconLabelRow(Texture texture, Label label)
    {
        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 4
        };

        row.AddChild(new TextureRect
        {
            Texture = texture,
            MinSize = new Vector2(14, 14),
            TextureScale = new Vector2(0.4f, 0.4f),
            Stretch = TextureRect.StretchMode.KeepCentered,
            VerticalAlignment = VAlignment.Center
        });

        row.AddChild(label);
        return row;
    }

    private Control CreateCenteredIconLabelRow(Texture texture, Label label)
    {
        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            HorizontalAlignment = HAlignment.Center
        };

        row.AddChild(new TextureRect
        {
            Texture = texture,
            MinSize = new Vector2(14, 14),
            TextureScale = new Vector2(0.4f, 0.4f),
            Stretch = TextureRect.StretchMode.KeepCentered,
            VerticalAlignment = VAlignment.Center
        });

        row.AddChild(label);
        return row;
    }

    private Texture? TryGetRewardTexture(string? iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath) || !iconPath.StartsWith('/'))
            return null;

        try
        {
            var path = new ResPath(iconPath);
            return _resourceCache.TryGetResource<TextureResource>(path, out var textureResource)
                ? textureResource.Texture
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static DailyRewardCardState GetCardState(DailyRewardEntry reward)
    {
        if (reward.IsClaimed)
            return DailyRewardCardState.Claimed;
        if (reward.IsCurrent)
            return DailyRewardCardState.Current;
        return DailyRewardCardState.Future;
    }

    private static string FormatActiveProgress(TimeSpan span)
    {
        if (span <= TimeSpan.Zero)
            return "00:00";
        return $"{span.Minutes:00}:{span.Seconds:00}";
    }

    private static string FormatCooldown(TimeSpan span)
    {
        if (span <= TimeSpan.Zero)
            return "00:00";
        var totalHours = (int)span.TotalHours;
        return $"{totalHours:00}:{span.Minutes:00}";
    }

    private static string GetAvailabilityText(DailyRewardUpdateMessage state)
    {
        if (state.TimeUntilNextClaim > TimeSpan.Zero)
            return Loc.GetString("daily-reward-window-cooldown-wait", ("time", FormatCooldown(state.TimeUntilNextClaim)));
        if (state.CurrentActiveTime < state.RequiredActiveTime)
            return Loc.GetString("daily-reward-window-active-needed");
        return Loc.GetString("daily-reward-window-cooldown-ready");
    }

    private static TimeSpan MaxZero(TimeSpan span) => span < TimeSpan.Zero ? TimeSpan.Zero : span;
    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left <= right ? left : right;

    private enum DailyRewardCardState : byte
    {
        Claimed,
        Current,
        Future
    }
}
