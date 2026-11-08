// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Vehicles;
using Content.Server.DoAfter;
using Content.Server.NPC.HTN;
using Content.Shared._Lavaland.Mobs;
using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// Taming, saddling and riding of lavaland fauna, following TG's tameable/ridable components:
/// feed a mob its food to tame it, affix a saddle if it needs one, then buckle on and drive it.
/// Riding reuses the fork's <see cref="VehicleComponent"/> and <see cref="StrapComponent"/>;
/// the only extra work is parking the mob AI and swapping its walk speed while a rider is on.
/// </summary>
public sealed class LavalandTamingSystem : EntitySystem
{
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly NpcFactionSystem _factions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TameableMobComponent, InteractUsingEvent>(OnFeed);
        SubscribeLocalEvent<MountableMobComponent, InteractUsingEvent>(OnSaddleUsed);
        SubscribeLocalEvent<MountableMobComponent, LavalandSaddleDoAfterEvent>(OnSaddled);
        SubscribeLocalEvent<MountableMobComponent, StrapAttemptEvent>(OnStrapAttempt);
        SubscribeLocalEvent<MountableMobComponent, StrappedEvent>(OnRiderStrapped);
        SubscribeLocalEvent<MountableMobComponent, UnstrappedEvent>(OnRiderUnstrapped);
        SubscribeLocalEvent<MountableMobComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnFeed(Entity<TameableMobComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || HasComp<TamedMobComponent>(ent) || !IsTameFood(ent.Comp, args.Used))
            return;

        args.Handled = true;

        // TG starts at tame_chance and adds bonus_tame_chance after every failure.
        if (ent.Comp.CurrentChance <= 0f)
            ent.Comp.CurrentChance = ent.Comp.TameChance;

        _popup.PopupEntity(Loc.GetString("lavaland-tame-eats", ("target", ent)), ent, args.User);

        if (!HasComp<StackComponent>(args.Used))
            QueueDel(args.Used);
        else
            _stack.TryUse(args.Used, 1);

        if (_random.Prob(ent.Comp.CurrentChance / 100f))
            Tame((ent.Owner, ent.Comp), args.User);
        else
            ent.Comp.CurrentChance += ent.Comp.BonusTameChance;
    }

    private void Tame(Entity<TameableMobComponent> ent, EntityUid tamer)
    {
        RemComp<TameableMobComponent>(ent);

        var tamed = AddComp<TamedMobComponent>(ent);
        tamed.Tamer = tamer;

        // TG: tamed mobs befriend the tamer and stop treating them as prey.
        _factions.IgnoreEntity(ent.Owner, tamer);

        Spawn("EffectHearts", Transform(ent).Coordinates);
        _popup.PopupEntity(Loc.GetString("lavaland-tame-success", ("target", ent)), ent, tamer);

        // Goldgrubs are rideable as soon as they trust you, no saddle required.
        if (TryComp<MountableMobComponent>(ent, out var mountable) && mountable.SaddleItems.Count == 0)
            MakeRideable((ent.Owner, mountable));
    }

    private void OnSaddleUsed(Entity<MountableMobComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled
            || ent.Comp.Saddled
            || !MatchesAny(ent.Comp.SaddleItems, args.Used))
            return;

        args.Handled = true;

        if (!HasComp<TamedMobComponent>(ent))
        {
            _popup.PopupEntity(Loc.GetString("lavaland-saddle-untamed", ("target", ent)), ent, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.SaddleDelay,
            new LavalandSaddleDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            DistanceThreshold = 2f,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupEntity(Loc.GetString("lavaland-saddle-affixing", ("target", ent)), ent, args.User);
    }

    private void OnSaddled(Entity<MountableMobComponent> ent, ref LavalandSaddleDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (args.Used is { } saddle)
            QueueDel(saddle);

        MakeRideable(ent);
    }

    private void MakeRideable(Entity<MountableMobComponent> ent)
    {
        if (ent.Comp.Saddled)
            return;

        ent.Comp.Saddled = true;

        // Fork vehicle machinery: the mob gets Vehicle from here on, so a rider buckled to the
        // Strap from the prototype relays their movement to it and is drawn above it.
        var vehicle = EnsureComp<VehicleComponent>(ent);
        vehicle.EngineRunning = true;
        // TG riders keep their hands free, so no virtual reins item like cars get.
        vehicle.RequiredHands = 0;
        Dirty(ent, vehicle);

        _popup.PopupEntity(Loc.GetString("lavaland-saddle-ready", ("target", ent)), ent);
    }

    private void OnStrapAttempt(Entity<MountableMobComponent> ent, ref StrapAttemptEvent args)
    {
        if (ent.Comp.Saddled)
            return;

        args.Cancelled = true;

        if (args.User is { } user)
            _popup.PopupEntity(Loc.GetString("lavaland-saddle-required", ("target", ent)), ent, user);
    }

    private void OnRiderStrapped(Entity<MountableMobComponent> ent, ref StrappedEvent args)
    {
        ent.Comp.Riding = true;

        // The AI would fight the rider's input over the same mover, so park it while mounted.
        if (TryComp<HTNComponent>(ent, out var htn))
            _htn.SetHTNEnabled((ent.Owner, htn), false, 1f);

        _movementSpeed.RefreshMovementSpeedModifiers(ent);
    }

    private void OnRiderUnstrapped(Entity<MountableMobComponent> ent, ref UnstrappedEvent args)
    {
        ent.Comp.Riding = false;

        if (TryComp<HTNComponent>(ent, out var htn))
            _htn.SetHTNEnabled((ent.Owner, htn), true, 1f);

        _movementSpeed.RefreshMovementSpeedModifiers(ent);
    }

    private void OnRefreshSpeed(Entity<MountableMobComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!ent.Comp.Riding
            || !TryComp<MovementSpeedModifierComponent>(ent, out var move)
            || move.BaseWalkSpeed <= 0f)
            return;

        var factor = ent.Comp.RideSpeed / move.BaseWalkSpeed;
        args.ModifySpeed(factor, factor);
    }

    private bool IsTameFood(TameableMobComponent comp, EntityUid food)
    {
        if (comp.FoodTags.Count > 0 && _tag.HasAnyTag(food, comp.FoodTags))
            return true;

        return MatchesAny(comp.Foods, food);
    }

    private bool MatchesAny(HashSet<ProtoId<EntityPrototype>> prototypes, EntityUid item)
    {
        return prototypes.Count > 0
               && Prototype(item)?.ID is { } id
               && prototypes.Contains(id);
    }
}
