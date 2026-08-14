using System;
using Content.Client.UserInterface;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.IoC;

namespace Content.Client.Resources;

/// <summary>
/// Pixel font stack for the Mini fork. Place font files under <see cref="Dir"/>.
/// </summary>
[PublicAPI]
public static class MiniFonts
{
    public const string Dir = "/Fonts/_Mini/Pixeloid/";

    public const string Regular = Dir + "PixeloidSans.ttf";
    public const string Bold = Dir + "PixeloidSans-Bold.ttf";
    public const string Mono = Dir + "PixeloidMono.ttf";

    /// <summary>Fallback for Cyrillic and other glyphs missing from the pixel font.</summary>
    public const string NotoRegular = "/Fonts/NotoSans/NotoSans-Regular.ttf";

    public const string NotoBold = "/Fonts/NotoSans/NotoSans-Bold.ttf";

    /// <summary>Fallback for emoji and rare symbols missing from the pixel font.</summary>
    public const string Symbols = "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf";

    /// <summary>Fallback for CJK characters.</summary>
    public const string Cjk = "/Fonts/NotoSans/NotoSansSC-Regular.ttf";

    public static string Resolve(string variation = "Regular", bool mono = false)
    {
        if (mono || variation == "Mono-Regular")
            return Mono;

        return variation switch
        {
            "Bold" or "BoldItalic" => Bold,
            "Italic" => Regular,
            _ when variation.StartsWith("Bold", StringComparison.Ordinal) => Bold,
            _ => Regular,
        };
    }

    public static string[] Stack(string variation = "Regular", bool mono = false)
    {
        var noto = variation.StartsWith("Bold", StringComparison.Ordinal) ? NotoBold : NotoRegular;

        return
        [
            Resolve(variation, mono),
            noto,
            Symbols,
            Cjk,
        ];
    }

    public static string[] StackWithPrimary(string primaryPath)
    {
        return
        [
            primaryPath,
            NotoRegular,
            Symbols,
            Cjk,
        ];
    }

    public static Font GetStack(this IResourceCache cache, string variation = "Regular", int size = 10, bool mono = false)
    {
        // Unit tests construct stylesheets without ClientContentIoC / Mini font files.
        if (IoCManager.Instance is { } ioc
            && ioc.TryResolveType<IUiFontStackManager>(out var fonts))
            return fonts.GetStack(cache, mono ? "Mono-Regular" : variation, size);

        return new DummyFont();
    }

    public static Font GetChatStack(this IResourceCache cache, string variation = "Regular", int size = 12)
    {
        if (IoCManager.Instance is { } ioc
            && ioc.TryResolveType<IUiFontStackManager>(out var fonts))
            return fonts.GetChatStack(cache, variation, size);

        return new DummyFont();
    }

    public static Font GetStackWithPrimary(this IResourceCache cache, string path, int size = 10)
    {
        if (IoCManager.Instance is { } ioc
            && ioc.TryResolveType<IUiFontStackManager>(out var fonts))
            return fonts.GetStackWithPrimary(cache, path, size);

        return new DummyFont();
    }
}
