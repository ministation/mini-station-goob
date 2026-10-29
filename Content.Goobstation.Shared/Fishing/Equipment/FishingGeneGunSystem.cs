// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Linq;
using Content.Goobstation.Shared.Fishing.Components;
using Content.Goobstation.Shared.Fishing.Prototypes;
using Content.Goobstation.Shared.Fishing.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.Fishing.Systems;

/// <summary>
/// Gene gun: takes an empty syringe and extracts one trait from a fish into a fish trait injector,
/// then injects stored traits into other fish.
/// </summary>
public sealed partial class FishingGeneGunSystem : EntitySystem
{
    private const string InjectorPrototype = "FishGeneInjector";

    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private FishSystem _fish = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishingGeneGunComponent, InteractUsingEvent>(OnGunInteractUsing);
        SubscribeLocalEvent<FishingGeneGunComponent, AfterInteractEvent>(OnGunAfterInteract);
        SubscribeLocalEvent<FishingGeneGunComponent, UseInHandEvent>(OnGunUseInHand);
        SubscribeLocalEvent<FishingGeneGunComponent, ItemSlotInsertAttemptEvent>(OnGunInsertAttempt);
        SubscribeLocalEvent<FishingGeneGunComponent, ExaminedEvent>(OnGunExamined);
        SubscribeLocalEvent<FishGeneInjectorComponent, AfterInteractEvent>(OnInjectorAfterInteract);
        SubscribeLocalEvent<FishGeneInjectorComponent, ExaminedEvent>(OnInjectorExamined);
    }

    #region Loading

    private void OnGunInteractUsing(Entity<FishingGeneGunComponent> ent, ref InteractUsingEvent args)
    {
        if (!_net.IsServer || args.Handled || !IsInjectorCandidate(args.Used))
            return;

        args.Handled = true;
        TryLoad(ent, args.Used, args.User);
    }

    private void OnGunAfterInteract(Entity<FishingGeneGunComponent> ent, ref AfterInteractEvent args)
    {
        if (!_net.IsServer || args.Handled || args.Target is not { } target)
            return;

        if (HasComp<FishComponent>(target))
        {
            args.Handled = true;
            ApplyToFish(ent, target, args.User);
            return;
        }

        if (!IsInjectorCandidate(target))
            return;

        args.Handled = true;
        TryLoad(ent, target, args.User);
    }

    private bool IsInjectorCandidate(EntityUid uid)
    {
        return HasComp<InjectorComponent>(uid) || HasComp<FishGeneInjectorComponent>(uid);
    }

    private void TryLoad(Entity<FishingGeneGunComponent> gun, EntityUid item, EntityUid user)
    {
        if (!_slots.TryGetSlot(gun.Owner, FishingGeneGunComponent.InjectorSlotId, out var slot))
            return;

        if (slot.HasItem)
        {
            _popup.PopupEntity(Loc.GetString("fishing-gene-gun-full"), gun.Owner, user);
            return;
        }

        if (HasComp<InjectorComponent>(item) && !IsEmpty(item))
        {
            _popup.PopupEntity(Loc.GetString("fishing-gene-gun-syringe-not-empty"), gun.Owner, user);
            return;
        }

        if (_slots.TryInsert(gun.Owner, slot, item, user, excludeUserAudio: true))
            _popup.PopupEntity(Loc.GetString("fishing-gene-gun-loaded"), gun.Owner, user);
    }

    /// <summary>
    /// Stops non-empty syringes from being loaded through verbs as well.
    /// </summary>
    private void OnGunInsertAttempt(Entity<FishingGeneGunComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Slot.ID != FishingGeneGunComponent.InjectorSlotId)
            return;

        if (HasComp<InjectorComponent>(args.Item) && !IsEmpty(args.Item))
            args.Cancelled = true;
    }

    private bool IsEmpty(EntityUid item)
    {
        if (!TryComp<InjectorComponent>(item, out var injector))
            return true;

        if (!_solution.TryGetSolution(item, injector.SolutionName, out _, out var solution))
            return true;

        return solution.Volume <= 0;
    }

    private void OnGunUseInHand(Entity<FishingGeneGunComponent> ent, ref UseInHandEvent args)
    {
        if (!_net.IsServer || args.Handled)
            return;

        if (!_slots.TryGetSlot(ent.Owner, FishingGeneGunComponent.InjectorSlotId, out var slot) || !slot.HasItem)
        {
            _popup.PopupEntity(Loc.GetString("fishing-gene-gun-empty"), ent.Owner, args.User);
            return;
        }

        args.Handled = true;
        _slots.TryEjectToHands(ent.Owner, slot, args.User, excludeUserAudio: true);
        _popup.PopupEntity(Loc.GetString("fishing-gene-gun-unloaded"), ent.Owner, args.User);
    }

    #endregion

    #region Applying

    private void ApplyToFish(Entity<FishingGeneGunComponent> gun, EntityUid fishUid, EntityUid user)
    {
        if (!TryComp<FishComponent>(fishUid, out var fish))
            return;

        if (!_slots.TryGetSlot(gun.Owner, FishingGeneGunComponent.InjectorSlotId, out var slot) || slot.Item is not { } loaded)
        {
            _popup.PopupEntity(Loc.GetString("fishing-gene-gun-empty"), gun.Owner, user);
            return;
        }

        if (!fish.Alive)
        {
            _popup.PopupEntity(Loc.GetString("fishing-gene-gun-dead"), fishUid, user);
            return;
        }

        if (HasComp<FishGeneInjectorComponent>(loaded))
            InjectTrait(fishUid, (fishUid, fish), loaded, user);
        else
            ExtractTrait(gun, slot, (fishUid, fish), user);
    }

    private void ExtractTrait(Entity<FishingGeneGunComponent> gun, ItemSlot slot, Entity<FishComponent> fish, EntityUid user)
    {
        var traits = _fish.GetTraits(fish.Comp).ToList();

        if (traits.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("fishing-gene-gun-no-traits"), fish.Owner, user);
            return;
        }

        var trait = traits[0];

        if (!_slots.TryEject(gun.Owner, slot, null, out var ejected, excludeUserAudio: true) || ejected is not { } syringe)
            return;

        QueueDel(syringe);
        _fish.RemoveTrait(fish, trait);

        var injector = Spawn(InjectorPrototype, Transform(gun.Owner).Coordinates);
        var injectorComp = EnsureComp<FishGeneInjectorComponent>(injector);
        injectorComp.Trait = trait;
        Dirty(injector, injectorComp);
        _meta.SetEntityName(injector, Loc.GetString("fishing-gene-injector-named", ("trait", TraitName(trait))));

        _slots.TryInsert(gun.Owner, slot, injector, user, excludeUserAudio: true);

        _popup.PopupEntity(Loc.GetString("fishing-gene-gun-extracted", ("trait", TraitName(trait))), fish.Owner, user);
        MarkGeneGunned(fish);
    }

    private void InjectTrait(EntityUid fishUid, Entity<FishComponent> fish, EntityUid injector, EntityUid user)
    {
        if (!TryComp<FishGeneInjectorComponent>(injector, out var comp) || comp.Trait is not { } trait)
        {
            _popup.PopupEntity(Loc.GetString("fishing-gene-injector-empty"), fishUid, user);
            return;
        }

        if (_fish.HasTrait(fish.Comp, trait))
        {
            _popup.PopupEntity(Loc.GetString("fishing-gene-has-trait", ("trait", TraitName(trait))), fishUid, user);
            return;
        }

        _fish.AddTrait(fish, trait);
        _popup.PopupEntity(Loc.GetString("fishing-gene-injected", ("trait", TraitName(trait))), fishUid, user);
        QueueDel(injector);
        MarkGeneGunned(fish);
    }

    private void OnInjectorAfterInteract(Entity<FishGeneInjectorComponent> ent, ref AfterInteractEvent args)
    {
        if (!_net.IsServer || args.Handled || args.Target is not { } target)
            return;

        if (!TryComp<FishComponent>(target, out var fish))
            return;

        args.Handled = true;

        if (!fish.Alive)
        {
            _popup.PopupEntity(Loc.GetString("fishing-gene-gun-dead"), target, args.User);
            return;
        }

        InjectTrait(target, (target, fish), ent.Owner, args.User);
    }

    /// <summary>
    /// The first gene gun operation marks the fish, any further one kills it.
    /// </summary>
    private void MarkGeneGunned(Entity<FishComponent> fish)
    {
        if (fish.Comp.GeneGunned)
        {
            _fish.SetAlive(fish, false);
            return;
        }

        fish.Comp.GeneGunned = true;
        Dirty(fish, fish.Comp);
    }

    private string TraitName(ProtoId<FishTraitPrototype> trait)
    {
        return _proto.TryIndex(trait, out FishTraitPrototype? proto) ? Loc.GetString(proto.Name) : trait.Id;
    }

    #endregion

    #region Examine

    private void OnGunExamined(Entity<FishingGeneGunComponent> ent, ref ExaminedEvent args)
    {
        if (!_slots.TryGetSlot(ent.Owner, FishingGeneGunComponent.InjectorSlotId, out var slot) || slot.Item is not { } item)
        {
            args.PushMarkup(Loc.GetString("fishing-gene-gun-examine-empty"));
            return;
        }

        var key = HasComp<FishGeneInjectorComponent>(item)
            ? "fishing-gene-gun-examine-inject"
            : "fishing-gene-gun-examine-extract";

        args.PushMarkup(Loc.GetString(key, ("item", item)));
    }

    private void OnInjectorExamined(Entity<FishGeneInjectorComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Trait is not { } trait)
            return;

        args.PushMarkup(Loc.GetString("fishing-gene-injector-examine", ("trait", TraitName(trait))));
    }

    #endregion
}
