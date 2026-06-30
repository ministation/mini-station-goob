using System;
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

        if (node.Value.StringValue is { } path && path.StartsWith(AdminIconsPrefix, StringComparison.Ordinal))
        {
            if (!_resourceCache.TryGetResource<TextureResource>(path, out var texture))
                return false;

            control = CreateIcon(texture.Texture);
            return true;
        }

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
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            VerticalAlignment = Control.VAlignment.Bottom,
            MouseFilter = Control.MouseFilterMode.Stop,
        };
    }
}
