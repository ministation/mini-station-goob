// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Numerics;
using Content.Client._Mini.UserInterface;
using Content.Client.UserInterface;
using Content.Shared.Objectives;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.Utility;
using Robust.Shared.Localization;
using Robust.Shared.Maths;

namespace Content.Client._Mini.Objectives;

/// <summary>
/// Antagonist objective card styled like compact daily quest cards.
/// </summary>
public sealed class AntagObjectiveCardControl : BoxContainer
{
    private readonly PixelTiledProgressBar _progressBar;
    private readonly Label _progressLabel;
    private readonly Label? _statusLabel;
    private readonly bool _showStatus;

    private string? _boundTitle;
    private string? _boundDescription;
    private float _boundProgress = -1f;
    private bool _boundComplete;

    public AntagObjectiveCardControl(bool showStatus = false)
    {
        _showStatus = showStatus;

        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 0;
        HorizontalExpand = true;
        VerticalExpand = false;

        _progressBar = new PixelTiledProgressBar();
        _progressLabel = new Label
        {
            Modulate = CharacterMenuCardStyle.ProgressTextColor,
            HorizontalAlignment = HAlignment.Right,
        };

        if (showStatus)
        {
            _statusLabel = new Label
            {
                HorizontalAlignment = HAlignment.Center,
                Modulate = Color.FromHex("#e8eef8"),
            };
        }
    }

    public void SetObjective(ObjectiveInfo info, Texture? icon)
    {
        var complete = info.Progress >= 0.99f;

        if (!NeedsObjectiveRebuild(info))
        {
            UpdateObjectiveProgress(info);
            return;
        }

        _boundTitle = info.Title;
        _boundDescription = info.Description;
        _boundProgress = info.Progress;
        _boundComplete = complete;

        DetachReusedControls();
        RemoveAllChildren();

        var panel = CreateCardPanel(complete);

        var column = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 4,
            HorizontalExpand = true,
            VerticalExpand = false,
        };
        panel.AddChild(column);
        AddChild(panel);

        var header = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true,
        };
        column.AddChild(header);

        if (icon != null)
        {
            header.AddChild(new TextureRect
            {
                Texture = icon,
                MinSize = new Vector2(CharacterMenuCardStyle.IconSize, CharacterMenuCardStyle.IconSize),
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                VerticalAlignment = VAlignment.Center,
            });
        }

        header.AddChild(CharacterMenuCardStyle.CreateTitleMarqueeHost(info.Title));

        if (!string.IsNullOrWhiteSpace(info.Description))
            column.AddChild(CharacterMenuCardStyle.CreateBodyMarqueeHost(info.Description));

        _progressBar.Value = Math.Clamp(info.Progress, 0f, 1f);
        _progressLabel.Text = FormatProgress(info.Progress);

        column.AddChild(_progressBar);
        column.AddChild(_progressLabel);

        if (_showStatus && _statusLabel != null)
        {
            _statusLabel.Text = complete
                ? Loc.GetString("antag-objective-complete")
                : Loc.GetString("antag-objective-active");
            _statusLabel.Modulate = complete
                ? CharacterMenuCardStyle.ClaimReadyColor
                : Color.FromHex("#e8eef8");
            column.AddChild(_statusLabel);
        }
    }

    public void UpdateObjectiveProgress(ObjectiveInfo info)
    {
        if (ChildCount == 0)
        {
            SetObjective(info, null);
            return;
        }

        _boundProgress = info.Progress;
        var complete = info.Progress >= 0.99f;

        _progressBar.Value = Math.Clamp(info.Progress, 0f, 1f);
        _progressLabel.Text = FormatProgress(info.Progress);

        if (_showStatus && _statusLabel != null)
        {
            _statusLabel.Text = complete
                ? Loc.GetString("antag-objective-complete")
                : Loc.GetString("antag-objective-active");
            _statusLabel.Modulate = complete
                ? CharacterMenuCardStyle.ClaimReadyColor
                : Color.FromHex("#e8eef8");
        }

        if (_boundComplete != complete
            && GetChild(0) is PanelContainer panel
            && panel.PanelOverride is StyleBoxFlat style)
        {
            _boundComplete = complete;
            style.BorderColor = complete
                ? CharacterMenuCardStyle.ClaimReadyColor.WithAlpha(0.4f)
                : CharacterMenuCardStyle.ObjectiveBorderColor.WithAlpha(0.35f);
        }
    }

    public void SetBriefing(string briefing)
    {
        DetachReusedControls();
        RemoveAllChildren();

        var panel = CreateBriefingPanel();
        panel.AddChild(CharacterMenuCardStyle.CreateBriefingMarqueeHost(briefing));
        AddChild(panel);
    }

    private bool NeedsObjectiveRebuild(ObjectiveInfo info)
    {
        return ChildCount == 0
            || _boundTitle != info.Title
            || _boundDescription != info.Description;
    }

    private void DetachReusedControls()
    {
        _progressBar.Orphan();
        _progressLabel.Orphan();
        _statusLabel?.Orphan();
    }

    private static PanelContainer CreateCardPanel(bool complete, float minHeight = 0) => new()
    {
        HorizontalExpand = true,
        VerticalExpand = false,
        RectClipContent = true,
        MinSize = minHeight > 0 ? new Vector2(0, minHeight) : default,
        PanelOverride = CharacterMenuCardStyle.CreateObjectivePanelStyle(complete),
    };

    private static PanelContainer CreateBriefingPanel() => new()
    {
        HorizontalExpand = true,
        VerticalExpand = false,
        RectClipContent = true,
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = CharacterMenuCardStyle.CardBackgroundColor.WithAlpha(0.55f),
            BorderColor = CharacterMenuCardStyle.AccentColor.WithAlpha(0.35f),
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 8,
            ContentMarginTopOverride = 6,
            ContentMarginRightOverride = 8,
            ContentMarginBottomOverride = 6,
        },
    };

    private static string FormatProgress(float progress)
    {
        var percent = (int)Math.Round(Math.Clamp(progress, 0f, 1f) * 100f);
        return Loc.GetString("antag-objective-progress", ("percent", percent));
    }
}
