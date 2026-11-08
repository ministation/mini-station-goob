// SPDX-License-Identifier: LicenseRef-MiniStation-1.0
// Mechanics reference: tgstation/tgstation (code/modules/fishing).

using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._Mini.Fishing;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Utility;

namespace Content.Client._Mini.Fishing.Portal;

public sealed partial class FishingPortalRadialMenu : RadialMenu
{
    [Dependency] private IEntitySystemManager _systems = default!;

    private readonly SpriteSystem _sprite;

    public event Action<string?>? OnSelected;

    public FishingPortalRadialMenu()
    {
        IoCManager.InjectDependencies(this);
        RobustXamlLoader.Load(this);

        _sprite = _systems.GetEntitySystem<SpriteSystem>();
    }

    public void SetOptions(List<FishingPortalOption> options)
    {
        var main = FindControl<RadialContainer>("Main");
        if (main == null)
            return;

        main.DisposeAllChildren();

        foreach (var option in options)
            AddOption(main, option.Icon, option.Name, option.SourceId);

        AddOption(
            main,
            new SpriteSpecifier.Rsi(new ResPath("_Goobstation/Interface/Fishing/radial.rsi"), "minus_sign"),
            Loc.GetString("fishing-portal-turn-off"),
            null);
    }

    private void AddOption(RadialContainer main, SpriteSpecifier icon, string name, string? sourceId)
    {
        // Sector buttons and the RadialMenuButton style class are what the construction
        // radial menus use, so the portal gets the same look and hover highlight.
        var button = new RadialMenuButtonWithSector
        {
            StyleClasses = { "RadialMenuButton" },
            DrawBorder = true,
            DrawBackground = true,
            SetSize = new Vector2(64, 64),
            ToolTip = name,
        };

        button.AddChild(new TextureRect
        {
            VerticalAlignment = VAlignment.Center,
            HorizontalAlignment = HAlignment.Center,
            Texture = _sprite.Frame0(icon),
            TextureScale = new Vector2(2f, 2f),
        });

        button.OnPressed += _ => OnSelected?.Invoke(sourceId);
        main.AddChild(button);
    }
}