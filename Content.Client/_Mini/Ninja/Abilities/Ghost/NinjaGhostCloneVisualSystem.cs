// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

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
        if (ent.Comp.Source is not { } source || source == ent.Owner || !Exists(source))
            return;

        if (!TryComp<SpriteComponent>(source, out var sourceSprite) ||
            !TryComp<SpriteComponent>(ent.Owner, out var cloneSprite))
        {
            return;
        }

        _sprite.CopySprite((source, sourceSprite), (ent.Owner, cloneSprite));
        cloneSprite.Visible = true;
    }
}