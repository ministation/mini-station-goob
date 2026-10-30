// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Mini.Fishing;

/// <summary>
/// Opens and closes the treasure chest lid and shows the rolled loot above it. The server only
/// sends the loot prototype id, the sprite itself is resolved here from the prototype.
/// </summary>
public sealed partial class FishingTreasureChestVisualsSystem : EntitySystem
{
    private const string LidLayer = "lid";

    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishingTreasureChestComponent, ComponentStartup>(OnChestStartup);
        SubscribeLocalEvent<FishingTreasureChestComponent, AfterAutoHandleStateEvent>(OnChestState);
        SubscribeLocalEvent<FishingTreasureChestPrizeComponent, ComponentStartup>(OnPrizeStartup);
        SubscribeLocalEvent<FishingTreasureChestPrizeComponent, AfterAutoHandleStateEvent>(OnPrizeState);
    }

    private void OnChestStartup(Entity<FishingTreasureChestComponent> ent, ref ComponentStartup args)
    {
        UpdateChest(ent);
    }

    private void OnChestState(Entity<FishingTreasureChestComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateChest(ent);
    }

    private void UpdateChest(Entity<FishingTreasureChestComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite) ||
            !_sprite.LayerMapTryGet((ent, sprite), LidLayer, out var layer, false))
            return;

        var state = ent.Comp.State == FishingTreasureChestState.Closed ? "crate_door" : "crate_open";
        _sprite.LayerSetRsiState((ent, sprite), layer, state);
    }

    private void OnPrizeStartup(Entity<FishingTreasureChestPrizeComponent> ent, ref ComponentStartup args)
    {
        UpdatePrize(ent);
    }

    private void OnPrizeState(Entity<FishingTreasureChestPrizeComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdatePrize(ent);
    }

    private void UpdatePrize(Entity<FishingTreasureChestPrizeComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (GetSprite(ent.Comp.Prize) is not { } spec)
        {
            _sprite.SetVisible((ent, sprite), false);
            return;
        }

        _sprite.SetVisible((ent, sprite), true);
        _sprite.LayerSetTexture((ent, sprite), 0, _sprite.Frame0(spec));
    }

    private SpriteSpecifier? GetSprite(EntProtoId? id)
    {
        return id is { } protoId ? new SpriteSpecifier.EntityPrototype(protoId.Id) : null;
    }
}
