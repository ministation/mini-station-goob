// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Emp;
using Content.Server.Ninja.Events;
using Content.Shared.Emp;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Ninja.Components;
using Content.Shared.Ninja.Systems;
using Content.Shared.Power.Components;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Robust.Shared.Containers;

namespace Content.Server.Ninja.Systems;

/// <summary>
/// Handles power cell upgrading and actions.
/// TODO: Move all of this to shared and predict it
/// </summary>
public sealed class NinjaSuitSystem : SharedNinjaSuitSystem
{
    [Dependency] private readonly SharedEmpSystem _emp = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SpaceNinjaSystem _ninja = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    // How much the cell score should be increased per 1 AutoRechargeRate.
    private const int AutoRechargeValue = 100;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaSuitComponent, ContainerIsInsertingAttemptEvent>(OnSuitInsertAttempt);
        SubscribeLocalEvent<NinjaSuitComponent, RecallKatanaEvent>(OnRecallKatana);
    }

    protected override void NinjaEquipped(Entity<NinjaSuitComponent> ent, Entity<SpaceNinjaComponent> user)
    {
        base.NinjaEquipped(ent, user);

        _ninja.SetSuitPowerAlert(user);

        // raise event to let ninja components get starting battery
        _ninja.GetNinjaBattery(user.Owner, out var uid, out var _);

        if (uid is not { } battery_uid)
            return;

        var ev = new NinjaBatteryChangedEvent(battery_uid, ent.Owner);
        RaiseLocalEvent(ent, ref ev);
        RaiseLocalEvent(user, ref ev);
    }

    private void OnSuitInsertAttempt(EntityUid uid, NinjaSuitComponent comp, ContainerIsInsertingAttemptEvent args)
    {
        // this is for handling battery upgrading, not stopping actions from being added
        // if another container like ActionsContainer is specified, don't handle it
        if (TryComp<PowerCellSlotComponent>(uid, out var slot) && args.Container.ID != slot.CellSlotId)
            return;

        // no power cell for some reason??? allow it
        if (!_powerCell.TryGetBatteryFromSlot(uid, out var battery))
            return;

        if (!TryComp<BatteryComponent>(args.EntityUid, out var inserting))
        {
            args.Cancel();
            return;
        }

        var user = Transform(uid).ParentUid;

        // can only upgrade power cell, not swap to recharge instantly otherwise ninja could just swap batteries with flashlights in maints for easy power
        if (GetCellScore(args.EntityUid, inserting) <= GetCellScore(battery.Value, battery.Value))
        {
            args.Cancel();
            Popup.PopupEntity(Loc.GetString("ninja-cell-downgrade"), user, user);
            return;
        }

        // tell ninja abilities that use battery to update it so they don't use charge from the old one
        if (!_ninja.IsNinja(user))
            return;

        var ev = new NinjaBatteryChangedEvent(args.EntityUid, uid);
        RaiseLocalEvent(uid, ref ev);
        RaiseLocalEvent(user, ref ev);
    }

    // this function assigns a score to a power cell depending on the capacity, to be used when comparing which cell is better.
    private float GetCellScore(EntityUid uid, BatteryComponent battcomp)
    {
        // if a cell is able to automatically recharge, boost the score drastically depending on the recharge rate,
        // this is to ensure a ninja can still upgrade to a micro reactor cell even if they already have a medium or high.
        if (TryComp<BatterySelfRechargerComponent>(uid, out var selfcomp))
            return battcomp.MaxCharge + selfcomp.AutoRechargeRate * AutoRechargeValue;
        return battcomp.MaxCharge;
    }

    protected override void UserUnequippedSuit(Entity<NinjaSuitComponent> ent, Entity<SpaceNinjaComponent> user)
    {
        base.UserUnequippedSuit(ent, user);

        // remove power indicator
        _ninja.SetSuitPowerAlert(user);
    }

    private void OnRecallKatana(Entity<NinjaSuitComponent> ent, ref RecallKatanaEvent args)
    {
        var (uid, comp) = ent;
        var user = args.Performer;
        if (!_ninja.NinjaQuery.TryComp(user, out var ninja))
            return;

        // Mini: a katana that starts in the sheath is never equipped as a hand item, so it may
        // still be unbound. Look for a katana carried by the ninja before giving up silently.
        if (ninja.Katana == null && TryFindKatana(user, out var found))
            _ninja.BindKatana((user, ninja), found);

        if (ninja.Katana is not { } katana || TerminatingOrDeleted(katana))
            return;

        args.Handled = true;

        var coords = _transform.GetWorldPosition(katana);
        var distance = (_transform.GetWorldPosition(user) - coords).Length();
        var chargeNeeded = distance * comp.RecallCharge;
        if (!_ninja.TryUseCharge(user, chargeNeeded))
        {
            Popup.PopupEntity(Loc.GetString("ninja-no-power"), user, user);
            return;
        }

        // Goob edit
        // if (CheckDisabled(ent, user))
        //    return;

        // TODO: teleporting into belt slot
        var message = _hands.TryPickupAnyHand(user, katana)
            ? "ninja-katana-recalled"
            : "ninja-hands-full";
        Popup.PopupEntity(Loc.GetString(message), user, user);
    }

    /// <summary>
    /// Finds an energy katana carried by the user, including one stored inside the sheath.
    /// The katana is a child of the sheath, which is itself inside the belt slot of the user.
    /// </summary>
    private bool TryFindKatana(EntityUid user, out EntityUid katana)
    {
        katana = default;

        var query = EntityQueryEnumerator<EnergyKatanaComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            for (var parent = xform.ParentUid; parent.IsValid(); parent = Transform(parent).ParentUid)
            {
                if (parent != user)
                    continue;

                katana = uid;
                return true;
            }
        }

        return false;
    }
}
