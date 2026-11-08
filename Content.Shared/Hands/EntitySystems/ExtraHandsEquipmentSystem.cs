// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Hands.Components;
using Content.Shared.Inventory.Events;

namespace Content.Shared.Hands.EntitySystems;

public sealed class ExtraHandsEquipmentSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExtraHandsEquipmentComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<ExtraHandsEquipmentComponent, GotUnequippedEvent>(OnUnequipped);
    }

    private void OnEquipped(Entity<ExtraHandsEquipmentComponent> ent, ref GotEquippedEvent args)
    {
        if (!TryComp<HandsComponent>(args.Equipee, out var handsComp))
            return;

        foreach (var (handName, hand) in ent.Comp.Hands)
        {
            // add the NetEntity id to the container name to prevent multiple items with this component from conflicting
            var handId = $"{GetNetEntity(ent.Owner).Id}-{handName}";
            _hands.AddHand((args.Equipee, handsComp), handId, hand.Location);
        }
    }

    private void OnUnequipped(Entity<ExtraHandsEquipmentComponent> ent, ref GotUnequippedEvent args)
    {
        // Mini: the pack is also unequipped while its wearer is deleted (round-end cleanup). The wearer's
        // hands and their contents are already terminating then; removing them here makes the engine log
        // "Attempting to remove an entity from a container while it is terminating" and a double delete.
        if (TerminatingOrDeleted(args.Equipee))
            return;

        if (!TryComp<HandsComponent>(args.Equipee, out var handsComp))
            return;

        foreach (var handName in ent.Comp.Hands.Keys)
        {
            // add the NetEntity id to the container name to prevent multiple items with this component from conflicting
            var handId = $"{GetNetEntity(ent.Owner).Id}-{handName}";
            _hands.RemoveHand((args.Equipee, handsComp), handId);
        }
    }
}
