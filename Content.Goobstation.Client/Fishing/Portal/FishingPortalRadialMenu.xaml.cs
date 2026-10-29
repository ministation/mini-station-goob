// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Goobstation.Shared.Fishing;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Utility;

namespace Content.Goobstation.Client.Fishing.Portal;

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
        var button = new RadialMenuButton
        {
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

        button.OnButtonUp += _ => OnSelected?.Invoke(sourceId);
        main.AddChild(button);
    }
}