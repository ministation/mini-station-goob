// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;

namespace Content.Client._Mini.Fishing.UI;

/// <summary>
/// Invisible full screen control that turns mouse wheel notches into reel input.
/// It only exists during the bite window and the fight, so the HUD stays usable while waiting.
/// It has to be parented above the game viewport, which is a full screen control itself.
/// </summary>
public sealed class FishingMinigameInputControl : Control
{
    public event Action<int>? OnReel;
    public event Action? OnHook;

    public FishingMinigameInputControl()
    {
        MouseFilter = MouseFilterMode.Stop;
        LayoutContainer.SetAnchorPreset(this, LayoutContainer.LayoutPreset.Wide);

        OnKeyBindDown += args =>
        {
            if (args.Function != EngineKeyFunctions.UIClick)
                return;

            args.Handle();
            OnHook?.Invoke();
        };
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
