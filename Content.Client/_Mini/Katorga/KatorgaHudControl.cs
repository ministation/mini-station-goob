// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Numerics;
using Content.Client.Resources;
using Content.Client.UserInterface;
using Content.Shared._Mini.Katorga;
using Content.Shared._Mini.Katorga.Components;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Client._Mini.Katorga;

/// <summary>
///     Худ каторжанина внизу экрана: прогресс отработки срока с зелёной полоской
///     в стиле голосования. Панель оформлена как худ войны с тайпаном.
/// </summary>
public sealed class KatorgaHudControl : PanelContainer
{
    public const float PreferredWidth = 580f;
    // Панель зажата XAML-ом в 580px: 88 (заголовок) + 295 (полоса) + 150 (подпись)
    // + 20 (разделители) + 24 (поля) = 577. Больше — подпись вылезает за рамку.
    private const float BarWidth = 295f;

    private static readonly Color PanelBackground = Color.FromHex("#14141C").WithAlpha(0.72f);
    private static readonly Color TextColor = Color.FromHex("#E8E6F0");
    private static readonly Color TimerColor = Color.FromHex("#F0D890");
    private static readonly Color WorkedColor = Color.FromHex("#8FD08F");
    private static readonly Color FreeColor = Color.FromHex("#7BC87B");

    private readonly Label _remainingLabel;
    private readonly Label _workedLabel;
    private readonly KatorgaFillBarControl _bar;

    public KatorgaHudControl()
    {
        IoCManager.InjectDependencies(this);

        MinHeight = 42;
        MaxHeight = 96;
        HorizontalAlignment = HAlignment.Center;
        MouseFilter = MouseFilterMode.Ignore;
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = PanelBackground,
            BorderThickness = new Thickness(1),
            BorderColor = Color.FromHex("#6E6A82").WithAlpha(0.45f),
            ContentMarginLeftOverride = 12,
            ContentMarginRightOverride = 12,
            ContentMarginTopOverride = 6,
            ContentMarginBottomOverride = 6,
        };

        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalAlignment = VAlignment.Center,
            SeparationOverride = 4,
        };
        AddChild(column);

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            VerticalAlignment = VAlignment.Center,
        };
        column.AddChild(row);

        var titleLabel = new Label
        {
            Text = Loc.GetString("katorga-hud-title"),
            FontColorOverride = TextColor,
            MinWidth = 88,
            MaxWidth = 88,
        };

        _bar = new KatorgaFillBarControl(BarWidth)
        {
            VerticalAlignment = VAlignment.Center,
        };

        _remainingLabel = new Label
        {
            FontColorOverride = TimerColor,
            MinWidth = 150,
            MaxWidth = 150,
            Align = Label.AlignMode.Right,
        };

        row.AddChild(titleLabel);
        row.AddChild(_bar);
        row.AddChild(_remainingLabel);

        _workedLabel = new Label
        {
            FontColorOverride = WorkedColor,
            Align = Label.AlignMode.Center,
        };
        column.AddChild(_workedLabel);

        Visible = false;
    }

    /// <summary>
    ///     Обновляет худ данными с сервера. Полоса плавно дотекает сама:
    ///     срок уменьшается между обновлениями.
    /// </summary>
    public void ShowHud(KatorgaBoundComponent comp, TimeSpan receiveTime, TimeSpan now)
    {
        Visible = true;

        var remaining = comp.SentenceRemaining - (now - receiveTime);
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        var fraction = comp.SentenceTotal > TimeSpan.Zero
            ? 1f - (float) (remaining.TotalSeconds / comp.SentenceTotal.TotalSeconds)
            : 0f;
        _bar.SetProgress(fraction);

        if (remaining <= TimeSpan.Zero)
        {
            _remainingLabel.Text = Loc.GetString("katorga-hud-free");
            _remainingLabel.FontColorOverride = FreeColor;
        }
        else
        {
            _remainingLabel.Text = Loc.GetString("katorga-hud-remaining", ("time", KatorgaTime.Format(remaining)));
            _remainingLabel.FontColorOverride = TimerColor;
        }

        _workedLabel.Text = comp.WorkedOff > TimeSpan.Zero
            ? Loc.GetString("katorga-hud-worked", ("time", KatorgaTime.Format(comp.WorkedOff)))
            : string.Empty;
    }

    public void HideHud()
    {
        Visible = false;
    }
}

/// <summary>
///     Полоса прогресса отработки: зелёная заливка слева направо,
///     как в голосовании. Трек тот же, что у худа войны с тайпаном.
/// </summary>
public sealed class KatorgaFillBarControl : Control
{
    private const float BarScale = 2f;

    private static readonly Color FillColor = Color.FromHex("#408040").WithAlpha(0.95f);
    private static readonly Color TrackColor = Color.FromHex("#252530").WithAlpha(0.55f);

    [Dependency] private readonly IResourceCache _cache = default!;

    private readonly StyleBoxTexture _trackStyle;
    private float _progress;

    public KatorgaFillBarControl(float width)
    {
        _width = width;
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Ignore;
        MinHeight = MiniSliderStyles.NativeTrackHeight * BarScale;
        MaxHeight = MiniSliderStyles.NativeTrackHeight * BarScale;
        MinSize = new Vector2(width, MiniSliderStyles.NativeTrackHeight * BarScale);
        MaxSize = MinSize;

        var tex = _cache.GetTexture(MiniSliderStyles.LongWhiteTrackPath);
        _trackStyle = MiniSliderStyles.CreateLongTrackBox(tex, BarScale);
    }

    private readonly float _width;

    public void SetProgress(float progress)
    {
        _progress = Math.Clamp(progress, 0f, 1f);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var box = PixelSizeBox;
        if (box.Width <= 0 || box.Height <= 0)
            return;

        var track = new StyleBoxTexture(_trackStyle) { Modulate = TrackColor };
        track.Draw(handle, box, UIScale);

        var fillWidth = box.Width * _progress;
        if (fillWidth > 0.5f)
        {
            var fill = new StyleBoxTexture(_trackStyle) { Modulate = FillColor };
            fill.Draw(handle, UIBox2.FromDimensions(box.Left, box.Top, fillWidth, box.Height), UIScale);
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        return new Vector2(_width, MiniSliderStyles.NativeTrackHeight * BarScale);
    }
}
