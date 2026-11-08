// SPDX-License-Identifier: LicenseRef-MiniStation-1.0
// Mechanics reference: tgstation/tgstation (code/modules/fishing).

using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Prototypes;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Mini.Fishing.Portal;

/// <summary>
/// Shows the portal open and glowing while a fishing source is connected.
/// </summary>
public sealed partial class FishingPortalVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IPrototypeManager _proto = default!;

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
        FishingSourcePrototype? source = null;
        if (ent.Comp.ActiveSource is { } id)
            _proto.TryIndex(id, out source);

        var active = source != null;
        _sprite.LayerSetVisible(ent.Owner, ent.Comp.ActiveLayer, active);
        _sprite.LayerSetVisible(ent.Owner, ent.Comp.OverlayLayer, active);
        _sprite.LayerSetVisible(ent.Owner, "emissive", active);

        if (source != null)
            _sprite.LayerSetRsiState(ent.Owner, ent.Comp.OverlayLayer, source.OverlayState);
    }
}
