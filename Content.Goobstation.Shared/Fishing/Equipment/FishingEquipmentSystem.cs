// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Goobstation.Shared.Fishing.Components;
using Content.Goobstation.Shared.Fishing.Events;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Prototypes;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Goobstation.Shared.Fishing.Systems;

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
}
