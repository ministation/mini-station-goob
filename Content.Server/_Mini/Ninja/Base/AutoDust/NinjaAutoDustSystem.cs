// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Linq;
using Content.Server.Body.Systems;
using Content.Shared._Mini.Ninja.Base.AutoDust;
using Content.Shared.Clothing;
using Content.Shared.Gibbing;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.Ninja.Base.AutoDust;

/// <summary>
/// Turns the ninja body into dust depending on the selected auto-dust mode.
/// </summary>
public sealed class NinjaAutoDustSystem : SharedNinjaAutoDustSystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaAutoDustComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<NinjaAutoDustComponent, ClothingGotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<NinjaAutoDustComponent, NinjaToggleAutoDustEvent>(OnToggle);
        SubscribeLocalEvent<NinjaAutoDustMarkerComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnEquipped(Entity<NinjaAutoDustComponent> ent, ref ClothingGotEquippedEvent args)
    {
        var marker = EnsureComp<NinjaAutoDustMarkerComponent>(args.Wearer);
        marker.Suit = ent.Owner;
    }

    private void OnUnequipped(Entity<NinjaAutoDustComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        if (TryComp<NinjaAutoDustMarkerComponent>(args.Wearer, out var marker) &&
            (marker.Suit == ent.Owner || marker.Suit == null))
        {
            RemComp<NinjaAutoDustMarkerComponent>(args.Wearer);
        }
    }

    private void OnToggle(Entity<NinjaAutoDustComponent> ent, ref NinjaToggleAutoDustEvent args)
    {
        args.Handled = true;

        ent.Comp.Mode = ent.Comp.Mode switch
        {
            NinjaDustMode.Off => NinjaDustMode.Crit,
            NinjaDustMode.Crit => NinjaDustMode.Dead,
            _ => NinjaDustMode.Off,
        };

        var message = ent.Comp.Mode switch
        {
            NinjaDustMode.Crit => "ninja-auto-dust-crit",
            NinjaDustMode.Dead => "ninja-auto-dust-dead",
            _ => "ninja-auto-dust-off",
        };

        _popup.PopupEntity(Loc.GetString(message), args.Performer, args.Performer, PopupType.Medium);
        Dirty(ent);
    }

    private void OnMobStateChanged(Entity<NinjaAutoDustMarkerComponent> ent, ref MobStateChangedEvent args)
    {
        if (ent.Comp.Suit is not { } suit || !TryComp<NinjaAutoDustComponent>(suit, out var dust))
            return;

        var shouldDust = args.NewMobState switch
        {
            MobState.Dead => dust.Mode != NinjaDustMode.Off,
            MobState.Critical => dust.Mode == NinjaDustMode.Crit,
            _ => false,
        };

        if (!shouldDust)
            return;

        var attempt = new NinjaAutoDustAttemptEvent(ent.Owner, suit, dust);
        RaiseLocalEvent(ent.Owner, ref attempt);

        if (attempt.Cancelled)
            return;

        Dust(ent.Owner, dust);
    }

    private void Dust(EntityUid body, NinjaAutoDustComponent dust)
    {
        var coords = _transform.GetMapCoordinates(body);
        Spawn(dust.DustEffect, coords);

        if (dust.DeleteItems)
            DeleteEquipment(body, dust);

        // GibBody handles the mind transfer to a ghost and removes the body safely.
        _body.GibBody(body);
    }

    private void DeleteEquipment(EntityUid body, NinjaAutoDustComponent dust)
    {
        foreach (var item in _inventory.GetHandOrInventoryEntities(body).ToList())
        {
            if (TerminatingOrDeleted(item))
                continue;

            if (_tags.HasAnyTag(item, dust.HighRiskTags))
            {
                _container.TryRemoveFromContainer(item);
                continue;
            }

            QueueDel(item);
        }
    }
}
