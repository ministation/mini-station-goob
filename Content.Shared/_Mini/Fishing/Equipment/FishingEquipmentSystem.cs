// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System;

using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Events;
using Content.Shared._Mini.Fishing.Prototypes;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Materials;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Prototypes;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
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
    public const string AnomalySlotId = "anomaly";

    /// <summary>tg: a gravity anomaly core makes the lure fall slower during the fight.</summary>
    private const float AnomalyGravityMultiplier = 0.4f;

    /// <summary>Base chance, in percent, that an anomalous hook uses its core effect on a catch.</summary>
    private const float AnomalyBaseChance = 25f;

    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private FishSystem _fish = default!;

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
        SubscribeLocalEvent<FishingRodComponent, ExaminedEvent>(OnRodExamined);
        SubscribeLocalEvent<FishingBaitComponent, ExaminedEvent>(OnBaitExamined);
        SubscribeLocalEvent<FishingHookComponent, ExaminedEvent>(OnHookExamined);
        SubscribeLocalEvent<FishingHookComponent, EntInsertedIntoContainerMessage>(OnHookCoreInserted);
        SubscribeLocalEvent<FishingHookComponent, EntRemovedFromContainerMessage>(OnHookCoreRemoved);
        _proto.PrototypesReloaded += OnPrototypesReloaded;

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

    public override void Shutdown()
    {
        base.Shutdown();
        _proto.PrototypesReloaded -= OnPrototypesReloaded;
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

    /// <summary>
    /// Hook entity currently slotted into the rod, if any.
    /// </summary>
    public EntityUid? GetHookItem(EntityUid rod)
    {
        if (!_slots.TryGetSlot(rod, HookSlotId, out var slot))
            return null;

        return slot.Item;
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

        if (GetHookItem(ent.Owner) is { } hookItem &&
            GetAnomalyEffect(hookItem) == FishingAnomalyEffect.Gravity)
        {
            args.Gravity *= AnomalyGravityMultiplier;
        }
    }

    /// <summary>tg: without bait fish are fifteen times less likely to bite.</summary>
    private const float NoBaitFishWeightMultiplier = 0.15f;

    private const float BasicBaitFishWeightMultiplier = 2f;
    private const float GoodBaitFishWeightMultiplier = 3.5f;
    private const float GreatBaitFishWeightMultiplier = 9f;

    private void OnLootModifiers(Entity<FishingRodComponent> ent, ref FishLootModifiersEvent args)
    {
        if (GetHook(ent) is { } hook)
        {
            args.FishWeightMultiplier *= hook.FishWeightMultiplier;
            args.DudRerolls += hook.DudRerolls;

            // tg's magnetic hook removes duds from the table outright.
            if (hook.RemoveDuds)
                args.RemoveDuds = true;
        }

        if (GetBait(ent) is not { } bait)
        {
            args.FishWeightMultiplier *= NoBaitFishWeightMultiplier;
            return;
        }

        args.FishWeightMultiplier *= bait.Quality switch
        {
            FishingBaitQuality.Good => GoodBaitFishWeightMultiplier,
            FishingBaitQuality.Great => GreatBaitFishWeightMultiplier,
            _ => BasicBaitFishWeightMultiplier,
        };

        // Normal bait removes duds from the table. Artificial lures leave them in.
        if (bait.Consumable)
            args.RemoveDuds = true;

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

    #region Anomaly hook

    /// <summary>
    /// tg material table for the dimensional anomaly effect: gold and silver are the most common.
    /// </summary>
    private static readonly (string Material, float Weight)[] AnomalyMaterials =
    {
        ("Gold", 20f),
        ("Silver", 20f),
        ("Plastic", 10f),
        ("Uranium", 5f),
        ("Plasma", 5f),
    };

    /// <summary>
    /// Anomaly core currently slotted into the hook, if any.
    /// </summary>
    public EntityUid? GetAnomalyCore(EntityUid hookItem)
    {
        if (!_slots.TryGetSlot(hookItem, AnomalySlotId, out var slot))
            return null;

        return slot.Item;
    }

    /// <summary>
    /// Effect granted by the core inside the hook. Cores without a counterpart in Mini have none.
    /// </summary>
    public FishingAnomalyEffect GetAnomalyEffect(EntityUid hookItem)
    {
        if (GetAnomalyCore(hookItem) is not { } core || TerminatingOrDeleted(core))
            return FishingAnomalyEffect.None;

        return Prototype(core)?.ID switch
        {
            "AnomalyCoreGravity" => FishingAnomalyEffect.Gravity,
            "AnomalyCoreBluespace" => FishingAnomalyEffect.DimensionalMaterials,
            "AnomalyCoreFlesh" => FishingAnomalyEffect.Bioscramble,
            _ => FishingAnomalyEffect.None,
        };
    }

    /// <summary>
    /// tg rolls the anomaly core effect chance from 25 percent plus a bait quality bonus.
    /// </summary>
    public float GetAnomalyEffectChance(EntityUid rod)
    {
        if (GetBait(rod) is not { } bait)
            return AnomalyBaseChance;

        return AnomalyBaseChance + bait.Quality switch
        {
            FishingBaitQuality.Good => 8f,
            FishingBaitQuality.Great => 16f,
            _ => 4f,
        };
    }

    /// <summary>
    /// Rolls the anomalous lure hook core effect for a fresh catch and applies it.
    /// </summary>
    public bool TryApplyAnomalyCatchEffect(EntityUid rod, EntityUid caught)
    {
        if (!_net.IsServer ||
            !TryComp<FishComponent>(caught, out var fish) ||
            GetHookItem(rod) is not { } hookItem)
            return false;

        var effect = GetAnomalyEffect(hookItem);
        if (effect is FishingAnomalyEffect.None or FishingAnomalyEffect.Gravity)
            return false;

        if (_random.NextFloat() * 100f >= GetAnomalyEffectChance(rod))
            return false;

        switch (effect)
        {
            case FishingAnomalyEffect.DimensionalMaterials:
                ApplyDimensionalMaterials((caught, fish));
                break;
            case FishingAnomalyEffect.Bioscramble:
                ApplyBioscramble((caught, fish));
                break;
        }

        return true;
    }

    /// <summary>
    /// tg: a dimensional core can give the catch the composition of a few random materials.
    /// </summary>
    private void ApplyDimensionalMaterials(Entity<FishComponent> fish)
    {
        // Number of materials: one with 16/20, two with 3/20, three with 1/20.
        var count = _random.NextFloat() switch
        {
            < 0.8f => 1,
            < 0.95f => 2,
            _ => 3,
        };

        var available = new List<(string Material, float Weight)>(AnomalyMaterials);
        var amount = Math.Max(1, (int) MathF.Round(fish.Comp.Weight / count));
        var composition = EnsureComp<PhysicalCompositionComponent>(fish);

        composition.MaterialComposition.Clear();

        for (var i = 0; i < count && available.Count > 0; i++)
        {
            var total = 0f;
            foreach (var entry in available)
                total += entry.Weight;

            var roll = _random.NextFloat() * total;
            var index = 0;
            for (; index < available.Count - 1; index++)
            {
                roll -= available[index].Weight;
                if (roll <= 0f)
                    break;
            }

            composition.MaterialComposition[available[index].Material] = amount;
            available.RemoveAt(index);
        }
    }

    /// <summary>
    /// tg: a bioscrambler core can resize the catch and give it a random fish trait.
    /// </summary>
    private void ApplyBioscramble(Entity<FishComponent> fish)
    {
        var multiplier = _random.Pick(new[] { 0.6f, 0.8f, 1f, 1.2f, 1.4f });
        fish.Comp.Size = MathF.Max(1f, fish.Comp.Size * multiplier);
        fish.Comp.Weight = MathF.Max(1f, fish.Comp.Weight * multiplier);
        Dirty(fish.Owner, fish.Comp);

        // shortcut: Mini has no per-trait bioscramble weights, so any fish trait can be rolled.
        var traits = new List<ProtoId<FishTraitPrototype>>();
        foreach (var trait in _proto.EnumeratePrototypes<FishTraitPrototype>())
            traits.Add(trait.ID);

        if (traits.Count > 0)
            _fish.AddTrait(fish, _random.Pick(traits));
    }

    private void OnHookCoreInserted(Entity<FishingHookComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (!_net.IsServer || args.Container.ID != AnomalySlotId)
            return;

        ent.Comp.AnomalyCoreInstalled = true;
        Dirty(ent.Owner, ent.Comp);
    }

    private void OnHookCoreRemoved(Entity<FishingHookComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (!_net.IsServer || args.Container.ID != AnomalySlotId)
            return;

        ent.Comp.AnomalyCoreInstalled = false;
        Dirty(ent.Owner, ent.Comp);
    }

    private void OnHookExamined(Entity<FishingHookComponent> ent, ref ExaminedEvent args)
    {
        if (!_slots.TryGetSlot(ent.Owner, AnomalySlotId, out _))
            return;

        if (GetAnomalyCore(ent.Owner) is { } core && !TerminatingOrDeleted(core))
            args.PushMarkup(Loc.GetString("fishing-anomaly-core-examine", ("core", Name(core))));

        args.PushMarkup(Loc.GetString("fishing-anomaly-core-effects"));
    }

    #endregion

    #region Catch luck

    private float _bestEfficiency;
    private bool _bestEfficiencyCached;

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        _bestEfficiencyCached = false;
    }

    private void OnRodExamined(Entity<FishingRodComponent> ent, ref ExaminedEvent args)
    {
        if (GetBait(ent) == null)
            args.PushMarkup(Loc.GetString("fishing-rod-no-bait-examine"));

        var best = GetBestEfficiency();
        if (best <= 0f)
            return;

        var percent = (int) MathF.Round(ent.Comp.Efficiency / best * 100f);
        args.PushMarkup(Loc.GetString("fishing-rod-luck-examine", ("percent", percent)));
    }

    private void OnBaitExamined(Entity<FishingBaitComponent> ent, ref ExaminedEvent args)
    {
        var bait = ent.Comp;
        if (bait.SpinFrequencyMin <= 0f || bait.SpinFrequencyMax <= 0f)
            return;

        args.PushMarkup(Loc.GetString("fishing-lure-spin-examine",
            ("min", bait.SpinFrequencyMin.ToString("0.#")),
            ("max", bait.SpinFrequencyMax.ToString("0.#"))));
    }

    /// <summary>
    /// The highest <see cref="FishingRodComponent.Efficiency"/> among all rod prototypes, the 100% mark.
    /// </summary>
    private float GetBestEfficiency()
    {
        if (_bestEfficiencyCached)
            return _bestEfficiency;

        foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.TryGetComponent<FishingRodComponent>(out var rod, _factory) && rod.Efficiency > _bestEfficiency)
                _bestEfficiency = rod.Efficiency;
        }

        _bestEfficiencyCached = true;
        return _bestEfficiency;
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
