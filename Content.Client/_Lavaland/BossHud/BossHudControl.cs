// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Numerics;
using Content.Client.Resources;
using Content.Client.UserInterface;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Lavaland.BossHud;

/// <summary>
/// Boss fight panel at the bottom of the screen: boss name, red health bar, current
/// and max health and the fight stage. Styled like the Katorga and Typan war HUDs.
/// </summary>
public sealed class BossHudControl : PanelContainer
{
    private const float BarWidth = 300f;
    private const float StageWidth = 110f;

    private static readonly Color PanelBackground = Color.FromHex("#14141C").WithAlpha(0.72f);
    private static readonly Color TextColor = Color.FromHex("#E8E6F0");
    private static readonly Color StageColor = Color.FromHex("#B7B2C8");

    private readonly Label _nameLabel;
    private readonly Label _healthLabel;
    private readonly Label _stageLabel;
    private readonly BossHudBarControl _bar;

    public BossHudControl()
    {
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

        var header = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            VerticalAlignment = VAlignment.Center,
        };
        column.AddChild(header);

        _nameLabel = new Label
        {
            FontColorOverride = TextColor,
            ClipText = true,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
        };
        _stageLabel = new Label
        {
            FontColorOverride = StageColor,
            MinWidth = StageWidth,
            MaxWidth = StageWidth,
            Align = Label.AlignMode.Right,
            VerticalAlignment = VAlignment.Center,
        };
        header.AddChild(_nameLabel);
        header.AddChild(_stageLabel);

        var barRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            VerticalAlignment = VAlignment.Center,
        };
        column.AddChild(barRow);

        _bar = new BossHudBarControl(BarWidth)
        {
            VerticalAlignment = VAlignment.Center,
        };
        _healthLabel = new Label
        {
            FontColorOverride = TextColor,
            HorizontalExpand = true,
            Align = Label.AlignMode.Right,
            VerticalAlignment = VAlignment.Center,
        };
        barRow.AddChild(_bar);
        barRow.AddChild(_healthLabel);

        Visible = false;
    }

    public void ShowHud(string name, float health, float maxHealth, int stage, int maxStage)
    {
        Visible = true;

        _nameLabel.Text = name;
        // Single stage bosses have nothing to report, a "1/1" label is just noise.
        _stageLabel.Text = maxStage > 1
            ? Loc.GetString("boss-hud-stage", ("stage", stage), ("max", maxStage))
            : string.Empty;
        _healthLabel.Text = Loc.GetString("boss-hud-health",
            ("current", (int) MathF.Ceiling(health)),
            ("max", (int) MathF.Ceiling(maxHealth)));

        _bar.SetProgress(maxHealth > 0f ? health / maxHealth : 0f);
    }

    public void HideHud()
    {
        Visible = false;
    }
}

/// <summary>
/// Red health bar: dim track and a left to right fill, same track texture as the
/// other mini station HUD bars.
/// </summary>
public sealed class BossHudBarControl : Control
{
    private const float BarScale = 2f;

    private static readonly Color FillColor = Color.FromHex("#C43B3B").WithAlpha(0.95f);
    private static readonly Color TrackColor = Color.FromHex("#252530").WithAlpha(0.55f);

    [Dependency] private readonly IResourceCache _cache = default!;

    private readonly StyleBoxTexture _trackStyle;
    private readonly float _width;
    private float _progress;

    public BossHudBarControl(float width)
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
