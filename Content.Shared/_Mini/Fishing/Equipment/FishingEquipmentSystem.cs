// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Events;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Prototypes;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Mini.Fishing.Systems;

/// <summary>
/// Applies the effects of the line, hook and bait slotted into a fishing rod.
/// </summary>
public sealed partial class FishingEquipmentSystem : EntitySystem
{
    public const string LineSlotId = "line";
    public const string HookSlotId = "hook";
    public const string BaitSlotId = "bait";

    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishingRodComponent, FishFightModifiersEvent>(OnFightModifiers);
        SubscribeLocalEvent<FishingRodComponent, FishLootModifiersEvent>(OnLootModifiers);
        SubscribeLocalEvent<FishingRodComponent, FishCaughtWithRodEvent>(OnCaughtWithRod);
        SubscribeLocalEvent<FishingRodComponent, MapInitEvent>(OnRodMapInit);
        SubscribeLocalEvent<FishingRodComponent, InteractUsingEvent>(OnRodInteractUsing);
        SubscribeLocalEvent<FishingLineComponent, EntGotInsertedIntoContainerMessage>(OnLineInserted);
        SubscribeLocalEvent<FishingLineComponent, EntGotRemovedFromContainerMessage>(OnLineRemoved);
        SubscribeLocalEvent<FishingLineComponent, EntityTerminatingEvent>(OnLineTerminating);
        SubscribeLocalEvent<FishingBaitCanComponent, UseInHandEvent>(OnBaitCanUse);

        SubscribeLocalEvent<FishingRodComponent, GetVerbsEvent<InteractionVerb>>(OnGetEquipmentVerbs);
        SubscribeLocalEvent<FishingRodComponent, BoundUIOpenedEvent>(OnEquipmentUiOpened);

        // The client visuals system owns the rod container events on its side.
        if (_net.IsServer)
        {
            SubscribeLocalEvent<FishingRodComponent, EntInsertedIntoContainerMessage>(OnEquipmentInserted);
            SubscribeLocalEvent<FishingRodComponent, EntRemovedFromContainerMessage>(OnEquipmentRemoved);
        }

        Subs.BuiEvents<FishingRodComponent>(FishingRodEquipmentUiKey.Key, subs =>
        {
            subs.Event<FishingRodEquipmentEjectMessage>(OnEquipmentEject);
        });
    }

    #region Slots helpers

    public FishingLineComponent? GetLine(EntityUid rod)
    {
        if (!_slots.TryGetSlot(rod, LineSlotId, out var slot) || slot.Item is not { } item)
            return null;

        return TryComp<FishingLineComponent>(item, out var line) ? line : null;
    }

    public FishingHookComponent? GetHook(EntityUid rod)
    {
        if (!_slots.TryGetSlot(rod, HookSlotId, out var slot) || slot.Item is not { } item)
            return null;

        return TryComp<FishingHookComponent>(item, out var hook) ? hook : null;
    }

    public FishingBaitComponent? GetBait(EntityUid rod)
    {
        if (!_slots.TryGetSlot(rod, BaitSlotId, out var slot) || slot.Item is not { } item)
            return null;

        return TryComp<FishingBaitComponent>(item, out var bait) ? bait : null;
    }

    #endregion

    #region Modifiers

    private void OnFightModifiers(Entity<FishingRodComponent> ent, ref FishFightModifiersEvent args)
    {
        var rod = ent.Comp;

        args.Difficulty += rod.DifficultyModifier;
        args.CompletionGain *= rod.CompletionSpeedMultiplier;
        args.BaitSpeed *= rod.BaitSpeedMultiplier;
        args.BaitHeight *= rod.BaitHeightMultiplier;
        args.Gravity *= rod.GravityMultiplier;
        args.Bounciness *= rod.BouncinessMultiplier;
        args.Deceleration *= rod.DecelerationMultiplier;

        if (GetLine(ent) is { } line)
        {
            args.Difficulty += line.DifficultyModifier;
            args.CompletionLoss *= line.CompletionLossMultiplier;
            args.Rules |= line.Rules;
        }

        if (GetHook(ent) is { } hook)
        {
            args.Bounciness *= hook.BouncinessMultiplier;
            args.Gravity *= hook.GravityMultiplier;
            args.Rules |= hook.Rules;
        }
    }

    private void OnLootModifiers(Entity<FishingRodComponent> ent, ref FishLootModifiersEvent args)
    {
        if (GetHook(ent) is { } hook)
        {
            args.FishWeightMultiplier *= hook.FishWeightMultiplier;
            args.DudRerolls += hook.DudRerolls;
        }

        if (GetBait(ent) is not { } bait)
            return;

        switch (bait.Quality)
        {
            case FishingBaitQuality.Basic:
                args.DudRerolls += 1;
                break;
            case FishingBaitQuality.Good:
                args.DudRerolls += 2;
                args.ExtraCatchChance += 0.05f;
                break;
            case FishingBaitQuality.Great:
                args.DudRerolls += 3;
                args.ExtraCatchChance += 0.1f;
                break;
        }
    }

    #endregion

    #region Bait consumption

    private void OnCaughtWithRod(Entity<FishingRodComponent> ent, ref FishCaughtWithRodEvent args)
    {
        if (!_net.IsServer)
            return;

        if (args.Reward is not { } reward || !_proto.TryIndex(reward, out EntityPrototype? proto))
            return;

        // Catching junk does not use up bait.
        if (!proto.HasComponent<FishComponent>() && !proto.HasComponent<MobStateComponent>())
            return;

        if (!_slots.TryGetSlot(ent, BaitSlotId, out var slot) || slot.Item is not { } baitItem)
            return;

        // A fish used as bait is always used up, lures and food are not necessarily consumable.
        if (HasComp<FishComponent>(baitItem))
        {
            QueueDel(baitItem);
            return;
        }

        if (!TryComp<FishingBaitComponent>(baitItem, out var bait) || !bait.Consumable)
            return;

        QueueDel(baitItem);
    }

    #endregion

    #region Cast range

    private void OnRodMapInit(Entity<FishingRodComponent> ent, ref MapInitEvent args)
    {
        if (!_net.IsServer)
            return;

        UpdateCastRange(ent);
    }

    private void OnLineInserted(Entity<FishingLineComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (_net.IsServer && args.Container.ID == LineSlotId)
            UpdateRodCastRange(args.Container.Owner);
    }

    private void OnLineRemoved(Entity<FishingLineComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (_net.IsServer && args.Container.ID == LineSlotId)
            UpdateRodCastRange(args.Container.Owner);
    }

    private void UpdateRodCastRange(EntityUid rodUid)
    {
        if (!TryComp<FishingRodComponent>(rodUid, out var rod) || TerminatingOrDeleted(rodUid))
            return;

        UpdateCastRange((rodUid, rod));
    }

    private void OnLineTerminating(Entity<FishingLineComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!_net.IsServer || TerminatingOrDeleted(ent))
            return;

        var rodUid = Transform(ent).ParentUid;
        if (!TryComp<FishingRodComponent>(rodUid, out var rod) || TerminatingOrDeleted(rodUid))
            return;

        UpdateCastRange((rodUid, rod));
    }

    private void UpdateCastRange(Entity<FishingRodComponent> rod)
    {
        var state = EnsureComp<FishingRodEquipmentComponent>(rod);
        var bonus = GetLine(rod)?.CastRangeBonus ?? 0f;

        if (!state.Initialized)
        {
            state.BaseCastRange = rod.Comp.CastRange - bonus;
            state.Initialized = true;
        }

        rod.Comp.CastRange = MathF.Max(1f, state.BaseCastRange + bonus);
    }

    #endregion

    #region Bait can

    private void OnBaitCanUse(Entity<FishingBaitCanComponent> ent, ref UseInHandEvent args)
    {
        if (!_net.IsServer || args.Handled)
            return;

        args.Handled = true;

        if (!TryTakeBait(ent, args.User, out var bait))
            return;

        _hands.TryPickupAnyHand(args.User, bait);
    }

    private void OnRodInteractUsing(Entity<FishingRodComponent> ent, ref InteractUsingEvent args)
    {
        if (!_net.IsServer || args.Handled)
            return;

        if (!TryComp<FishingBaitCanComponent>(args.Used, out var can))
            return;

        args.Handled = true;

        if (!_slots.TryGetSlot(ent, BaitSlotId, out var slot) || slot.HasItem)
        {
            _popup.PopupEntity(Loc.GetString("fishing-bait-slot-full"), ent, args.User);
            return;
        }

        if (!TryTakeBait((args.Used, can), args.User, out var bait))
            return;

        if (!_slots.TryInsert(ent, BaitSlotId, bait, args.User))
            _popup.PopupEntity(Loc.GetString("fishing-bait-not-bait"), ent, args.User);
    }

    private bool TryTakeBait(Entity<FishingBaitCanComponent> can, EntityUid user, out EntityUid bait)
    {
        bait = default;
        var comp = can.Comp;

        if (comp.UsesLeft <= 0)
        {
            _popup.PopupEntity(Loc.GetString("fishing-bait-can-empty"), can, user);
            return false;
        }

        if (_timing.CurTime < comp.NextUse)
        {
            _popup.PopupEntity(Loc.GetString("fishing-bait-can-wait"), can, user);
            return false;
        }

        comp.NextUse = _timing.CurTime + TimeSpan.FromSeconds(comp.Cooldown);
        comp.UsesLeft = Math.Max(0, comp.UsesLeft - 1);
        Dirty(can, comp);

        bait = Spawn(comp.BaitPrototype, Transform(can).Coordinates);
        return true;
    }

    #endregion

    #region Equipment window

    private static bool IsEquipmentSlot(string containerId)
    {
        return containerId == LineSlotId || containerId == HookSlotId || containerId == BaitSlotId;
    }

    private void OnGetEquipmentVerbs(Entity<FishingRodComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;

        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("fishing-rod-equipment-verb"),
            Act = () => _ui.TryToggleUi(ent.Owner, FishingRodEquipmentUiKey.Key, user),
        });
    }

    private void OnEquipmentUiOpened(Entity<FishingRodComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is not FishingRodEquipmentUiKey)
            return;

        _ui.SetUiState(ent.Owner, FishingRodEquipmentUiKey.Key, BuildEquipmentState(ent.Owner));
    }

    private void OnEquipmentInserted(Entity<FishingRodComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        UpdateEquipmentState(ent.Owner, args.Container.ID);
    }

    private void OnEquipmentRemoved(Entity<FishingRodComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        UpdateEquipmentState(ent.Owner, args.Container.ID);
    }

    private void UpdateEquipmentState(EntityUid rod, string containerId)
    {
        if (!_net.IsServer || !IsEquipmentSlot(containerId))
            return;

        if (_ui.IsUiOpen(rod, FishingRodEquipmentUiKey.Key))
            _ui.SetUiState(rod, FishingRodEquipmentUiKey.Key, BuildEquipmentState(rod));
    }

    private void OnEquipmentEject(Entity<FishingRodComponent> ent, ref FishingRodEquipmentEjectMessage args)
    {
        if (!IsEquipmentSlot(args.Slot))
            return;

        if (!_slots.TryGetSlot(ent.Owner, args.Slot, out var slot))
            return;

        _slots.TryEjectToHands(ent.Owner, slot, args.Actor, excludeUserAudio: true);
    }

    private FishingRodEquipmentBoundUserInterfaceState BuildEquipmentState(EntityUid rod)
    {
        return new FishingRodEquipmentBoundUserInterfaceState(new List<FishingRodEquipmentSlotState>
        {
            GetSlotState(rod, LineSlotId, "fishing-rod-slot-line"),
            GetSlotState(rod, HookSlotId, "fishing-rod-slot-hook"),
            GetSlotState(rod, BaitSlotId, "fishing-rod-slot-bait"),
        });
    }

    private FishingRodEquipmentSlotState GetSlotState(EntityUid rod, string slotId, string name)
    {
        string? prototype = null;

        if (_slots.TryGetSlot(rod, slotId, out var slot) && slot.Item is { } item)
            prototype = Prototype(item)?.ID;

        return new FishingRodEquipmentSlotState
        {
            Slot = slotId,
            Name = name,
            Prototype = prototype,
        };
    }

    #endregion
}
