// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.Ninja.Appearance;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Containers;

namespace Content.Server._Mini.Ninja.Systems;

/// <summary>
/// Server side of the suit personalisation: stores the selected colour/style on the suit and
/// freezes the look of the suit parts when they are dropped. The sprites themselves are
/// recoloured on the client. Hair stays hidden while the helmet is worn, even when only the
/// scarf is shown: the suit must not expose the ninja's identity.
/// </summary>
public sealed class NinjaAppearanceSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaAppearanceItemComponent, EntParentChangedMessage>(OnItemReparented);
        SubscribeLocalEvent<NinjaAppearanceComponent, GotUnequippedEvent>(OnSuitUnequipped);
    }

    /// <summary>
    /// Applies the selected appearance to the suit.
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
