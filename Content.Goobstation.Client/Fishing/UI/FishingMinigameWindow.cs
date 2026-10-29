// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Goobstation.Shared.Fishing;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Content.Goobstation.Client.Fishing.UI;

/// <summary>
/// Window shown while a fishing challenge runs: waiting for a bite, then the reel minigame.
/// The minigame itself is simulated on the server at 20 Hz and mirrored here.
/// </summary>
public sealed class FishingMinigameWindow : FancyWindow
{
    private readonly Label _status;
    private readonly MinigameBar _bar;

    public event Action<int>? OnReel;

    public FishingMinigameWindow()
    {
        Title = Loc.GetString("fishing-minigame-title");
        SetSize = new Vector2(240, 430);
        MinSize = new Vector2(220, 390);

        _status = new Label
        {
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 0, 0, 6),
        };

        _bar = new MinigameBar
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        _bar.OnReel += dir => OnReel?.Invoke(dir);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(10),
        };

        root.AddChild(_status);
        root.AddChild(_bar);
        ContentsContainer.AddChild(root);
    }

    public void UpdateState(FishingMinigameBoundUserInterfaceState state)
    {
        _bar.Apply(state);

        _status.Text = state.Phase switch
        {
            FishingPhase.Waiting => Loc.GetString("fishing-ui-waiting"),
            FishingPhase.Biting => Loc.GetString("fishing-ui-bite"),
            FishingPhase.Minigame => Loc.GetString("fishing-ui-reel"),
            _ => string.Empty,
        };
    }
}

/// <summary>
/// The vertical bar with the fish and the bait, drawn from the tg fishing HUD textures.
/// </summary>
public sealed class MinigameBar : Control
{
    private const float Scale = 3f;
    private const float Area = 1000f;
    private const string RsiPath = "/Textures/_Goobstation/Interface/Fishing/fishing_hud.rsi";

    private readonly RSI _rsi;

    private FishingMinigameBoundUserInterfaceState? _state;

    public event Action<int>? OnReel;

    public MinigameBar()
    {
        IoCManager.InjectDependencies(this);
        var cache = IoCManager.Resolve<IResourceCache>();
        _rsi = cache.GetResource<RSIResource>(RsiPath).RSI;

        MouseFilter = MouseFilterMode.Stop;

        OnKeyBindDown += args =>
        {
            if (args.Function == EngineKeyFunctions.UIClick)
                OnReel?.Invoke(1);
            else if (args.Function == EngineKeyFunctions.UIRightClick)
                OnReel?.Invoke(-1);
        };

        OnKeyBindUp += args =>
        {
            if (args.Function == EngineKeyFunctions.UIClick || args.Function == EngineKeyFunctions.UIRightClick)
                OnReel?.Invoke(0);
        };
    }

    public void Apply(FishingMinigameBoundUserInterfaceState state)
    {
        _state = state;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        return new Vector2(32f * Scale, 96f * Scale);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (_state == null)
            return;

        var box = PixelSizeBox;
        if (box.Width <= 0 || box.Height <= 0)
            return;

        var barW = 32f * Scale;
        var barH = 96f * Scale;
        var bar = UIBox2.FromDimensions(box.Left + (box.Width - barW) / 2f, box.Top + (box.Height - barH) / 2f, barW, barH);

        if (TryTex(_state.Background) is { } background)
            handle.DrawTextureRectRegion(background, bar);

        var innerLeft = bar.Left;
        var innerWidth = 24f * Scale;

        // Bait zone.
        var baitBottom = bar.Bottom - Math.Clamp(_state.BaitPosition / Area, 0f, 1f) * barH;
        var baitHeight = Math.Max(6f, Math.Clamp(_state.BaitHeight / Area, 0f, 1f) * barH);
        var baitRect = new UIBox2(innerLeft, Math.Max(bar.Top, baitBottom - baitHeight), innerLeft + innerWidth, Math.Min(bar.Bottom, baitBottom));
        if (baitRect.Height > 0f)
        {
            handle.DrawRect(baitRect, Color.FromHex("#7bc96f33"));
            handle.DrawRect(new UIBox2(baitRect.Left, baitRect.Top, baitRect.Right, baitRect.Top + 1f), Color.FromHex("#7bc96faa"));
        }

        // Fish sprite, shifted to its position and clipped to the bar.
        if (TryTex(_state.FishIcon) is { } fish)
        {
            var fishOffset = Math.Clamp(_state.FishPosition / Area, 0f, 1f) * 76f * Scale;
            var dest = new UIBox2(innerLeft, bar.Top - fishOffset, innerLeft + barW, bar.Bottom - fishOffset);
            DrawTextureClipped(handle, fish, dest, bar);
        }

        // Completion bar on the right column.
        var completionWidth = 6f * Scale;
        var completionHeight = barH * Math.Clamp(_state.Completion / 100f, 0f, 1f);
        var completionRect = new UIBox2(bar.Right - completionWidth - 2f * Scale, bar.Bottom - completionHeight, bar.Right - 2f * Scale, bar.Bottom);
        handle.DrawRect(completionRect, CompletionColor(_state.Completion));

        if (TryTex(_state.FrameState) is { } frame)
            handle.DrawTextureRectRegion(frame, bar);
    }

    private static Color CompletionColor(float completion)
    {
        var t = Math.Clamp(completion / 100f, 0f, 1f);
        return new Color(1f - t, 0.35f + t * 0.55f, 0.2f + t * 0.2f, 0.9f);
    }

    private Texture? TryTex(string stateName)
    {
        return _rsi.TryGetState(stateName, out var state) ? state.Frame0 : null;
    }

    private static void DrawTextureClipped(DrawingHandleScreen handle, Texture texture, UIBox2 dest, UIBox2 clip)
    {
        var inter = new UIBox2(
            MathF.Max(dest.Left, clip.Left),
            MathF.Max(dest.Top, clip.Top),
            MathF.Min(dest.Right, clip.Right),
            MathF.Min(dest.Bottom, clip.Bottom));

        if (inter.Width <= 0f || inter.Height <= 0f)
            return;

        var sub = new UIBox2(
            (inter.Left - dest.Left) / dest.Width * texture.Width,
            (inter.Top - dest.Top) / dest.Height * texture.Height,
            (inter.Right - dest.Left) / dest.Width * texture.Width,
            (inter.Bottom - dest.Top) / dest.Height * texture.Height);

        handle.DrawTextureRectRegion(texture, inter, sub);
    }
}