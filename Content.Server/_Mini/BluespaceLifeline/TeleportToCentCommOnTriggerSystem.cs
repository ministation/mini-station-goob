// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Shuttles.Components;
using Content.Shared._Mini.BluespaceLifeline;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Inventory;
using Content.Shared.PDA;
using Content.Shared.Trigger;
using Content.Shared.Warps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Mini.BluespaceLifeline;

public sealed class TeleportToCentCommOnTriggerSystem : EntitySystem
{
    private static readonly HashSet<string> KeepSlots = new(StringComparer.Ordinal)
    {
        "jumpsuit",
        "shoes",
        "id",
    };

    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedSubdermalImplantSystem _implants = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TeleportToCentCommOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<TeleportToCentCommOnTriggerComponent> ent, ref TriggerEvent args)
    {
        var host = Transform(ent).ParentUid;
        if (TryComp(ent, out SubdermalImplantComponent? implant) && implant.ImplantedEntity is { } implanted)
            host = implanted;

        if (!EntityManager.EntityExists(host))
            return;

        if (!TryFindCentCommWarp(out var coords))
        {
            Log.Warning($"Bluespace lifeline: no CentComm warp for {ToPrettyString(host)}");
            return;
        }

        StripExceptEssentials(host);
        _transform.SetCoordinates(host, coords);

        // One-shot: remove and delete the implant (no leftover AnomalyCore).
        if (TryComp(host, out ImplantedComponent? implantedComp))
            _implants.ForceRemove((host, implantedComp), ent.Owner);
        else
            QueueDel(ent.Owner);

        args.Handled = true;
    }

    private void StripExceptEssentials(EntityUid host)
    {
        if (_inventory.TryGetContainerSlotEnumerator(host, out var slots))
        {
            while (slots.MoveNext(out var slot))
            {
                if (slot.ContainedEntity is not { } item)
                    continue;

                if (KeepSlots.Contains(slot.ID))
                    continue;

                if (HasComp<PdaComponent>(item))
                    continue;

                // Delete equipped gear outright — do not drop it on the floor.
                QueueDel(item);
            }
        }

        foreach (var held in _hands.EnumerateHeld(host).ToArray())
            QueueDel(held);
    }

    private bool TryFindCentCommWarp(out EntityCoordinates coords)
    {
        EntityUid? centcommGrid = null;
        var ccQuery = EntityQueryEnumerator<StationCentcommComponent>();
        while (ccQuery.MoveNext(out _, out var cc))
        {
            if (cc.Entity is { } grid && EntityManager.EntityExists(grid))
            {
                centcommGrid = grid;
                break;
            }
        }

        var warps = EntityQueryEnumerator<WarpPointComponent, TransformComponent>();
        while (warps.MoveNext(out _, out var warp, out var xform))
        {
            if (centcommGrid != null &&
                xform.GridUid != centcommGrid &&
                xform.ParentUid != centcommGrid)
                continue;

            if (warp.Location != null &&
                warp.Location.Contains("CentCom", StringComparison.OrdinalIgnoreCase))
            {
                coords = xform.Coordinates;
                return true;
            }
        }

        if (centcommGrid != null)
        {
            warps = EntityQueryEnumerator<WarpPointComponent, TransformComponent>();
            while (warps.MoveNext(out _, out _, out var xform))
            {
                if (xform.GridUid != centcommGrid && xform.ParentUid != centcommGrid)
                    continue;

                coords = xform.Coordinates;
                return true;
            }

            if (TryComp(centcommGrid.Value, out MapGridComponent? mapGrid))
            {
                coords = new EntityCoordinates(centcommGrid.Value, mapGrid.LocalAABB.Center);
                return true;
            }
        }

        coords = default;
        return false;
    }
}

