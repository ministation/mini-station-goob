// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client._Mini.Fishing.UI;

/// <summary>
/// Invisible full screen control that turns mouse wheel notches into reel input.
/// It only exists during the bite window and the fight, so the HUD stays usable while waiting.
/// </summary>
public sealed class FishingMinigameInputControl : Control
{
    public event Action<int>? OnReel;
    public event Action? OnHook;

    public FishingMinigameInputControl()
    {
        MouseFilter = MouseFilterMode.Stop;
        MinSize = new Vector2(3000f, 3000f);
        LayoutContainer.SetAnchorPreset(this, LayoutContainer.LayoutPreset.Wide);

        OnKeyBindDown += args =>
        {
            if (args.Function != EngineKeyFunctions.UIClick)
                return;

            args.Handle();
            OnHook?.Invoke();
        };
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (Parent is { } parent && Size != parent.Size)
            SetSize = parent.Size;
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        if (args.Delta.Y == 0f)
            return;

        args.Handle();
        OnReel?.Invoke(args.Delta.Y > 0f ? 1 : -1);
    }
}
