using Content.Server._Orion.EnergyDome.Components;
using Content.Server.DeviceLinking.Systems;
using Content.Server.Power.EntitySystems;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Timing;
using Content.Shared.Toggleable;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._Orion.EnergyDome.Systems;

//
// License-Identifier: AGPL-3.0-or-later
//

public sealed class EnergyDomeSystem : EntitySystem
{
    /// <summary>
    /// Minimum joules required to enable a cell-powered dome.
    /// HasCharge(0) is always true for a present battery, so we need a positive threshold.
    /// </summary>
    private const float MinEnableCharge = 1f;

    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly DeviceLinkSystem _signalSystem = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EnergyDomeGeneratorComponent, MapInitEvent>(OnInit);

        SubscribeLocalEvent<EnergyDomeGeneratorComponent, ActivateInWorldEvent>(OnActivatedInWorld);
        SubscribeLocalEvent<EnergyDomeGeneratorComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<EnergyDomeGeneratorComponent, SignalReceivedEvent>(OnSignalReceived);
        SubscribeLocalEvent<EnergyDomeGeneratorComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<EnergyDomeGeneratorComponent, ToggleActionEvent>(OnToggleAction);

        SubscribeLocalEvent<EnergyDomeGeneratorComponent, PowerCellChangedEvent>(OnPowerCellChanged);
        SubscribeLocalEvent<EnergyDomeGeneratorComponent, PowerCellSlotEmptyEvent>(OnPowerCellSlotEmpty);
        SubscribeLocalEvent<EnergyDomeGeneratorComponent, ChargeChangedEvent>(OnChargeChanged);

        SubscribeLocalEvent<EnergyDomeGeneratorComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<EnergyDomeGeneratorComponent, DroppedEvent>(OnDropped);
        SubscribeLocalEvent<EnergyDomeGeneratorComponent, GotUnequippedHandEvent>(OnUnequippedHand);
        SubscribeLocalEvent<EnergyDomeGeneratorComponent, GotUnequippedEvent>(OnUnequipped);

        SubscribeLocalEvent<EnergyDomeGeneratorComponent, GetVerbsEvent<ActivationVerb>>(AddToggleDomeVerb);
        SubscribeLocalEvent<EnergyDomeGeneratorComponent, ExaminedEvent>(OnExamine);

        SubscribeLocalEvent<EnergyDomeGeneratorComponent, ComponentRemove>(OnComponentRemove);

        SubscribeLocalEvent<EnergyDomeComponent, DamageChangedEvent>(OnDomeDamaged);
    }

    private void OnInit(Entity<EnergyDomeGeneratorComponent> generator, ref MapInitEvent args)
    {
        if (generator.Comp.CanDeviceNetworkUse)
            _signalSystem.EnsureSinkPorts(generator, generator.Comp.TogglePort, generator.Comp.OnPort, generator.Comp.OffPort);

        if (generator.Comp.CanInteractUse)
            _actionContainer.EnsureAction(generator, ref generator.Comp.ToggleActionEntity, generator.Comp.ToggleAction);
    }

    #region Use Ways

    private void OnSignalReceived(Entity<EnergyDomeGeneratorComponent> generator, ref SignalReceivedEvent args)
    {
        if (!generator.Comp.CanDeviceNetworkUse)
            return;

        if (args.Port == generator.Comp.OnPort)
            AttemptToggle(generator, true);
        else if (args.Port == generator.Comp.OffPort)
            AttemptToggle(generator, false);
        else if (args.Port == generator.Comp.TogglePort)
            AttemptToggle(generator, !generator.Comp.Enabled);
    }

    private void OnAfterInteract(Entity<EnergyDomeGeneratorComponent> generator, ref AfterInteractEvent args)
    {
        if (!generator.Comp.CanInteractUse || args.Handled || !args.CanReach)
            return;

        if (AttemptToggle(generator, !generator.Comp.Enabled, args.User))
            args.Handled = true;
    }

    private void OnActivatedInWorld(Entity<EnergyDomeGeneratorComponent> generator, ref ActivateInWorldEvent args)
    {
        if (!generator.Comp.CanInteractUse || args.Handled || !args.Complex)
            return;

        if (AttemptToggle(generator, !generator.Comp.Enabled, args.User))
            args.Handled = true;
    }

    private void OnExamine(Entity<EnergyDomeGeneratorComponent> generator, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(
            generator.Comp.Enabled
                ? "energy-dome-on-examine-is-on-message"
                : "energy-dome-on-examine-is-off-message"
            ));
    }

    private void AddToggleDomeVerb(Entity<EnergyDomeGeneratorComponent> generator, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !generator.Comp.CanInteractUse)
            return;

        var user = args.User;
        ActivationVerb verb = new()
        {
            Text = Loc.GetString("energy-dome-verb-toggle"),
            Act = () => AttemptToggle(generator, !generator.Comp.Enabled, user)
        };

        args.Verbs.Add(verb);
    }

    private void OnGetActions(Entity<EnergyDomeGeneratorComponent> generator, ref GetItemActionsEvent args)
    {
        if (generator.Comp.CanInteractUse)
            args.AddAction(ref generator.Comp.ToggleActionEntity, generator.Comp.ToggleAction);
    }

    private void OnToggleAction(Entity<EnergyDomeGeneratorComponent> generator, ref ToggleActionEvent args)
    {
        if (args.Handled)
            return;

        // Orphaned action (item no longer held/worn) — strip it and refuse to activate.
        if (generator.Comp.CanInteractUse && !IsHeldOrWornBy(generator, args.Performer))
        {
            if (generator.Comp.ToggleActionEntity is { } action)
                _actions.RemoveProvidedAction(args.Performer, generator, action);

            TurnOff(generator, false);
            args.Handled = true;
            return;
        }

        AttemptToggle(generator, !generator.Comp.Enabled, args.Performer);
        args.Handled = true;
    }

    #endregion

    #region Interactions

    private void OnPowerCellSlotEmpty(Entity<EnergyDomeGeneratorComponent> generator, ref PowerCellSlotEmptyEvent args)
    {
        TurnOff(generator, true);
    }

    private void OnPowerCellChanged(Entity<EnergyDomeGeneratorComponent> generator, ref PowerCellChangedEvent args)
    {
        if (args.Ejected || !HasPower(generator))
            TurnOff(generator, true);
    }

    private void OnChargeChanged(Entity<EnergyDomeGeneratorComponent> generator, ref ChargeChangedEvent args)
    {
        if (args.CurrentCharge <= 0)
            TurnOff(generator, true);
    }

    private void OnDomeDamaged(Entity<EnergyDomeComponent> dome, ref DamageChangedEvent args)
    {
        if (dome.Comp.Generator == null)
            return;

        if (args.DamageDelta == null)
            return;

        var generatorUid = dome.Comp.Generator.Value;
        if (!TryComp<EnergyDomeGeneratorComponent>(generatorUid, out var generatorComp))
            return;

        var totalDamage = args.DamageDelta.GetTotal().Float();
        var energyLeak = totalDamage * generatorComp.DamageEnergyDraw;

        _audio.PlayPvs(generatorComp.ParrySound, dome);

        if (HasComp<PowerCellDrawComponent>(generatorUid))
        {
            if (_powerCell.TryGetBatteryFromSlot(generatorUid, out var cell))
            {
                _battery.UseCharge(cell.Value.AsNullable(), energyLeak);

                if (_battery.GetCharge(cell.Value.AsNullable()) <= 0)
                    TurnOff((generatorUid, generatorComp), true);
            }
            else
            {
                // Cell gone while dome was active.
                TurnOff((generatorUid, generatorComp), true);
            }
        }

        // Wired/static dome path: battery on the generator itself.
        if (!TryComp<BatteryComponent>(generatorUid, out var battery))
            return;

        _battery.UseCharge((generatorUid, (BatteryComponent?) battery), energyLeak);

        if (_battery.GetCharge((generatorUid, battery)) <= 0)
            TurnOff((generatorUid, generatorComp), true);
    }

    private void OnParentChanged(Entity<EnergyDomeGeneratorComponent> generator, ref EntParentChangedMessage args)
    {
        // TODO: taking the active barrier in hand for some reason does not manage to change the parent in this case,
        // and the barrier is not turned off.
        if (GetProtectedEntity(generator) != generator.Comp.DomeParentEntity)
            TurnOff(generator, false);
    }

    private void OnDropped(Entity<EnergyDomeGeneratorComponent> generator, ref DroppedEvent args)
    {
        TurnOff(generator, false);
    }

    private void OnUnequippedHand(Entity<EnergyDomeGeneratorComponent> generator, ref GotUnequippedHandEvent args)
    {
        TurnOff(generator, false);
    }

    private void OnUnequipped(Entity<EnergyDomeGeneratorComponent> generator, ref GotUnequippedEvent args)
    {
        TurnOff(generator, false);
    }

    private void OnComponentRemove(Entity<EnergyDomeGeneratorComponent> generator, ref ComponentRemove args)
    {
        TurnOff(generator, false);
    }

    #endregion

    #region Functional

    public bool AttemptToggle(Entity<EnergyDomeGeneratorComponent> generator, bool status, EntityUid? user = null)
    {
        if (_useDelay.IsDelayed(generator.Owner))
        {
            _audio.PlayPvs(generator.Comp.TurnOffSound, generator);
            Popup(generator, Loc.GetString("energy-dome-recharging"), user);
            return false;
        }

        // Turning off is always allowed (even with a dead cell).
        if (!status)
        {
            Toggle(generator, false);
            return true;
        }

        if (user != null && generator.Comp.CanInteractUse && !IsHeldOrWornBy(generator, user.Value))
        {
            _audio.PlayPvs(generator.Comp.TurnOffSound, generator);
            return false;
        }

        if (!HasPower(generator, out var missingCell))
        {
            _audio.PlayPvs(generator.Comp.TurnOffSound, generator);
            Popup(generator,
                Loc.GetString(missingCell ? "energy-dome-no-cell" : "energy-dome-no-power"),
                user);
            return false;
        }

        Toggle(generator, true);
        return true;
    }

    private void Popup(EntityUid generator, string message, EntityUid? user)
    {
        if (user != null)
            _popup.PopupEntity(message, generator, user.Value);
        else
            _popup.PopupEntity(message, generator);
    }

    private void Toggle(Entity<EnergyDomeGeneratorComponent> generator, bool status)
    {
        if (status)
            TurnOn(generator);
        else
            TurnOff(generator, false);
    }

    private void TurnOn(Entity<EnergyDomeGeneratorComponent> generator)
    {
        if (generator.Comp.Enabled)
            return;

        var protectedEntity = GetProtectedEntity(generator);

        var newDome = Spawn(generator.Comp.DomePrototype, Transform(protectedEntity).Coordinates);
        generator.Comp.DomeParentEntity = protectedEntity;
        _transform.SetParent(newDome, protectedEntity);

        if (TryComp<EnergyDomeComponent>(newDome, out var domeComp))
            domeComp.Generator = generator;

        if (TryComp<PowerCellDrawComponent>(generator.Owner, out _))
            _powerCell.SetDrawEnabled(generator.Owner, true);

        generator.Comp.SpawnedDome = newDome;
        _audio.PlayPvs(generator.Comp.TurnOnSound, generator);
        generator.Comp.Enabled = true;
    }

    private void TurnOff(Entity<EnergyDomeGeneratorComponent> generator, bool startReloading)
    {
        if (!generator.Comp.Enabled)
            return;

        generator.Comp.Enabled = false;
        QueueDel(generator.Comp.SpawnedDome);
        generator.Comp.SpawnedDome = null;
        generator.Comp.DomeParentEntity = null;

        if (TryComp<PowerCellDrawComponent>(generator.Owner, out _))
            _powerCell.SetDrawEnabled(generator.Owner, false);

        _audio.PlayPvs(generator.Comp.TurnOffSound, generator);

        if (!startReloading)
            return;

        _audio.PlayPvs(generator.Comp.EnergyOutSound, generator);

        if (TryComp<UseDelayComponent>(generator, out var useDelay))
            _useDelay.TryResetDelay(new Entity<UseDelayComponent>(generator, useDelay));
    }

    #endregion

    #region Util

    /// <summary>
    /// True if the generator has enough charge to enable (wired battery or slotted cell).
    /// </summary>
    private bool HasPower(EntityUid generator, out bool missingCell)
    {
        missingCell = false;

        if (TryComp<PowerCellSlotComponent>(generator, out _))
        {
            if (!_powerCell.TryGetBatteryFromSlot(generator, out var cell))
            {
                // Wired generators may still have a built-in Battery without a cell.
                if (TryComp<BatteryComponent>(generator, out var wired) &&
                    _battery.GetCharge((generator, wired)) >= MinEnableCharge)
                    return true;

                missingCell = true;
                return false;
            }

            return _battery.GetCharge(cell.Value.AsNullable()) >= MinEnableCharge;
        }

        if (TryComp<BatteryComponent>(generator, out var battery))
            return _battery.GetCharge((generator, battery)) >= MinEnableCharge;

        return false;
    }

    private bool HasPower(EntityUid generator) => HasPower(generator, out _);

    private bool IsHeldOrWornBy(EntityUid generator, EntityUid user)
    {
        if (_hands.IsHolding(user, generator))
            return true;

        return _container.TryGetContainingContainer((generator, null, null), out var container)
               && container.Owner == user;
    }

    private EntityUid GetProtectedEntity(EntityUid entity)
    {
        return _container.TryGetOuterContainer(entity, Transform(entity), out var container)
            ? container.Owner
            : entity;
    }

    #endregion
}
