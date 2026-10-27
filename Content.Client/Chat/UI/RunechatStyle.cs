// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Resources;
using Content.Client.UserInterface;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Chat.UI;

public static class RunechatStyle
{
    public const int MinFontSize = 6;
    public const int MaxFontSize = 24;
    private const float DesignPixelsPerEm = 8f;

    private static readonly Dictionary<(string Key, int Size), Font> Fonts = new();
    private static readonly Dictionary<Font, Stylesheet> Sheets = new();

    /// <summary>
    /// Mini: runechat and popups draw with the selectable UI font (Cozette, Pixeloid, ...) and its Noto Sans
    /// fallback instead of Goob's hardcoded pixel font.
    /// </summary>
    public static Font GetFont(int size)
    {
        var uiFonts = IoCManager.Resolve<IUiFontStackManager>();
        uiFonts.Initialize();
        var key = (uiFonts.ActiveStyle.ID, size);
        if (Fonts.TryGetValue(key, out var font))
            return font;

        var cache = IoCManager.Resolve<IResourceCache>();
        font = cache.GetChatStack(size: size);
        Fonts[key] = font;
        return font;
    }

    /// <summary>Explicit font file, e.g. the NamePeek overlay when runechat is off.</summary>
    public static Font GetFont(string path, int size)
    {
        if (Fonts.TryGetValue((path, size), out var font))
            return font;

        var cache = IoCManager.Resolve<IResourceCache>();
        font = new VectorFont(cache.GetResource<FontResource>(path), size);
        Fonts[(path, size)] = font;
        return font;
    }

    public static Stylesheet GetStylesheet(Font font)
    {
        if (Sheets.TryGetValue(font, out var sheet))
            return sheet;

        sheet = new Stylesheet(new[]
        {
            new StyleRule(
                new SelectorElement(typeof(RichTextLabel), null, null, null),
                new[] { new StyleProperty(Label.StylePropertyFont, font) }),
        });
        Sheets[font] = sheet;
        return sheet;
    }

    public static int ScaleSize(int baseSize, int fontSize)
    {
        return Math.Max(1, (int) MathF.Round(baseSize * fontSize / 12f));
    }

    public static int GetLoudSize(int fontSize)
    {
        return ScaleSize(18, fontSize);
    }

    public static float GetOutline(int pointSize, float scale)
    {
        return pointSize * 96f / 72f / DesignPixelsPerEm * scale;
    }
}
