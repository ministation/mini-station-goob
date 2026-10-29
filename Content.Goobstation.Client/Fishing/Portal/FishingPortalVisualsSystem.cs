// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Goobstation.Shared.Fishing.Components;
using Robust.Client.GameObjects;

namespace Content.Goobstation.Client.Fishing.Portal;

/// <summary>
/// Shows the portal open and glowing while a fishing source is connected.
/// </summary>
public sealed partial class FishingPortalVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishingPortalComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FishingPortalComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnStartup(Entity<FishingPortalComponent> ent, ref ComponentStartup args)
    {
        Update(ent);
    }

    private void OnState(Entity<FishingPortalComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        Update(ent);
    }

    private void Update(Entity<FishingPortalComponent> ent)
    {
        var active = ent.Comp.ActiveSource != null;
        _sprite.LayerSetVisible(ent.Owner, ent.Comp.ActiveLayer, active);
        _sprite.LayerSetVisible(ent.Owner, "emissive", active);
    }
}
