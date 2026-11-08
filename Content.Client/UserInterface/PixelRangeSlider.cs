// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using static Robust.Client.UserInterface.Controls.LayoutContainer;

namespace Content.Client.UserInterface;

/// <summary>
/// Continuous pixel-art range slider (tiled track + sprite pointer).
/// </summary>
public sealed class PixelRangeSlider : Control
{
    private const float Scale = MiniSliderStyles.UiScale;

    private static float ScaledTrackHeight => MiniSliderStyles.NativeTrackHeight * Scale;
    private static float ScaledPointerWidth => MiniSliderStyles.NativePointerWidth * Scale;
    private static float ScaledPointerHeight => MiniSliderStyles.NativePointerHeight * Scale;
    private static float ControlHeight => Math.Max(ScaledTrackHeight, ScaledPointerHeight);

    [Dependency] private readonly IResourceCache _cache = default!;

    private readonly PanelContainer _trackPanel;
    private readonly TextureRect _pointer;

    private bool _dragging;
    private float _minValue;
    private float _maxValue = 100f;
    private float _value;

    public float MinValue
    {
        get => _minValue;
        set
        {
            _minValue = value;
            Value = Math.Clamp(_value, _minValue, _maxValue);
            UpdatePointerVisuals();
        }
    }

    public float MaxValue
    {
        get => _maxValue;
        set
        {
            _maxValue = value;
            Value = Math.Clamp(_value, _minValue, _maxValue);
            UpdatePointerVisuals();
        }
    }

    public float Value
    {
        get => _value;
        set
        {
            var clamped = Math.Clamp(value, _minValue, _maxValue);
            if (MathHelper.CloseToPercent(clamped, _value))
                return;

            _value = clamped;
            UpdatePointerVisuals();
            OnValueChanged?.Invoke(new ValueChangedEventArgs(_value));
        }
    }

    public event Action<ValueChangedEventArgs>? OnValueChanged;

    public PixelRangeSlider()
    {
        IoCManager.InjectDependencies(this);

        MouseFilter = MouseFilterMode.Stop;
        MinHeight = ControlHeight;

        var trackTex = _cache.GetTexture(MiniSliderStyles.LongPlainTrackPath);
        _trackPanel = new PanelContainer
        {
            PanelOverride = MiniSliderStyles.CreateLongTrackBox(trackTex, Scale),
            MinHeight = ScaledTrackHeight,
            MaxHeight = ScaledTrackHeight,
            VerticalAlignment = VAlignment.Center,
        };

        _pointer = new TextureRect
        {
            Texture = _cache.GetTexture(MiniSliderStyles.PointerPath),
            Stretch = TextureRect.StretchMode.Keep,
            TextureScale = new Vector2(Scale, Scale),
            ModulateSelfOverride = Color.FromHex("#9A9AA8"),
        };

        var layout = new LayoutContainer
        {
            MinHeight = ControlHeight,
            HorizontalExpand = true,
        };

        layout.AddChild(_trackPanel);
        layout.AddChild(_pointer);

        SetAnchorPreset(_trackPanel, LayoutPreset.Wide);
        SetAnchorPreset(_pointer, LayoutPreset.TopLeft);

        var trackMarginTop = (ControlHeight - ScaledTrackHeight) / 2f;
        SetMarginTop(_trackPanel, trackMarginTop);

        var trackCenterY = trackMarginTop + ScaledTrackHeight / 2f;
        SetMarginTop(_pointer, trackCenterY - ScaledPointerHeight / 2f);

        AddChild(layout);
        UpdatePointerVisuals();
    }

    protected override void Resized()
    {
        base.Resized();
        UpdatePointerVisuals();
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _dragging = true;
        SetValueFromMouse(args.RelativePosition.X);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _dragging = false;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        if (!_dragging)
            return;

        SetValueFromMouse(args.RelativePosition.X);
    }

    private void SetValueFromMouse(float mouseX)
    {
        var travel = GetThumbTravel();
        if (travel <= 0f || MathHelper.CloseToPercent(_maxValue, _minValue))
            return;

        var thumbLeft = Math.Clamp(mouseX - ScaledPointerWidth / 2f, 0f, travel);
        var ratio = thumbLeft / travel;
        Value = _minValue + ratio * (_maxValue - _minValue);
    }

    private float GetThumbTravel() =>
        Math.Max(0f, PixelWidth - ScaledPointerWidth);

    private void UpdatePointerVisuals()
    {
        var travel = GetThumbTravel();
        var range = _maxValue - _minValue;
        var ratio = range <= 0f ? 0f : (_value - _minValue) / range;
        SetMarginLeft(_pointer, travel * ratio);
    }

    public readonly struct ValueChangedEventArgs
    {
        public ValueChangedEventArgs(float value) => Value = value;

        public float Value { get; }
    }
}
