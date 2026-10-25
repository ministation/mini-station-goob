// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Mini.Ninja;
using Content.Shared._Mini.Ninja.Components;
using Content.Shared._Mini.Ninja.Prototypes;
using Content.Shared._Mini.Ninja.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Ninja.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.RetractableItemAction;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.Ninja.Systems;

/// <summary>
/// Server side of the Spider OS: module selection, suit boot, action/component installation and power drain.
/// </summary>
public sealed class NinjaOsSystem : SharedNinjaOsSystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly RetractableItemActionSystem _retractable = default!;

    /// <summary>
    /// Items that were made unremoveable by an active suit, per suit.
    /// </summary>
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _lockedBySuit = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<NinjaOsComponent>(NinjaOsUiKey.Key, subs =>
        {
            subs.Event<NinjaOsSelectModuleMessage>(OnSelectModule);
            subs.Event<NinjaOsSetSuitPowerMessage>(OnSetSuitPower);
        });

        SubscribeLocalEvent<NinjaOsComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<NinjaOsComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<NinjaOsComponent, OpenNinjaOsEvent>(OnOpenNinjaOs);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NinjaOsComponent>();
        while (query.MoveNext(out var suitUid, out var comp))
        {
            if (!comp.SuitActivated)
                continue;

            // An activated suit without a wearer does not drain power.
            if (!Xform.GetParentUid(suitUid).IsValid())
                continue;

            if (!PowerCell.TryGetBatteryFromSlot(suitUid, out var battery))
                continue;

            _battery.TryUseCharge(battery.Value.AsNullable(), comp.IdleDraw * frameTime);
        }
    }

    private void OnShutdown(Entity<NinjaOsComponent> ent, ref ComponentShutdown args)
    {
        var wearer = Xform.GetParentUid(ent.Owner);
        if (wearer.IsValid())
            RemoveGrantedActions(ent.Owner, wearer, ent.Comp);

        UnlockAll(ent.Owner);
    }

    private void OnOpenNinjaOs(Entity<NinjaOsComponent> suit, ref OpenNinjaOsEvent args)
    {
        var (suitUid, _) = suit;
        if (!IsAuthorized(suitUid, args.Performer))
            return;

        args.Handled = true;
        _ui.TryOpenUi(suitUid, NinjaOsUiKey.Key, args.Performer);
    }

    private void OnUiOpened(Entity<NinjaOsComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is not NinjaOsUiKey.Key)
            return;

        UpdateUi(ent.Owner, ent.Comp);
    }

    private void OnSelectModule(Entity<NinjaOsComponent> suit, ref NinjaOsSelectModuleMessage args)
    {
        var (suitUid, comp) = suit;

        if (!IsAuthorized(suitUid, args.Actor) || comp.SuitActivated)
            return;

        if (!TryGetModule(comp, args.Path, args.Tier, out _))
            return;

        if (comp.LockedTiers.Contains(args.Tier))
            return;

        comp.SelectedModules[args.Tier] = args.Path;
        comp.LockedTiers.Add(args.Tier);

        Dirty(suitUid, comp);
        UpdateUi(suitUid, comp);
    }

    private void OnSetSuitPower(Entity<NinjaOsComponent> suit, ref NinjaOsSetSuitPowerMessage args)
    {
        var (suitUid, comp) = suit;

        if (!IsAuthorized(suitUid, args.Actor))
            return;

        if (args.Activated)
        {
            if (comp.SuitActivated)
                return;

            if (!Proto.TryIndex(comp.ActivationBoot, out NinjaOsBootPrototype? boot) ||
                !RunBootScriptChecks(suitUid, args.Actor, boot))
            {
                SendResult(suitUid, args.Actor, false, GetFirstBootFail(suitUid, args.Actor, comp));
                return;
            }

            ActivateSuit(suitUid, comp);
            SendResult(suitUid, args.Actor, true);
        }
        else
        {
            if (!comp.SuitActivated)
                return;

            DeactivateSuit(suitUid, comp);
            SendResult(suitUid, args.Actor, true);
        }

        Dirty(suitUid, comp);
        UpdateUi(suitUid, comp);
    }

    private string GetFirstBootFail(EntityUid suitUid, EntityUid actor, NinjaOsComponent comp)
    {
        if (!Proto.TryIndex(comp.ActivationBoot, out NinjaOsBootPrototype? boot))
            return "ninja-os-boot-fail-auth";

        foreach (var step in boot.Steps)
        {
            if (step.Check is { } check && !RunBootCheck(suitUid, actor, check, out var reason))
                return reason;
        }

        return "ninja-os-boot-fail-auth";
    }

    private void SendResult(EntityUid suitUid, EntityUid actor, bool success, string failReason = "")
    {
        _ui.ServerSendUiMessage(suitUid, NinjaOsUiKey.Key, new NinjaOsResultMessage(success, failReason), actor);
    }

    private void ActivateSuit(EntityUid suitUid, NinjaOsComponent comp)
    {
        comp.SuitActivated = true;

        foreach (var (tier, path) in comp.SelectedModules)
        {
            if (comp.ActivatedTiers.Contains(tier) || !TryGetModule(comp, path, tier, out var module))
                continue;

            GrantModuleComponents(suitUid, module);
            GrantModuleActions(suitUid, module);
            comp.ActivatedTiers.Add(tier);
        }

        var wearer = Xform.GetParentUid(suitUid);
        if (!wearer.IsValid())
            return;

        LockAll(wearer, suitUid);
        _actions.GrantContainedActions(wearer, suitUid);

        var ev = new NinjaSuitPowerChangedEvent(suitUid, wearer, true);
        RaiseLocalEvent(suitUid, ref ev);
        RaiseLocalEvent(wearer, ref ev);
    }

    private void DeactivateSuit(EntityUid suitUid, NinjaOsComponent comp)
    {
        comp.SuitActivated = false;

        UnlockAll(suitUid);

        var wearer = Xform.GetParentUid(suitUid);
        if (!wearer.IsValid())
            return;

        RemoveGrantedActions(suitUid, wearer, comp);

        var ev = new NinjaSuitPowerChangedEvent(suitUid, wearer, false);
        RaiseLocalEvent(suitUid, ref ev);
        RaiseLocalEvent(wearer, ref ev);
    }

    /// <summary>
    /// Copy the OS configuration to another suit, used when the ninja gets a new body.
    /// </summary>
    public void RestoreState(EntityUid suitUid, NinjaOsComponent source)
    {
        var comp = EnsureComp<NinjaOsComponent>(suitUid);
        comp.Os = source.Os;
        comp.SelectedModules = new Dictionary<int, NinjaPath>(source.SelectedModules);
        comp.LockedTiers = new HashSet<int>(source.LockedTiers);
        comp.ActivatedTiers = new HashSet<int>();
        comp.SuitActivated = false;

        foreach (var (tier, path) in comp.SelectedModules.ToArray())
        {
            if (TryGetModule(comp, path, tier, out var module) && !module.TransferOnSecondChance)
                comp.SelectedModules.Remove(tier);
        }

        Dirty(suitUid, comp);
    }

    private void GrantModuleComponents(EntityUid suitUid, NinjaModulePrototype module)
    {
        var toAdd = new ComponentRegistry();
        foreach (var (name, entry) in module.Components)
        {
            if (!HasComp(suitUid, entry.Component.GetType()))
                toAdd[name] = entry;
        }

        if (toAdd.Count > 0)
            EntityManager.AddComponents(suitUid, toAdd);
    }

    private void GrantModuleActions(EntityUid suitUid, NinjaModulePrototype module)
    {
        if (module.Actions == null || module.Actions.Count == 0)
            return;

        foreach (var action in module.Actions)
        {
            AddActionToContainer(suitUid, action);
        }
    }

    private void AddActionToContainer(EntityUid suitUid, EntProtoId actionProto)
    {
        var container = EnsureComp<ActionsContainerComponent>(suitUid);

        foreach (var existing in container.Container.ContainedEntities)
        {
            if (MetaData(existing).EntityPrototype?.ID == actionProto.Id)
                return;
        }

        _actionContainer.AddAction(suitUid, actionProto.Id, container);
    }

    private void RemoveGrantedActions(EntityUid suitUid, EntityUid wearer, NinjaOsComponent comp)
    {
        if (!TryComp<ActionsContainerComponent>(suitUid, out var container))
            return;

        // The Spider OS action has to stay reachable so the suit can be turned back on.
        string keep = "ActionOpenNinjaOs";
        if (TryComp<NinjaSuitComponent>(suitUid, out var suit))
            keep = suit.OpenNinjaOsAction.Id;

        foreach (var contained in container.Container.ContainedEntities.ToArray())
        {
            if (MetaData(contained).EntityPrototype?.ID is not { } protoId || protoId == keep)
                continue;

            // Mini: put summoned items (shuriken emitter, johyo) back into their actions first.
            if (TryComp<RetractableItemActionComponent>(contained, out var retractable))
                _retractable.TryRetractRetractableItem(wearer, (contained, retractable));

            _actions.RemoveProvidedAction(wearer, suitUid, contained);
        }
    }

    private void LockAll(EntityUid wearer, EntityUid suitUid)
    {
        LockItem(suitUid, suitUid);

        foreach (var slot in HardwareSlots.Values)
        {
            if (Inventory.TryGetSlotEntity(wearer, slot, out var item))
                LockItem(suitUid, item.Value);
        }
    }

    private void UnlockAll(EntityUid suitUid)
    {
        if (!_lockedBySuit.Remove(suitUid, out var locked))
            return;

        foreach (var uid in locked)
        {
            if (!TerminatingOrDeleted(uid))
                RemComp<UnremoveableComponent>(uid);
        }
    }

    private void LockItem(EntityUid suitUid, EntityUid item)
    {
        if (HasComp<UnremoveableComponent>(item))
            return;

        AddComp(item, new UnremoveableComponent { DeleteOnDrop = false }, true);

        if (!_lockedBySuit.TryGetValue(suitUid, out var locked))
        {
            locked = new HashSet<EntityUid>();
            _lockedBySuit[suitUid] = locked;
        }

        locked.Add(item);
    }

    private void UpdateUi(EntityUid suitUid, NinjaOsComponent comp)
    {
        var state = new NinjaOsBoundUserInterfaceState(
            comp.Os.Id,
            comp.SelectedModules,
            comp.LockedTiers,
            comp.ActivatedTiers,
            comp.SuitActivated,
            comp.ActivationBoot.Id,
            comp.DeactivationBoot.Id);

        _ui.SetUiState(suitUid, NinjaOsUiKey.Key, state);
    }
}