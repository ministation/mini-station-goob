// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing/fishing_equipment.dm, AGPL-3.0-or-later).

using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._Mini.Fishing;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client._Mini.Fishing.Capsule;

public sealed partial class FishingSpotCapsuleRadialMenu : RadialMenu
{
    [Dependency] private IEntitySystemManager _systems = default!;

    private readonly SpriteSystem _sprite;

    public event Action<string?>? OnSelected;

    public FishingSpotCapsuleRadialMenu()
    {
        IoCManager.InjectDependencies(this);
        RobustXamlLoader.Load(this);

        _sprite = _systems.GetEntitySystem<SpriteSystem>();
    }

    public void SetOptions(List<FishingSpotCapsuleOptionState> options)
    {
        var main = FindControl<RadialContainer>("Main");
        if (main == null)
            return;

        main.DisposeAllChildren();

        foreach (var option in options)
            AddOption(main, option);
    }

    private void AddOption(RadialContainer main, FishingSpotCapsuleOptionState option)
    {
        var button = new RadialMenuButton
        {
            SetSize = new Vector2(64, 64),
            ToolTip = string.IsNullOrEmpty(option.Description)
                ? option.Name
                : $"{option.Name}\n{option.Description}",
        };

        button.AddChild(new TextureRect
        {
            VerticalAlignment = VAlignment.Center,
            HorizontalAlignment = HAlignment.Center,
            Texture = _sprite.Frame0(option.Icon),
            TextureScale = new Vector2(2f, 2f),
        });

        button.OnButtonUp += _ => OnSelected?.Invoke(option.SourceId);
        main.AddChild(button);
    }
}
