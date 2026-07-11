using System;
using System.Numerics;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Client.UserInterface;
using Content.Shared._Mini.TypanWar;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._Mini.TypanWar;

public sealed class TypanWarHudControl : PanelContainer
{
    public const float PreferredWidth = 580f;
    private const float BarWidth = 280f;

    private static readonly Color PanelBackground = Color.FromHex("#14141C").WithAlpha(0.72f);

    private readonly Label _titleLabel;
    private readonly Label _ntScoreLabel;
    private readonly Label _typanScoreLabel;
    private readonly Label _timerLabel;
    private readonly TypanWarApexScoreBarControl _bar;

    public TypanWarHudControl()
    {
        IoCManager.InjectDependencies(this);

        MinHeight = 42;
        MaxHeight = 64;
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

        _titleLabel = new Label
        {
            FontColorOverride = Color.FromHex("#E8E6F0"),
            MinWidth = 88,
            MaxWidth = 88,
        };

        _ntScoreLabel = new Label
        {
            FontColorOverride = Color.FromHex("#A8C8FF"),
            MinWidth = 44,
            MaxWidth = 44,
            Align = Label.AlignMode.Right,
        };

        _bar = new TypanWarApexScoreBarControl(BarWidth)
        {
            VerticalAlignment = VAlignment.Center,
        };

        _typanScoreLabel = new Label
        {
            FontColorOverride = Color.FromHex("#FFB0B0"),
            MinWidth = 44,
            MaxWidth = 44,
        };

        _timerLabel = new Label
        {
            FontColorOverride = Color.FromHex("#F0D890"),
            MinWidth = 56,
            MaxWidth = 56,
            Align = Label.AlignMode.Right,
            Margin = new Thickness(6, 0, 0, 0),
        };

        row.AddChild(_titleLabel);
        row.AddChild(_ntScoreLabel);
        row.AddChild(_bar);
        row.AddChild(_typanScoreLabel);
        row.AddChild(_timerLabel);

        Visible = false;
    }

    public void Update(
        TypanWarPhase phase,
        TypanWarWinner winner,
        float ntPoints,
        float typanPoints,
        int pointsToWin,
        float timeRemainingSeconds)
    {
        Visible = phase is TypanWarPhase.Pending or TypanWarPhase.Active or TypanWarPhase.Ended;
        if (!Visible)
            return;

        _titleLabel.Text = phase switch
        {
            TypanWarPhase.Pending => Loc.GetString("typan-war-hud-pending"),
            TypanWarPhase.Ended => winner switch
            {
                TypanWarWinner.Nanotrasen => Loc.GetString("typan-war-hud-winner-nt"),
                TypanWarWinner.Typan => Loc.GetString("typan-war-hud-winner-typan"),
                _ => Loc.GetString("typan-war-hud-ended"),
            },
            _ => Loc.GetString("typan-war-hud-active"),
        };

        _titleLabel.FontColorOverride = phase == TypanWarPhase.Ended
            ? winner switch
            {
                TypanWarWinner.Nanotrasen => Color.FromHex("#A8C8FF"),
                TypanWarWinner.Typan => Color.FromHex("#FFB0B0"),
                _ => Color.FromHex("#E8E6F0"),
            }
            : Color.FromHex("#E8E6F0");

        if (phase == TypanWarPhase.Pending)
        {
            _ntScoreLabel.Text = "0";
            _typanScoreLabel.Text = "0";
            _bar.SetPoints(0, 0, pointsToWin);
        }
        else
        {
            _ntScoreLabel.Text = ((int) ntPoints).ToString();
            _typanScoreLabel.Text = ((int) typanPoints).ToString();
            _bar.SetPoints(ntPoints, typanPoints, pointsToWin);
        }

        var span = TimeSpan.FromSeconds(Math.Max(0, timeRemainingSeconds));
        _timerLabel.Text = $"{(int) span.TotalMinutes:00}:{(int) (span.TotalSeconds % 60):00}";
        _timerLabel.Visible = phase != TypanWarPhase.Ended;
    }
}

/// <summary>
/// Apex-style score bar: both factions grow toward the center from opposite sides.
/// </summary>
public sealed class TypanWarApexScoreBarControl : Control
{
    private const float BarScale = 2f;

    private static readonly Color NtColor = Color.FromHex("#4A7FD4").WithAlpha(0.9f);
    private static readonly Color TypanColor = Color.FromHex("#C84848").WithAlpha(0.9f);
    private static readonly Color EmptyModulate = Color.FromHex("#252530").WithAlpha(0.55f);
    private static readonly Color CenterLineColor = Color.FromHex("#E8E6F0").WithAlpha(0.35f);

    [Dependency] private readonly IResourceCache _cache = default!;

    private readonly StyleBoxTexture _trackStyle;
    private readonly float _width;
    private float _ntPoints;
    private float _typanPoints;
    private int _pointsToWin = 100;

    public TypanWarApexScoreBarControl(float width)
    {
        _width = width;
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Ignore;
        MinHeight = MiniSliderStyles.NativeTrackHeight * BarScale;
        MaxHeight = MiniSliderStyles.NativeTrackHeight * BarScale;
        MinSize = new Vector2(_width, MiniSliderStyles.NativeTrackHeight * BarScale);
        MaxSize = MinSize;

        var tex = _cache.GetTexture(MiniSliderStyles.LongWhiteTrackPath);
        _trackStyle = MiniSliderStyles.CreateLongTrackBox(tex, BarScale);
    }

    public void SetPoints(float ntPoints, float typanPoints, int pointsToWin)
    {
        _ntPoints = Math.Max(0, ntPoints);
        _typanPoints = Math.Max(0, typanPoints);
        _pointsToWin = Math.Max(1, pointsToWin);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var box = PixelSizeBox;
        if (box.Width <= 0 || box.Height <= 0)
            return;

        var empty = new StyleBoxTexture(_trackStyle) { Modulate = EmptyModulate };
        empty.Draw(handle, box, UIScale);

        var centerX = box.Left + box.Width * 0.5f;
        handle.DrawLine(new Vector2(centerX, box.Top), new Vector2(centerX, box.Bottom), CenterLineColor);

        var ntFrac = Math.Min(_ntPoints / _pointsToWin, 1f);
        var typanFrac = Math.Min(_typanPoints / _pointsToWin, 1f);
        var halfWidth = box.Width * 0.5f;

        var ntWidth = halfWidth * ntFrac;
        if (ntWidth > 0.5f)
        {
            var fill = new StyleBoxTexture(_trackStyle) { Modulate = NtColor };
            fill.Draw(handle, UIBox2.FromDimensions(box.Left, box.Top, ntWidth, box.Height), UIScale);
        }

        var typanWidth = halfWidth * typanFrac;
        if (typanWidth > 0.5f)
        {
            var fill = new StyleBoxTexture(_trackStyle) { Modulate = TypanColor };
            fill.Draw(handle, UIBox2.FromDimensions(box.Right - typanWidth, box.Top, typanWidth, box.Height), UIScale);
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        return new Vector2(_width, MiniSliderStyles.NativeTrackHeight * BarScale);
    }
}
