// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

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
/// Boss fight panel at the bottom of the screen: boss name, red health bar with a grey
/// armor tail, current health and armor and the fight stage. Styled like the Katorga and
/// Typan war HUDs.
/// </summary>
public sealed class BossHudControl : PanelContainer
{
    private const float BarWidth = 300f;
    private const float StageWidth = 110f;

    private static readonly Color PanelBackground = Color.FromHex("#14141C").WithAlpha(0.72f);
    private static readonly Color TextColor = Color.FromHex("#E8E6F0");
    private static readonly Color StageColor = Color.FromHex("#B7B2C8");
    private static readonly Color ArmorColor = Color.FromHex("#9A97A5");

    private readonly Label _nameLabel;
    private readonly Label _healthLabel;
    private readonly Label _armorLabel;
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
        _armorLabel = new Label
        {
            FontColorOverride = ArmorColor,
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
        barRow.AddChild(_armorLabel);
        barRow.AddChild(_healthLabel);

        Visible = false;
    }

    private string? _shownName;
    private int _shownHealth = -1;
    private int _shownMaxHealth = -1;
    private int _shownArmor = -1;
    private int _shownMaxArmor = -1;
    private int _shownStage = -1;
    private int _shownMaxStage = -1;
    private bool _shownArmorBroken;
    private bool _shown;

    public void ShowHud(string name, float health, float maxHealth, float armor, float maxArmor,
        bool armorBroken, int stage, int maxStage)
    {
        var shownHealth = (int) MathF.Ceiling(health);
        var shownMaxHealth = (int) MathF.Ceiling(maxHealth);
        var shownArmor = (int) MathF.Ceiling(armor);
        var shownMaxArmor = (int) MathF.Ceiling(maxArmor);

        // The HUD is polled every frame, and every label write relayouts the whole UI.
        // Skip the work while nothing the player can see changed.
        if (_shown
            && _shownName == name
            && _shownHealth == shownHealth
            && _shownMaxHealth == shownMaxHealth
            && _shownArmor == shownArmor
            && _shownMaxArmor == shownMaxArmor
            && _shownArmorBroken == armorBroken
            && _shownStage == stage
            && _shownMaxStage == maxStage)
            return;

        _shown = true;
        _shownName = name;
        _shownHealth = shownHealth;
        _shownMaxHealth = shownMaxHealth;
        _shownArmor = shownArmor;
        _shownMaxArmor = shownMaxArmor;
        _shownArmorBroken = armorBroken;
        _shownStage = stage;
        _shownMaxStage = maxStage;

        Visible = true;

        _nameLabel.Text = name;
        // Single stage bosses have nothing to report, a "1/1" label is just noise.
        _stageLabel.Text = maxStage > 1
            ? Loc.GetString("boss-hud-stage", ("stage", stage), ("max", maxStage))
            : string.Empty;
        _healthLabel.Text = Loc.GetString("boss-hud-health",
            ("current", (int) MathF.Ceiling(health)),
            ("max", (int) MathF.Ceiling(maxHealth)));

        // Bosses without an armor pool get no grey segment and no armor label.
        var hasArmor = maxArmor > 0f;
        _armorLabel.Visible = hasArmor;
        if (hasArmor)
        {
            _armorLabel.Text = armorBroken
                ? Loc.GetString("boss-hud-armor-broken")
                : Loc.GetString("boss-hud-armor",
                    ("current", (int) MathF.Ceiling(armor)),
                    ("max", (int) MathF.Ceiling(maxArmor)));
        }

        var healthProgress = maxHealth > 0f ? health / maxHealth : 0f;
        var armorProgress = 0f;
        // With armor up the bar shows the combined pool: red health, grey armor.
        if (hasArmor && armor > 0f)
        {
            var total = maxHealth + maxArmor;
            if (total > 0f)
            {
                healthProgress = health / total;
                armorProgress = armor / total;
            }
        }

        _bar.SetProgress(healthProgress, armorProgress);
    }

    public void HideHud()
    {
        _shown = false;
        Visible = false;
    }
}

/// <summary>
/// Red health bar with a grey armor tail: dim track, a left to right red fill and the
/// armor as a grey segment after it, same track texture as the other mini station HUD bars.
/// </summary>
public sealed class BossHudBarControl : Control
{
    private const float BarScale = 2f;

    private static readonly Color FillColor = Color.FromHex("#C43B3B").WithAlpha(0.95f);
    private static readonly Color ArmorFillColor = Color.FromHex("#8F8C9C").WithAlpha(0.95f);
    private static readonly Color TrackColor = Color.FromHex("#252530").WithAlpha(0.55f);

    [Dependency] private readonly IResourceCache _cache = default!;

    private readonly StyleBoxTexture _trackStyle;
    private readonly float _width;
    private float _progress;
    private float _armorProgress;

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

    public void SetProgress(float progress, float armorProgress)
    {
        _progress = Math.Clamp(progress, 0f, 1f);
        _armorProgress = Math.Clamp(armorProgress, 0f, 1f);
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

        var armorWidth = MathF.Min(box.Width * _armorProgress, box.Width - fillWidth);
        if (armorWidth > 0.5f)
        {
            var armorFill = new StyleBoxTexture(_trackStyle) { Modulate = ArmorFillColor };
            armorFill.Draw(handle, UIBox2.FromDimensions(box.Left + fillWidth, box.Top, armorWidth, box.Height), UIScale);
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        return new Vector2(_width, MiniSliderStyles.NativeTrackHeight * BarScale);
    }
}
