// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Maths;

namespace Content.Client._Mini.Fishing.UI;

/// <summary>
/// Small invisible control that follows the float and turns a left click into a hook.
/// Only the reel wheel is global now (the screen event), so this control covers just the
/// fishing marker area: the rest of the screen, the HUD and open windows stay clickable
/// while the minigame runs.
/// </summary>
public sealed class FishingMinigameInputControl : Control
{
    public event Action? OnHook;

    public FishingMinigameInputControl()
    {
        MouseFilter = MouseFilterMode.Stop;
        LayoutContainer.SetAnchorPreset(this, LayoutContainer.LayoutPreset.TopLeft);

        OnKeyBindDown += args =>
        {
            if (args.Function != EngineKeyFunctions.UIClick)
                return;

            args.Handle();
            OnHook?.Invoke();
        };
    }

    /// <summary>
    /// Moves the catcher onto the fishing marker. A small control is the point: a full screen
    /// one would swallow every click on the screen while the fight is running.
    /// </summary>
    public void SetRect(Vector2 position, Vector2 size)
    {
        MinSize = size;
        MaxSize = size;

        LayoutContainer.SetMarginLeft(this, position.X);
        LayoutContainer.SetMarginTop(this, position.Y);
        LayoutContainer.SetMarginRight(this, 0f);
        LayoutContainer.SetMarginBottom(this, 0f);
    }
}
