// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Goobstation.UIKit.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.UIKit.UserInterface.RichText;

public abstract class BaseTextureTag
{
    [Dependency] protected readonly IEntitySystemManager EntitySystemManager = default!;

    /// <summary>
    /// Mini sticker/icon size cap — without MaxSize, RSI/frames can explode into a huge white stripe in AHelp.
    /// </summary>
    protected static bool TryDrawIcon(Texture tex,
        long scaleValue,
        Vector2 offset,
        string? tooltip,
        [NotNullWhen(true)] out Control? control)
    {
        var adjustedOffset = offset + new Vector2(-6, -4);
        var texture = new TooltipTextureRect(tooltip, adjustedOffset)
        {
            Texture = tex,
            TextureScale = new Vector2(scaleValue, scaleValue),
            MaxSize = new Vector2(120, 120),
        };

        control = texture;
        return true;
    }

    protected static Control DrawIcon(Texture tex,
        long scaleValue,
        Vector2 offset,
        string? tooltip)
    {
        TryDrawIcon(tex, scaleValue, offset, tooltip, out var control);
        return control!;
    }

    protected static bool TryDrawIconEntity(NetEntity netEntity, long spriteSize, [NotNullWhen(true)] out Control? control)
    {
        var spriteView = new StaticSpriteView()
        {
            OverrideDirection = Direction.South,
            SetSize = new Vector2(spriteSize * 2, spriteSize * 2),
        };

        spriteView.SetEntity(netEntity);
        spriteView.Scale = new Vector2(2, 2);

        control = spriteView;
        return true;
    }

    protected static Control DrawIconEntity(NetEntity netEntity, long spriteSize)
    {
        TryDrawIconEntity(netEntity, spriteSize, out var control);
        return control!;
    }

    protected static string ClearString(string str)
    {
        str = str.Replace("=", "");
        str = str.Replace("\"", "");
        str = str.Trim();

        return str;
    }
}
