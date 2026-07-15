using System.Diagnostics.CodeAnalysis;
using Content.Shared.StatusIcon;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Goobstation.UIKit.UserInterface.RichText;

/// <summary>
/// Mini/Goob icon tag:
/// - <c>[icon="/Textures/_Mini/Interface/AdminIcons/..."]</c> (AHelp rank icons)
/// - <c>[icon src="JobIconId" tooltip="..."]</c> (local/radio job icons)
/// </summary>
public sealed class IconTag : IMarkupTagHandler
{
    private const string AdminIconsPrefix = "/Textures/_Mini/Interface/AdminIcons/";

    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystem = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    private SpriteSystem? _spriteSystem;

    public string Name => "icon";

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        control = null;

        // AHelp / admin ranks: [icon="/Textures/..."]
        if (node.Value.StringValue is { } path)
        {
            if (path.StartsWith(AdminIconsPrefix, StringComparison.Ordinal))
            {
                if (!_resourceCache.TryGetResource<TextureResource>(path, out var texture))
                    return false;

                control = CreateIcon(texture.Texture);
                return true;
            }

            var texturePath = path.StartsWith('/') ? path : $"/{path}";
            if (texturePath.StartsWith("/Textures/", StringComparison.Ordinal)
                && _resourceCache.TryGetResource<TextureResource>(texturePath, out var directTexture))
            {
                control = CreateIcon(directTexture.Texture);
                return true;
            }
        }

        // Chat job icons: [icon src="JobIconNoId" tooltip="Assistant"]
        if (!node.Attributes.TryGetValue("src", out var id) || id.StringValue == null)
            return false;

        _spriteSystem ??= _entitySystem.GetEntitySystem<SpriteSystem>();
        if (!_prototype.TryIndex<JobIconPrototype>(id.StringValue, out var iconPrototype))
            return false;

        var icon = CreateIcon(_spriteSystem.Frame0(iconPrototype.Icon));

        if (node.Attributes.TryGetValue("tooltip", out var tooltip) && tooltip.StringValue != null)
            icon.ToolTip = tooltip.StringValue;

        control = icon;
        return true;
    }

    private static TextureRect CreateIcon(Texture texture)
    {
        return new TextureRect
        {
            Texture = texture,
            SetWidth = 20,
            SetHeight = 20,
            MaxSize = new System.Numerics.Vector2(20, 20),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            VerticalAlignment = Control.VAlignment.Bottom,
            MouseFilter = Control.MouseFilterMode.Stop,
        };
    }
}
