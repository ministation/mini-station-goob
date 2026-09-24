using System.Numerics;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Content.Client.UserInterface;

/// <summary>
/// Horizontally expandable pixel-art progress bar with tiled track texture.
/// </summary>
public sealed class PixelTiledProgressBar : Control
{
    public const float DefaultScale = 2.5f;

    public static readonly Color EmptyModulate = PixelScrollProgressBar.EmptyModulate;

    [Dependency] private readonly IResourceCache _cache = default!;

    private readonly StyleBoxTexture _trackStyle;
    private readonly float _scale;
    private readonly Color _fillModulate;
    private readonly Color _emptyModulate;
    private float _value;

    public float Value
    {
        get => _value;
        set
        {
            var clamped = Math.Clamp(value, 0f, 1f);
            if (MathHelper.CloseToPercent(_value, clamped, 0.0001f))
                return;

            _value = clamped;
        }
    }

    public PixelTiledProgressBar(
        string texturePath = MiniSliderStyles.GreenPlainTrackPath,
        float scale = DefaultScale,
        Color? fillModulate = null,
        Color? emptyModulate = null)
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Ignore;
        HorizontalExpand = true;
        _scale = scale;
        _fillModulate = fillModulate ?? Color.White;
        _emptyModulate = emptyModulate ?? EmptyModulate;
        MinHeight = MiniSliderStyles.NativeTrackHeight * scale;

        var tex = _cache.GetTexture(texturePath);
        _trackStyle = MiniSliderStyles.CreateLongTrackBox(tex, scale);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var box = PixelSizeBox;
        if (box.Width <= 0 || box.Height <= 0)
            return;

        var empty = new StyleBoxTexture(_trackStyle) { Modulate = _emptyModulate };
        empty.Draw(handle, box, UIScale);

        if (_value <= 0f)
            return;

        var fillWidth = box.Width * _value;

        // Below the combined width of the texture's left+right patch caps the 9-slice draws the
        // right cap on top of the left edge and the strip renders mirrored. Clamp to the caps so
        // the smallest visible fill is still a proper leading segment.
        var minFillWidth = Math.Min(
            (_trackStyle.PatchMarginLeft + _trackStyle.PatchMarginRight) * _trackStyle.TextureScale.X * UIScale,
            box.Width);
        if (fillWidth < minFillWidth)
            fillWidth = minFillWidth;

        var fill = new StyleBoxTexture(_trackStyle) { Modulate = _fillModulate };
        fill.Draw(handle, UIBox2.FromDimensions(box.Left, box.Top, fillWidth, box.Height), UIScale);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        return new Vector2(0, MiniSliderStyles.NativeTrackHeight * _scale);
    }
}
