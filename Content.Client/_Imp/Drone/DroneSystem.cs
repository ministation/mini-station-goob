// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Imp.Drone;
using Content.Shared.Alert;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Imp.Drone;

/// <summary>
/// Mini: batteries are no longer continuously networked (charge is inferred from charge rate),
/// so the drone charge alert has to be computed client-side, same as <see cref="Content.Client.Silicons.Borgs.BorgSystem"/>.
/// </summary>
public sealed class DroneSystem : SharedDroneSystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan AlertUpdateDelay = TimeSpan.FromSeconds(0.5f);

    private TimeSpan _nextAlertUpdate = TimeSpan.Zero;
    private EntityQuery<DroneComponent> _droneQuery;
    private EntityQuery<PowerCellSlotComponent> _slotQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DroneComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<DroneComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        _droneQuery = GetEntityQuery<DroneComponent>();
        _slotQuery = GetEntityQuery<PowerCellSlotComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_player.LocalEntity is not { } localPlayer)
            return;

        var curTime = _timing.CurTime;
        if (curTime < _nextAlertUpdate)
            return;

        _nextAlertUpdate = curTime + AlertUpdateDelay;

        if (!_droneQuery.TryComp(localPlayer, out var drone) || !_slotQuery.TryComp(localPlayer, out var slot))
            return;

        UpdateBatteryAlert((localPlayer, drone, slot));
    }

    private void OnPlayerAttached(Entity<DroneComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        UpdateBatteryAlert((ent.Owner, ent.Comp, null));
    }

    private void OnPlayerDetached(Entity<DroneComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.BatteryAlert);
        _alerts.ClearAlert(ent.Owner, ent.Comp.NoBatteryAlert);
    }

    private void UpdateBatteryAlert(Entity<DroneComponent, PowerCellSlotComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp2, false))
            return;

        if (!_powerCell.TryGetBatteryFromSlot((ent.Owner, ent.Comp2), out var battery))
        {
            _alerts.ClearAlert(ent.Owner, ent.Comp1.BatteryAlert);
            _alerts.ShowAlert(ent.Owner, ent.Comp1.NoBatteryAlert);
            return;
        }

        // Alert levels from 0 to 10.
        var chargeLevel = (short) MathF.Round(_battery.GetChargeLevel(battery.Value.AsNullable()) * 10f);

        // Make sure 0 only shows if the battery is really dead, accounting for float imprecision.
        if (chargeLevel == 0 && _powerCell.HasDrawCharge((ent.Owner, null, ent.Comp2)))
            chargeLevel = 1;

        _alerts.ClearAlert(ent.Owner, ent.Comp1.NoBatteryAlert);
        _alerts.ShowAlert(ent.Owner, ent.Comp1.BatteryAlert, chargeLevel);
    }
}
