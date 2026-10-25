// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.Ninja.Appearance;
using Content.Shared.Clothing.Components;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Containers;

namespace Content.Server._Mini.Ninja.Systems;

/// <summary>
/// Server side of the suit personalisation: stores the selected colour/style on the suit,
/// freezes the look of the suit parts when they are dropped and restores the hair layers when the helmet is hidden.
/// The sprites themselves are recoloured on the client.
/// </summary>
public sealed class NinjaAppearanceSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaAppearanceItemComponent, EntParentChangedMessage>(OnItemReparented);
        SubscribeLocalEvent<NinjaAppearanceComponent, GotUnequippedEvent>(OnSuitUnequipped);
    }

    /// <summary>
    /// Applies the selected appearance to the suit and updates the helmet hair layers.
    /// </summary>
    public void ApplyAppearance(EntityUid suit, EntityUid wearer, NinjaColorway colorway, NinjaStyle style, bool helmetShown)
    {
        var appearance = EnsureComp<NinjaAppearanceComponent>(suit);
        appearance.Colorway = colorway;
        appearance.Style = style;
        appearance.HelmetShown = helmetShown;
        appearance.ScarfShown = !helmetShown;
        Dirty(suit, appearance);

        if (TryComp<NinjaAppearanceItemComponent>(suit, out var suitItem))
        {
            suitItem.FrozenColor = colorway;
            suitItem.FrozenStyle = style;
            Dirty(suit, suitItem);
        }

        UpdateHelmetLayers(suit, wearer, helmetShown);
    }

    private void UpdateHelmetLayers(EntityUid suit, EntityUid wearer, bool shown)
    {
        if (!wearer.IsValid() || !TryComp<HumanoidAppearanceComponent>(wearer, out var humanoid))
            return;

        foreach (var item in EnumerateWearerItems(wearer))
        {
            if (!TryComp<NinjaAppearanceItemComponent>(item, out var itemComp) ||
                itemComp.ItemType != NinjaAppearanceItemType.Helmet)
            {
                continue;
            }

            if (shown)
                RestoreHideLayerClothing(wearer, item, itemComp, humanoid.HideLayersOnEquip);
            else
                RemoveHideLayerClothing(wearer, item, itemComp, humanoid.HideLayersOnEquip);

            return;
        }
    }

    private void RemoveHideLayerClothing(EntityUid wearer, EntityUid helmet,
        NinjaAppearanceItemComponent comp, HashSet<HumanoidVisualLayers> hideable)
    {
        if (comp.SavedHideLayers != null || !TryComp<HideLayerClothingComponent>(helmet, out var hide))
            return;

        var inSlot = TryComp<ClothingComponent>(helmet, out var clothing)
            ? clothing.InSlotFlag ?? SlotFlags.NONE
            : SlotFlags.NONE;

        if (inSlot == SlotFlags.NONE)
            return;

        comp.SavedHideLayers = new Dictionary<HumanoidVisualLayers, SlotFlags>(hide.Layers);
        ToggleHiddenLayers(wearer, hide.Layers, hideable, inSlot, hidden: false);
        RemComp<HideLayerClothingComponent>(helmet);
    }

    private void RestoreHideLayerClothing(EntityUid wearer, EntityUid helmet,
        NinjaAppearanceItemComponent comp, HashSet<HumanoidVisualLayers> hideable)
    {
        if (comp.SavedHideLayers is not { } saved)
            return;

        var inSlot = TryComp<ClothingComponent>(helmet, out var clothing)
            ? clothing.InSlotFlag ?? SlotFlags.NONE
            : SlotFlags.NONE;

        if (inSlot == SlotFlags.NONE)
            return;

        var hide = EnsureComp<HideLayerClothingComponent>(helmet);
        hide.Layers = saved;
        Dirty(helmet, hide);
        comp.SavedHideLayers = null;

        ToggleHiddenLayers(wearer, saved, hideable, inSlot, hidden: true);
    }

    private void ToggleHiddenLayers(EntityUid wearer, Dictionary<HumanoidVisualLayers, SlotFlags> layers,
        HashSet<HumanoidVisualLayers> hideable, SlotFlags inSlot, bool hidden)
    {
        foreach (var (layer, validSlots) in layers)
        {
            if (hideable.Contains(layer) && validSlots.HasFlag(inSlot))
                _humanoid.SetLayerVisibility(wearer, layer, visible: !hidden, source: inSlot);
        }
    }

    private void OnSuitUnequipped(Entity<NinjaAppearanceComponent> ent, ref GotUnequippedEvent args)
    {
        foreach (var item in EnumerateWearerItems(args.Equipee))
        {
            if (item == ent.Owner || !TryComp<NinjaAppearanceItemComponent>(item, out var itemComp))
                continue;

            if (FindSuit(item) != null)
                continue;

            if (itemComp.FrozenColor != ent.Comp.Colorway || itemComp.FrozenStyle != ent.Comp.Style)
            {
                itemComp.FrozenColor = ent.Comp.Colorway;
                itemComp.FrozenStyle = ent.Comp.Style;
                Dirty(item, itemComp);
            }
        }
    }

    private void OnItemReparented(Entity<NinjaAppearanceItemComponent> ent, ref EntParentChangedMessage args)
    {
        if (FindSuit(args.Transform.ParentUid) is { } suit)
        {
            if (ent.Comp.FrozenColor == suit.Colorway && ent.Comp.FrozenStyle == suit.Style)
                return;

            ent.Comp.FrozenColor = suit.Colorway;
            ent.Comp.FrozenStyle = suit.Style;
            Dirty(ent.Owner, ent.Comp);
            return;
        }

        if (TryComp<NinjaAppearanceComponent>(ent.Owner, out var own) &&
            (ent.Comp.FrozenColor != own.Colorway || ent.Comp.FrozenStyle != own.Style))
        {
            ent.Comp.FrozenColor = own.Colorway;
            ent.Comp.FrozenStyle = own.Style;
            Dirty(ent.Owner, ent.Comp);
        }
    }

    private NinjaAppearanceComponent? FindSuit(EntityUid? root)
    {
        if (root is not { } parent || !parent.IsValid())
            return null;

        while (parent.IsValid() && Exists(parent))
        {
            if (TryComp<NinjaAppearanceComponent>(parent, out var comp))
                return comp;

            var enumerator = _inventory.GetSlotEnumerator(parent);
            while (enumerator.NextItem(out var worn))
            {
                if (TryComp<NinjaAppearanceComponent>(worn, out var wornComp))
                    return wornComp;
            }

            parent = Transform(parent).ParentUid;
        }

        return null;
    }

    private IEnumerable<EntityUid> EnumerateWearerItems(EntityUid wearer)
    {
        if (!TryComp<InventoryComponent>(wearer, out var inventory))
            yield break;

        var enumerator = _inventory.GetSlotEnumerator((wearer, inventory));
        while (enumerator.NextItem(out var item))
        {
            yield return item;
            foreach (var contained in EnumerateContainedItems(item))
                yield return contained;
        }
    }

    private IEnumerable<EntityUid> EnumerateContainedItems(EntityUid item)
    {
        if (!TryComp<ContainerManagerComponent>(item, out var containerManager))
            yield break;

        foreach (var container in containerManager.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities)
            {
                yield return contained;
                foreach (var inner in EnumerateContainedItems(contained))
                    yield return inner;
            }
        }
    }
}
