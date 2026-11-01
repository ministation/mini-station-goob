// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;

namespace Content.Client._Mini.Fishing.Equipment;

/// <summary>
/// Draws the slotted line, hook and bait on top of the fishing rod sprite, both in the world and in the hands.
/// Also swaps the gene gun sprite depending on what is loaded into it.
/// </summary>
public sealed partial class FishingEquipmentVisualsSystem : EntitySystem
{
    private const string ReelLayer = "reel";
    private const string LineLayer = "line";
    private const string HookLayer = "hook";
    private const string BaitLayer = "bait";

    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishingRodComponent, ComponentStartup>(OnRodStartup);
        SubscribeLocalEvent<FishingRodComponent, EntInsertedIntoContainerMessage>(OnRodItemInserted);
        SubscribeLocalEvent<FishingRodComponent, EntRemovedFromContainerMessage>(OnRodItemRemoved);
        SubscribeLocalEvent<FishingRodComponent, HeldVisualsUpdatedEvent>(OnRodHeldVisualsUpdated);
        SubscribeLocalEvent<FishingBaitCanComponent, ComponentStartup>(OnCanStartup);
        SubscribeLocalEvent<FishingBaitCanComponent, AfterAutoHandleStateEvent>(OnCanState);
        SubscribeLocalEvent<FishingGeneGunComponent, ComponentStartup>(OnGeneGunStartup);
        SubscribeLocalEvent<FishingGeneGunComponent, EntInsertedIntoContainerMessage>(OnGeneGunInserted);
        SubscribeLocalEvent<FishingGeneGunComponent, EntRemovedFromContainerMessage>(OnGeneGunRemoved);
        SubscribeLocalEvent<FishGeneInjectorComponent, ComponentStartup>(OnInjectorStartup);
        SubscribeLocalEvent<FishGeneInjectorComponent, AfterAutoHandleStateEvent>(OnInjectorState);
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
        var line = GetComp<FishingLineComponent>(uid, FishingEquipmentSystem.LineSlotId);
        var hook = GetComp<FishingHookComponent>(uid, FishingEquipmentSystem.HookSlotId);
        var bait = GetComp<FishingBaitComponent>(uid, FishingEquipmentSystem.BaitSlotId);

        UpdateHeldOverlays(uid, line, hook != null, bait != null);

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var rod = (uid, sprite);

        // Rods without the overlay layers (legacy sprites) are left as they are.
        if (!_sprite.LayerMapTryGet(rod, ReelLayer, out var reelLayer, false))
            return;

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

    #region Inhand overlays

    private void OnRodHeldVisualsUpdated(Entity<FishingRodComponent> ent, ref HeldVisualsUpdatedEvent args)
    {
        var line = GetComp<FishingLineComponent>(ent.Owner, FishingEquipmentSystem.LineSlotId);
        var hook = GetComp<FishingHookComponent>(ent.Owner, FishingEquipmentSystem.HookSlotId);
        var bait = GetComp<FishingBaitComponent>(ent.Owner, FishingEquipmentSystem.BaitSlotId);

        UpdateHeldOverlays(ent.Owner, line, hook != null, bait != null);
    }

    /// <summary>
    /// Shows or hides the line and hook layers that the rod added to the sprite of whoever holds it
    /// and tints the line and reel overlays with the color of the slotted line, like the world sprite.
    /// </summary>
    private void UpdateHeldOverlays(EntityUid rod, FishingLineComponent? line, bool hasHook, bool hasBait)
    {
        if (!_container.TryGetContainingContainer(rod, out var container))
            return;

        var holder = container.Owner;

        if (!TryComp<HandsComponent>(holder, out var hands) ||
            !TryComp<SpriteComponent>(holder, out var sprite))
            return;

        var hasAnything = line != null || hasHook || hasBait;
        var lineColor = line?.LineColor ?? Color.Gray;

        foreach (var handId in hands.SortedHands)
        {
            if (!_hands.TryGetHeldItem((holder, hands), handId, out var held) || held != rod)
                continue;

            if (!_hands.TryGetHand((holder, hands), handId, out var hand))
                continue;

            var suffix = hand.Value.Location.ToString().ToLowerInvariant();
            SetHeldRodState(holder, sprite, rod, suffix);
            SetHeldLayerVisible(holder, sprite, $"line_overlay-inhand-{suffix}", hasAnything);
            SetHeldLayerVisible(holder, sprite, $"hook_overlay-inhand-{suffix}", hasHook);
            SetHeldLayerColor(holder, sprite, $"line_overlay-inhand-{suffix}", lineColor);
            SetHeldLayerColor(holder, sprite, $"reel_overlay-inhand-{suffix}", lineColor);
        }
    }

    /// <summary>
    /// Shows the rod art of this rod type in the hand instead of the default one.
    /// </summary>
    private void SetHeldRodState(EntityUid holder, SpriteComponent sprite, EntityUid rod, string suffix)
    {
        if (!TryComp<FishingRodComponent>(rod, out var rodComp) || rodComp.InhandState == null)
            return;

        var key = $"rod-inhand-{suffix}";
        if (!_sprite.LayerMapTryGet((holder, sprite), key, out var layer, false))
            return;

        _sprite.LayerSetRsiState((holder, sprite), layer, $"{rodComp.InhandState}-inhand-{suffix}");
    }

    private void SetHeldLayerVisible(EntityUid holder, SpriteComponent sprite, string key, bool visible)
    {
        var ent = new Entity<SpriteComponent?>(holder, sprite);

        if (_sprite.LayerMapTryGet(ent, key, out var layer, false))
            _sprite.LayerSetVisible(ent, layer, visible);
    }

    private void SetHeldLayerColor(EntityUid holder, SpriteComponent sprite, string key, Color color)
    {
        var ent = new Entity<SpriteComponent?>(holder, sprite);

        if (_sprite.LayerMapTryGet(ent, key, out var layer, false))
            _sprite.LayerSetColor(ent, layer, color);
    }

    #endregion

    private T? GetComp<T>(EntityUid rod, string slotId) where T : Component
    {
        if (!_slots.TryGetSlot(rod, slotId, out var slot) || slot.Item is not { } item)
            return null;

        return TryComp<T>(item, out var comp) ? comp : null;
    }

    #region Bait can

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

    #endregion

    #region Gene gun

    private void OnGeneGunStartup(Entity<FishingGeneGunComponent> ent, ref ComponentStartup args)
    {
        UpdateGeneGun(ent.Owner);
    }

    private void OnGeneGunInserted(Entity<FishingGeneGunComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        UpdateGeneGun(ent.Owner);
    }

    private void OnGeneGunRemoved(Entity<FishingGeneGunComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        UpdateGeneGun(ent.Owner);
    }

    private void UpdateGeneGun(EntityUid uid)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite) || !_sprite.LayerExists((uid, sprite), 0))
            return;

        var state = "fish_gun";

        if (_slots.TryGetSlot(uid, FishingGeneGunComponent.InjectorSlotId, out var slot) && slot.Item is { } item)
            state = HasComp<FishGeneInjectorComponent>(item) ? "fish_gun_inject" : "fish_gun_extract";

        _sprite.LayerSetRsiState((uid, sprite), 0, state);
    }

    #endregion

    #region Trait injector

    private void OnInjectorStartup(Entity<FishGeneInjectorComponent> ent, ref ComponentStartup args)
    {
        UpdateInjector(ent.Owner);
    }

    private void OnInjectorState(Entity<FishGeneInjectorComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateInjector(ent.Owner);
    }

    /// <summary>
    /// Uses the injector sprite generated for the stored trait, or the empty one when nothing is stored.
    /// </summary>
    private void UpdateInjector(EntityUid uid)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite) ||
            !TryComp<FishGeneInjectorComponent>(uid, out var injector) ||
            !_sprite.LayerExists((uid, sprite), 0))
            return;

        var state = "fish_trait_injector";

        if (injector.Trait is { } trait)
        {
            var traited = $"fish_trait_injector_{trait.Id}";
            if (sprite.BaseRSI?.TryGetState(traited, out _) == true)
                state = traited;
        }

        _sprite.LayerSetRsiState((uid, sprite), 0, state);
    }

    #endregion
}
