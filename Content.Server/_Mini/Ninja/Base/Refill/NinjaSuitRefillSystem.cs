// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using Content.Shared._Mini.Ninja.Base.Refill;
using Content.Shared.Actions.Components;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Interaction;
using Content.Shared.Ninja.Components;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.Ninja.Base.Refill;

/// <summary>
/// Refills limited charge suit actions when the wearer uses a matching stack on the suit.
/// </summary>
public sealed class NinjaSuitRefillSystem : EntitySystem
{
    [Dependency] private readonly SharedChargesSystem _charges = default!;
    [Dependency] private readonly SharedStackSystem _stacks = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaSuitRefillComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<SpaceNinjaComponent, InteractUsingEvent>(OnNinjaInteractUsing);
    }

    /// <summary>
    /// Also allow feeding the suit by using a material stack on the ninja wearing it.
    /// </summary>
    private void OnNinjaInteractUsing(Entity<SpaceNinjaComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Suit is not { } suit)
            return;

        if (!TryComp<NinjaSuitRefillComponent>(suit, out var refill))
            return;

        if (TryRefill((suit, refill), args.Used, args.User))
            args.Handled = true;
    }

    private void OnInteractUsing(Entity<NinjaSuitRefillComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryRefill(ent, args.Used, args.User))
            args.Handled = true;
    }

    private bool TryRefill(Entity<NinjaSuitRefillComponent> ent, EntityUid used, EntityUid user)
    {
        if (!TryComp<StackComponent>(used, out var stack))
            return false;

        foreach (var (actionProto, cost) in ent.Comp.ActionMaterials)
        {
            if (stack.StackTypeId != cost.Stack)
                continue;

            if (!TryFindAction(ent.Owner, actionProto, out var actionUid))
                continue;

            if (!TryComp<LimitedChargesComponent>(actionUid, out var charges))
                continue;

            if (charges.LastCharges >= charges.MaxCharges)
                continue;

            if (stack.Count < cost.Amount)
                continue;

            if (!_stacks.TryUse(used, cost.Amount))
                continue;

            _charges.AddCharges((actionUid, charges), 1);
            _popup.PopupEntity(Loc.GetString("ninja-suit-refill", ("action", MetaData(actionUid).EntityName)), user, user, PopupType.Small);
            return true;
        }

        return false;
    }

    private bool TryFindAction(EntityUid suitUid, EntProtoId actionProto, [NotNullWhen(true)] out EntityUid actionUid)
    {
        actionUid = default;

        if (!TryComp<ActionsContainerComponent>(suitUid, out var container))
            return false;

        foreach (var contained in container.Container.ContainedEntities)
        {
            if (MetaData(contained).EntityPrototype?.ID == actionProto.Id)
            {
                actionUid = contained;
                return true;
            }
        }

        return false;
    }
}