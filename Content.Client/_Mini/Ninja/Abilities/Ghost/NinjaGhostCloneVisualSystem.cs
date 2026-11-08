// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

// SPDX-FileCopyrightText: 2026 Mini Station
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared._Mini.Ninja.Abilities.Ghost;
using Robust.Client.GameObjects;

namespace Content.Client._Mini.Ninja.Abilities.Ghost;

/// <summary>
/// Draws holographic clones with the sprite of the ninja they copy.
/// </summary>
public sealed class NinjaGhostCloneVisualSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaGhostCloneVisualComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnState(Entity<NinjaGhostCloneVisualComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Comp.Source is not { } sourceNet ||
            !TryGetEntity(sourceNet, out var source) ||
            source == ent.Owner)
            return;

        if (!TryComp<SpriteComponent>(source.Value, out var sourceSprite) ||
            !TryComp<SpriteComponent>(ent.Owner, out var cloneSprite))
        {
            return;
        }

        _sprite.CopySprite((source.Value, sourceSprite), (ent.Owner, cloneSprite));
        cloneSprite.Visible = true;
    }
}