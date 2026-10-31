// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Numerics;
using Content.Client.UserInterface.Systems.Chat.Widgets;
using Robust.Client.UserInterface;

namespace Content.Client.UserInterface.Screens;

/// <summary>
///     Screens that are considered to be 'in-game'.
/// </summary>
public abstract class InGameScreen : UIScreen
{
    public Action<Vector2>? OnChatResized;

    /// <summary>
    ///     Wheel notches that reached the screen itself: no control under the cursor
    ///     consumed them (a scrollable list, the chat box). Raised without marking the
    ///     event handled, so nothing on the screen is blocked by listeners.
    /// </summary>
    public event Action<GUIMouseWheelEventArgs>? WheelScrolled;

    public abstract ChatBox ChatBox { get; }

    public abstract void SetChatSize(Vector2 size);

    // protected, not protected internal: MouseWheel is protected internal in Robust.Client,
    // and overrides from another assembly must drop the internal part.
    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        if (!args.Handled)
            WheelScrolled?.Invoke(args);
    }
}
