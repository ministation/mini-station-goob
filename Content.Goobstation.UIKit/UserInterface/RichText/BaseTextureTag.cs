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
    /// Mini sticker/icon size cap — keep inline chat stickers line-sized so multiple
    /// [tex] tags don't wrap onto the next line / over the name.
    /// </summary>
    protected static bool TryDrawIcon(Texture tex,
        long scaleValue,
        Vector2 offset,
        string? tooltip,
        [NotNullWhen(true)] out Control? control,
        float maxSize = 28f)
    {
        // Chat line height is ~16–20px; stickers are 32x32. Cap inline size so two
        // stickers stay on the same line instead of wrapping over "OOC:".
        var scale = Math.Max(1, (int) scaleValue);
        var natural = Math.Max(tex.Width, tex.Height) * scale;
        var size = Math.Clamp(natural, 12f, Math.Max(12f, maxSize));

        var texture = new TooltipTextureRect(tooltip, offset)
        {
            Texture = tex,
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            HorizontalExpand = false,
            VerticalExpand = false,
            HorizontalAlignment = Control.HAlignment.Left,
            VerticalAlignment = Control.VAlignment.Bottom,
            SetSize = new Vector2(size, size),
            MinSize = new Vector2(size, size),
            MaxSize = new Vector2(size, size),
        };

        control = texture;
        return true;
    }

    protected static Control DrawIcon(Texture tex,
        long scaleValue,
        Vector2 offset,
        string? tooltip,
        float maxSize = 28f)
    {
        TryDrawIcon(tex, scaleValue, offset, tooltip, out var control, maxSize);
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
