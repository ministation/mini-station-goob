// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Goobstation.Shared.Fishing.Components;
using Content.Goobstation.Shared.Fishing.Prototypes;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Client.Fishing;

/// <summary>
/// Swaps a fish icon to its dead sprite when it dies.
/// </summary>
public sealed partial class FishVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishComponent, ComponentStartup>(OnFishStartup);
        SubscribeLocalEvent<FishComponent, AfterAutoHandleStateEvent>(OnFishState);
    }

    private void OnFishStartup(Entity<FishComponent> ent, ref ComponentStartup args)
    {
        UpdateSprite(ent);
    }

    private void OnFishState(Entity<FishComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateSprite(ent);
    }

    private void UpdateSprite(Entity<FishComponent> ent)
    {
        if (ent.Comp.Fish is not { } id || !_proto.TryIndex(id, out FishPrototype? proto))
            return;

        var sprite = ent.Comp.Alive ? proto.Sprite : proto.DeadSprite ?? proto.Sprite;
        _sprite.LayerSetSprite(ent.Owner, 0, sprite);
    }
}
