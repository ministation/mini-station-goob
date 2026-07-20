using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Log;
using Content.Shared._Amour.Stickers;

namespace Content.Goobstation.UIKit.UserInterface.RichText;

public sealed class TextureTag : BaseTextureTag, IMarkupTagHandler
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public string Name => "tex";

    // Whitelist путей для системных иконок, не относящихся к стикерам
    private static readonly HashSet<string> AllowedTexturePaths = new()
    {
        "/Textures/Interface/Misc/job_icons.rsi",
        "/Textures/_Mini/Interface/Misc/job_icons.rsi",
        "/Textures/_Goobstation/Interface/Misc/job_icons.rsi",
        "/Textures/_CorvaxGoob/Interface/Misc/job_icons.rsi",
        "/Textures/_Lavaland/Interface/Misc/job_icons.rsi",
        "/Textures/_Goobstation/Hertic/",
    };

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        control = null;

        if (!node.Attributes.TryGetValue("path", out var rawPathParameter) ||
            !rawPathParameter.TryGetString(out var rawPath))
            return false;

        // Amour edit start
        if (!IsValidTexturePath(rawPath))
        {
            Logger.WarningS("texture-tag", $"Attempted to use non-whitelisted texture path: {rawPath}");
            return false;
        }
        // Amour edit end

        if (!node.Attributes.TryGetValue("state", out var stateParameter) ||
            !stateParameter.TryGetString(out var state))
            state = "";

        if (!node.Attributes.TryGetValue("scale", out var scale) || !scale.TryGetLong(out var scaleValue))
            scaleValue = 1;

        if (!node.Attributes.TryGetValue("tooltip", out var tooltipParameter) ||
            !tooltipParameter.TryGetString(out var tooltip))
            tooltip = null;

        if (!node.Attributes.TryGetValue("offsetX", out var xParameter) ||
            !xParameter.TryGetLong(out var x))
            x = 0;

        if (!node.Attributes.TryGetValue("offsetY", out var yParameter) ||
            !yParameter.TryGetLong(out var y))
            y = 0;

        Texture tex;
        if (_prototypeManager.HasIndex<EntityPrototype>(rawPath))
        {
            var prototype = _prototypeManager.Index<EntityPrototype>(rawPath);
            tex = EntitySystemManager.GetEntitySystem<SpriteSystem>().Frame0(prototype);
        }
        else
        {
            var resPath = new ResPath(rawPath);

            SpriteSpecifier sprite =
                state == "" ? new SpriteSpecifier.Texture(resPath) : new SpriteSpecifier.Rsi(resPath, state);

            tex = EntitySystemManager.GetEntitySystem<SpriteSystem>().Frame0(sprite);
        }

        // Mini custom emoji fill more of the 32x32 canvas than stock emotes — draw ~1.5x smaller.
        var maxInline = rawPath.Contains("_Mini/Interface/Emoji", StringComparison.OrdinalIgnoreCase)
            ? 28f / 1.5f
            : 28f;

        if (!TryDrawIcon(tex,
                scaleValue.Value,
                new Vector2((float) x, (float) y),
                tooltip,
                out var texture,
                maxInline))
            return false;

        control = texture;
        return true;
    }

    // Amour edit start
    /// <summary>
    ///     Проверяет, разрешён ли путь к текстуре.
    ///     Стикеры в чате дополнительно фильтруются на сервере в StickerSanitizerSystem.
    /// </summary>
    private bool IsValidTexturePath(string path)
    {
        if (AllowedTexturePaths.Contains(path))
            return true;

        if (_prototypeManager.HasIndex<EntityPrototype>(path))
            return true;

        foreach (var sticker in _prototypeManager.EnumeratePrototypes<StickerPrototype>())
        {
            if (sticker.TexturePath.ToString() == path)
                return true;
        }

        return IsGameTexturePath(path);
    }

    private static bool IsGameTexturePath(string path)
    {
        var normalized = path.StartsWith("/Textures/")
            ? path
            : $"/Textures/{path.TrimStart('/')}";

        return normalized.StartsWith("/Textures/");
    }
    // Amour edit end
}
