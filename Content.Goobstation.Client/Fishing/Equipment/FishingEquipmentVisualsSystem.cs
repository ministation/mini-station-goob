// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Goobstation.Shared.Fishing.Components;
using Content.Goobstation.Shared.Fishing.Systems;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;

namespace Content.Goobstation.Client.Fishing.Equipment;

/// <summary>
/// Draws the slotted line, hook and bait on top of the fishing rod sprite.
/// </summary>
public sealed partial class FishingEquipmentVisualsSystem : EntitySystem
{
    private const string ReelLayer = "reel";
    private const string LineLayer = "line";
    private const string HookLayer = "hook";
    private const string BaitLayer = "bait";

    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishingRodComponent, ComponentStartup>(OnRodStartup);
        SubscribeLocalEvent<FishingRodComponent, EntInsertedIntoContainerMessage>(OnRodItemInserted);
        SubscribeLocalEvent<FishingRodComponent, EntRemovedFromContainerMessage>(OnRodItemRemoved);
        SubscribeLocalEvent<FishingBaitCanComponent, ComponentStartup>(OnCanStartup);
        SubscribeLocalEvent<FishingBaitCanComponent, AfterAutoHandleStateEvent>(OnCanState);
    }

    private void OnRodStartup(Entity<FishingRodComponent> ent, ref ComponentStartup args)
    {
        UpdateRod(ent);
    }

    private void OnRodItemInserted(Entity<FishingRodComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        UpdateRod(ent);
    }

    private void OnRodItemRemoved(Entity<FishingRodComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        UpdateRod(ent);
    }

    private void UpdateRod(EntityUid uid)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var rod = (uid, sprite);

        // Rods without the overlay layers (legacy sprites) are left as they are.
        if (!_sprite.LayerMapTryGet(rod, ReelLayer, out var reelLayer, false))
            return;

        var line = GetComp<FishingLineComponent>(uid, FishingEquipmentSystem.LineSlotId);
        var hook = GetComp<FishingHookComponent>(uid, FishingEquipmentSystem.HookSlotId);
        var bait = GetComp<FishingBaitComponent>(uid, FishingEquipmentSystem.BaitSlotId);

        var lineColor = line?.LineColor ?? Color.Gray;
        _sprite.LayerSetColor(rod, reelLayer, lineColor);

        var hasLine = line != null;
        _sprite.LayerSetVisible(rod, LineLayer, hasLine || hook != null || bait != null);
        _sprite.LayerSetColor(rod, LineLayer, lineColor);

        var hasHook = hook != null;
        _sprite.LayerSetVisible(rod, HookLayer, hasHook);
        if (hasHook)
            _sprite.LayerSetRsiState(rod, HookLayer, hook!.RodOverlayState);

        var hasBait = bait != null;
        _sprite.LayerSetVisible(rod, BaitLayer, hasBait);
        if (hasBait)
            _sprite.LayerSetRsiState(rod, BaitLayer, bait!.RodOverlayState);
    }

    private T? GetComp<T>(EntityUid rod, string slotId) where T : Component
    {
        if (!_slots.TryGetSlot(rod, slotId, out var slot) || slot.Item is not { } item)
            return null;

        return TryComp<T>(item, out var comp) ? comp : null;
    }

    private void OnCanStartup(Entity<FishingBaitCanComponent> ent, ref ComponentStartup args)
    {
        UpdateCan(ent);
    }

    private void OnCanState(Entity<FishingBaitCanComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateCan(ent);
    }

    private void UpdateCan(EntityUid uid)
    {
        if (!TryComp<FishingBaitCanComponent>(uid, out var can) ||
            !TryComp<SpriteComponent>(uid, out var sprite) ||
            !_sprite.LayerExists((uid, sprite), 0))
            return;

        var state = can.UsesLeft <= 0
            ? "bait_can_empty"
            : can.UsesLeft < can.MaxUses
                ? "bait_can_open"
                : "bait_can";

        _sprite.LayerSetRsiState((uid, sprite), 0, state);
    }
}
