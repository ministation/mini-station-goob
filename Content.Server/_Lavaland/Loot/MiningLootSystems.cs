// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System;
using Robust.Shared.Containers;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Storage;
using Content.Shared.Interaction;
using System.Collections.Generic;
using Content.Goobstation.Shared.Overlays;
using Content.Goobstation.Shared.Teleportation.Systems;
using Content.Server.Body.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._EinsteinEngines.Flight;
using Content.Shared._EinsteinEngines.Language;
using Content.Shared._Lavaland.Loot;
using Content.Server._EinsteinEngines.Language;
using Content.Shared.Actions;
using Content.Shared.Administration.Systems;
using Content.Shared.Atmos;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Follower;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.StatusEffect;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Lavaland.Loot;

/// <summary>
/// Rod of Asclepius: heals the holder and living beings around them while the rod is held.
/// </summary>
public sealed class LavalandHealingAuraSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private static readonly string[] HealedTypes =
    {
        "Blunt", "Slash", "Piercing", "Heat", "Cold", "Shock", "Caustic", "Poison", "Radiation", "Asphyxiation",
    };

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<RodOfAsclepiusAuraComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            comp.Accumulator += frameTime;
            if (comp.Accumulator < comp.Interval)
                continue;

            comp.Accumulator = 0f;

            // Held or worn by a living being. Items in bags do nothing.
            var holder = Transform(uid).ParentUid;
            if (!HasComp<DamageableComponent>(holder) || !HasComp<MobStateComponent>(holder))
                continue;

            _damageable.TryChangeDamage(holder, BuildHeal(comp.HealAmount), true);

            var coords = Transform(holder).Coordinates;
            foreach (var target in _lookup.GetEntitiesInRange<DamageableComponent>(coords, comp.Range))
            {
                if (target.Owner == holder || !HasComp<MobStateComponent>(target))
                    continue;

                _damageable.TryChangeDamage(target.Owner, BuildHeal(comp.HealAmount), true);
            }
        }
    }

    private static DamageSpecifier BuildHeal(float amount)
    {
        var heal = new DamageSpecifier();
        foreach (var type in HealedTypes)
            heal.DamageDict[type] = -amount;
        return heal;
    }
}

/// <summary>
/// Immortality talisman: short window of absolute invulnerability on use.
/// </summary>
public sealed class ImmortalityTalismanSystem : EntitySystem
{
    [Dependency] private readonly SharedGodmodeSystem _godmode = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImmortalityTalismanComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<ImmortalityTalismanComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var user = args.User;
        if (HasComp<GodmodeComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("immortality-talisman-active-already"), user, user);
            return;
        }

        _godmode.EnableGodmode(user);
        _audio.PlayPvs("/Audio/Effects/teleport_arrival.ogg", user);
        _popup.PopupEntity(Loc.GetString("immortality-talisman-activated"), user, user);

        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(ent.Comp.Duration), () =>
        {
            if (TerminatingOrDeleted(user) || !Exists(user))
                return;

            _godmode.DisableGodmode(user);
            _popup.PopupEntity(Loc.GetString("immortality-talisman-expired"), user, user);
        });
    }
}

/// <summary>
/// Memento mori: the wearer is kept alive while the necklace is bound to them,
/// and turns to dust when it is taken off.
/// </summary>
public sealed class MementoMoriSystem : EntitySystem
{
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MementoMoriComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<MementoMoriBoundComponent, MobStateChangedEvent>(OnBoundMobStateChanged);
        SubscribeLocalEvent<MementoMoriComponent, GotUnequippedEvent>(OnNecklaceUnequipped);
        SubscribeLocalEvent<MementoMoriComponent, EntityTerminatingEvent>(OnNecklaceTerminating);
    }

    private void OnEquipped(Entity<MementoMoriComponent> ent, ref GotEquippedEvent args)
    {
        if (HasComp<MementoMoriBoundComponent>(args.Equipee))
        {
            _popup.PopupEntity(Loc.GetString("memento-mori-already-bound"), args.Equipee, args.Equipee);
            return;
        }

        var bound = EnsureComp<MementoMoriBoundComponent>(args.Equipee);
        bound.Necklace = ent;
        _popup.PopupEntity(Loc.GetString("memento-mori-bound"), args.Equipee, args.Equipee);
    }

    private void OnBoundMobStateChanged(Entity<MementoMoriBoundComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (!Exists(ent.Comp.Necklace) || TerminatingOrDeleted(ent.Comp.Necklace))
            return;

        _rejuvenate.PerformRejuvenate(ent);
        _popup.PopupEntity(Loc.GetString("memento-mori-refused-death"), ent, ent);
    }

    private void OnNecklaceUnequipped(Entity<MementoMoriComponent> ent, ref GotUnequippedEvent args)
    {
        DustIfBound(args.Equipee, ent);
    }

    private void OnNecklaceTerminating(Entity<MementoMoriComponent> ent, ref EntityTerminatingEvent args)
    {
        DustIfBound(Transform(ent).ParentUid, ent);
    }

    private void DustIfBound(EntityUid wearer, EntityUid necklace)
    {
        if (TerminatingOrDeleted(wearer) || !TryComp<MementoMoriBoundComponent>(wearer, out var bound) ||
            bound.Necklace != necklace)
            return;

        RemComp<MementoMoriBoundComponent>(wearer);
        _popup.PopupEntity(Loc.GetString("memento-mori-pact-broken"), wearer, wearer);

        if (HasComp<BodyComponent>(wearer))
            _body.GibBody(wearer);
    }
}

/// <summary>
/// Strange elixir: grants permanent flight on use.
/// </summary>
public sealed class FlightPotionSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FlightPotionComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<FlightPotionComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        EnsureComp<FlightComponent>(args.User);
        _audio.PlayPvs("/Audio/Items/drink.ogg", ent);
        _popup.PopupEntity(Loc.GetString("strange-elixir-drunk"), args.User, args.User);
        QueueDel(ent);
    }
}

/// <summary>
/// Book of Babel: teaches every known language on use.
/// </summary>
public sealed class BookOfBabelSystem : EntitySystem
{
    [Dependency] private readonly LanguageSystem _language = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BookOfBabelComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<BookOfBabelComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        foreach (var language in _protoMan.EnumeratePrototypes<LanguagePrototype>())
            _language.AddLanguage(args.User, language.ID);

        _audio.PlayPvs("/Audio/Items/drink.ogg", ent);
        _popup.PopupEntity(Loc.GetString("book-of-babel-read"), args.User, args.User);
        QueueDel(ent);
    }
}

/// <summary>
/// Spooky lantern: releases a friendly wisp that follows the user and lends them thermal vision.
/// </summary>
public sealed class WispLanternSystem : EntitySystem
{
    [Dependency] private readonly FollowerSystem _follower = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WispLanternComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<WispLanternComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnUseInHand(Entity<WispLanternComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.Wisp != null)
        {
            ReturnWisp(ent, args.User);
            _popup.PopupEntity(Loc.GetString("wisp-lantern-returned"), args.User, args.User);
            return;
        }

        var spawned = Spawn(ent.Comp.WispProto, Transform(args.User).Coordinates);
        _follower.StartFollowingEntity(spawned, args.User);
        ent.Comp.Wisp = spawned;
        GrantThermalVision(args.User, ent.Comp);
        _popup.PopupEntity(Loc.GetString("wisp-lantern-released"), args.User, args.User);
    }

    private void OnTerminating(Entity<WispLanternComponent> ent, ref EntityTerminatingEvent args)
    {
        ReturnWisp(ent, Transform(ent).ParentUid);
    }

    private void ReturnWisp(Entity<WispLanternComponent> ent, EntityUid user)
    {
        if (ent.Comp.Wisp is { } wisp && !TerminatingOrDeleted(wisp))
            QueueDel(wisp);

        ent.Comp.Wisp = null;
        RevokeThermalVision(user, ent.Comp);
    }

    private void GrantThermalVision(EntityUid user, WispLanternComponent comp)
    {
        if (HasComp<ThermalVisionComponent>(user))
            return;

        var thermal = EnsureComp<ThermalVisionComponent>(user);
        thermal.IsActive = true;
        thermal.IsEquipment = false;

        if (thermal.ToggleActionEntity is { } action && !TerminatingOrDeleted(action))
            _actions.RemoveAction(user, action);
        thermal.ToggleActionEntity = null;

        Dirty(user, thermal);
        comp.GaveThermalVision = true;
    }

    private void RevokeThermalVision(EntityUid user, WispLanternComponent comp)
    {
        if (!comp.GaveThermalVision || TerminatingOrDeleted(user))
            return;

        RemComp<ThermalVisionComponent>(user);
        comp.GaveThermalVision = false;
    }
}

/// <summary>
/// Warp cube: links a red cube to a blue one and teleports the user between them.
/// </summary>
public sealed class WarpCubeSystem : EntitySystem
{
    [Dependency] private readonly TeleportSystem _teleport = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarpCubeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<WarpCubeComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<WarpCubeComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnMapInit(Entity<WarpCubeComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.PartnerProto is not { } proto || ent.Comp.SpawnedPartner || ent.Comp.Partner != null)
            return;

        ent.Comp.SpawnedPartner = true;

        var partner = Spawn(proto, Transform(ent).Coordinates);
        if (!TryComp<WarpCubeComponent>(partner, out var partnerComp))
            return;

        partnerComp.SpawnedPartner = true;
        partnerComp.Partner = ent;
        ent.Comp.Partner = partner;
    }

    private void OnUseInHand(Entity<WarpCubeComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.Partner is not { } partner || TerminatingOrDeleted(partner))
        {
            _popup.PopupEntity(Loc.GetString("warp-cube-unlinked"), args.User, args.User);
            return;
        }

        _audio.PlayPvs("/Audio/Effects/teleport_departure.ogg", args.User);

        if (!_teleport.Teleport(args.User, Transform(partner).Coordinates, args.User, predicted: false))
            return;

        _audio.PlayPvs("/Audio/Effects/teleport_arrival.ogg", args.User);
        _popup.PopupEntity(Loc.GetString("warp-cube-teleported"), args.User, args.User);
    }

    private void OnTerminating(Entity<WarpCubeComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.Partner is not { } partner || TerminatingOrDeleted(partner))
            return;

        if (TryComp<WarpCubeComponent>(partner, out var partnerComp))
            partnerComp.Partner = null;
    }
}

/// <summary>
/// Brimdust sac: coats the user in explosive brimdust for a minute. Taking brute damage sets
/// off a small blast and burns one stack.
/// TODO: TG also washes the coating off with water, the fork has no clean wet event for that.
/// </summary>
public sealed class BrimdustCoatingSystem : EntitySystem
{
    private const string CoatingStatusEffect = "BrimdustCoating";
    private const string CoatingExplosion = "LavalandBrimstone";
    private const int MaxStacks = 3;

    private static readonly TimeSpan CoatingDuration = TimeSpan.FromSeconds(60);

    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BrimdustSacComponent, UseInHandEvent>(OnSacUsed);
        SubscribeLocalEvent<BrimdustCoatingComponent, DamageChangedEvent>(OnCoatingDamaged);
    }

    private void OnSacUsed(Entity<BrimdustSacComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var user = args.User;
        if (_status.TryAddStatusEffect<BrimdustCoatingComponent>(user, CoatingStatusEffect, CoatingDuration, true)
            && TryComp<BrimdustCoatingComponent>(user, out var coating))
        {
            coating.Stacks = Math.Min(coating.Stacks + ent.Comp.Stacks, MaxStacks);
            _popup.PopupEntity(Loc.GetString("brimdust-coating-applied", ("stacks", coating.Stacks)), user, user);
        }

        QueueDel(ent);
    }

    private void OnCoatingDamaged(Entity<BrimdustCoatingComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta is null)
            return;

        if (!args.DamageDelta.TryGetDamageInGroup(_protoMan.Index<DamageGroupPrototype>("Brute"), out var brute)
            || brute <= FixedPoint2.Zero)
            return;

        Spawn("ExplosionEffectGrenade", Transform(ent).Coordinates);
        _explosion.QueueExplosion(Transform(ent).MapPosition, CoatingExplosion, 6f, 3f, 2f, cause: ent.Owner, canCreateVacuum: false, addLog: false);

        if (--ent.Comp.Stacks > 0)
            return;

        _status.TryRemoveStatusEffect(ent.Owner, CoatingStatusEffect);
    }
}

/// <summary>
/// Paradox bag: an item put into one bag of a linked pair moves to the other bag if it fits.
/// </summary>
public sealed class ParadoxBagSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;

    // Transfers are deferred one tick so that storage bookkeeping for the source bag is finished first.
    private readonly List<(EntityUid Bag, EntityUid Item)> _pending = new();
    private bool _moving;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ParadoxBagComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<ParadoxBagComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<ParadoxBagComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnAfterInteract(Entity<ParadoxBagComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || target == ent.Owner)
            return;

        if (!TryComp<ParadoxBagComponent>(target, out var targetComp))
            return;

        args.Handled = true;

        if (ent.Comp.Partner != null || targetComp.Partner != null)
        {
            _popup.PopupEntity(Loc.GetString("paradox-bag-already-linked"), args.User, args.User);
            return;
        }

        ent.Comp.Partner = target;
        targetComp.Partner = ent;
        _popup.PopupEntity(Loc.GetString("paradox-bag-linked"), args.User, args.User);
    }

    private void OnInserted(Entity<ParadoxBagComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (_moving)
            return;

        if (!TryComp(ent, out StorageComponent? storage) || args.Container != storage.Container)
            return;

        // Never move bags: a bag inside its own pair would bounce forever.
        if (HasComp<ParadoxBagComponent>(args.Entity))
            return;

        _pending.Add((ent, args.Entity));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pending.Count == 0)
            return;

        var pending = _pending.ToArray();
        _pending.Clear();

        foreach (var (bag, item) in pending)
        {
            TryTransfer(bag, item);
        }
    }

    private void TryTransfer(EntityUid bag, EntityUid item)
    {
        if (_moving || TerminatingOrDeleted(bag) || TerminatingOrDeleted(item))
            return;

        if (!TryComp<ParadoxBagComponent>(bag, out var comp) || comp.Partner is not { } partner)
            return;

        if (!TryComp(bag, out StorageComponent? storage) || !storage.Container.Contains(item))
            return;

        if (!TryComp(partner, out StorageComponent? partnerStorage) || !_storage.CanInsert(partner, item, out _, partnerStorage))
            return;

        _moving = true;
        try
        {
            _storage.Insert(partner, item, out _, storageComp: partnerStorage, playSound: false);
        }
        finally
        {
            _moving = false;
        }
    }

    private void OnTerminating(Entity<ParadoxBagComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.Partner is not { } partner || TerminatingOrDeleted(partner))
            return;

        if (TryComp<ParadoxBagComponent>(partner, out var partnerComp))
            partnerComp.Partner = null;
    }
}
