// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Lavaland.Weapons.Berserker;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Stacks;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Reflect;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Weapons;

/// <summary>
/// TG's berserker armor set: the helmet feeds on pain to charge berserk mode.
/// While active the wearer swings 4x faster, shrugs off half of all brute damage,
/// reflects projectiles, cannot shoot guns and cannot take the helmet off.
/// The charge drains until it runs out, then everything reverts.
/// Feeding ash drake hide to an armor piece permanently empowers it.
/// </summary>
public sealed class BerserkerSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly SharedStackSystem _stacks = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly string[] BruteTypes = { "Blunt", "Slash", "Piercing" };

    /// <summary>
    /// Extra brute resist granted by drakeskin empowerment, on top of the base armor.
    /// </summary>
    private const float EmpowerMultiplier = 0.6f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandBerserkerComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnWearerDamaged);
        SubscribeLocalEvent<LavalandBerserkerComponent, InventoryRelayedEvent<ProjectileReflectAttemptEvent>>(OnWearerProjectile);
        SubscribeLocalEvent<LavalandBerserkerComponent, BerserkModeActionEvent>(OnAction);
        SubscribeLocalEvent<LavalandBerserkerComponent, GotUnequippedEvent>(OnHelmetUnequipped);
        SubscribeLocalEvent<LavalandBerserkerComponent, ComponentShutdown>(OnHelmetShutdown);

        SubscribeLocalEvent<BerserkingComponent, DamageModifyEvent>(OnBerserkDamage);
        SubscribeLocalEvent<BerserkingComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<BerserkingComponent, MeleeAttackEvent>(OnMeleeAttack);
        SubscribeLocalEvent<BerserkingComponent, MobStateChangedEvent>(OnWearerStateChanged);
        SubscribeLocalEvent<BerserkingComponent, ComponentShutdown>(OnBerserkingShutdown);

        // Broadcasts raised on other entities (the gun, the unequip attempt) - filter by wearer / item.
        SubscribeLocalEvent<ShotAttemptedEvent>(OnShotAttempt);
        SubscribeLocalEvent<IsUnequippingAttemptEvent>(OnUnequipAttempt);

        SubscribeLocalEvent<LavalandBerserkerGearComponent, InteractUsingEvent>(OnEmpower);
        SubscribeLocalEvent<LavalandBerserkerEmpoweredComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnEmpoweredDamage);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LavalandBerserkerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Active)
                continue;

            if (comp.Wearer is not { } wearer || !TryComp<BerserkingComponent>(wearer, out var berserk) || berserk.Helmet != uid)
            {
                comp.Active = false;
                comp.Wearer = null;
                continue;
            }

            comp.Charge = Math.Clamp(comp.Charge - comp.DrainPerSecond * frameTime, 0f, comp.MaxCharge);
            if (comp.Charge <= 0f)
                EndBerserk(uid, comp, wearer);
        }
    }

    #region Charge

    private void OnWearerDamaged(Entity<LavalandBerserkerComponent> ent, ref InventoryRelayedEvent<DamageModifyEvent> args)
    {
        var comp = ent.Comp;
        if (comp.Active)
            return;

        var damage = args.Args.OriginalDamage.GetTotal();
        if (damage <= FixedPoint2.Zero)
            return;

        AddCharge(ent, damage.Float() * comp.DamageToCharge);
    }

    private void OnWearerProjectile(Entity<LavalandBerserkerComponent> ent, ref InventoryRelayedEvent<ProjectileReflectAttemptEvent> args)
    {
        var comp = ent.Comp;
        if (comp.Active)
            return;

        var damage = args.Args.Component.Damage.GetTotal();
        if (damage <= FixedPoint2.Zero)
            return;

        // DamageModifyEvent already added the base amount; add it again to reach the projectile multiplier.
        AddCharge(ent, damage.Float() * comp.DamageToCharge);
    }

    private void AddCharge(Entity<LavalandBerserkerComponent> ent, float amount)
    {
        var comp = ent.Comp;
        var wasFull = comp.Charge >= comp.MaxCharge;
        comp.Charge = Math.Clamp(comp.Charge + amount, 0f, comp.MaxCharge);

        if (!wasFull && comp.Charge >= comp.MaxCharge
            && _inventory.TryGetSlotEntity(ent, "head", out var wearer))
        {
            _popup.PopupEntity(Loc.GetString("berserker-charged"), wearer.Value, wearer.Value);
        }
    }

    #endregion

    #region Berserk mode

    private void OnAction(Entity<LavalandBerserkerComponent> ent, ref BerserkModeActionEvent args)
    {
        var comp = ent.Comp;
        if (comp.Active)
            return;

        if (!_inventory.TryGetSlotContainer(args.Performer, "head", out var head, out _)
            || head.ContainedEntity != ent.Owner)
        {
            _popup.PopupEntity(Loc.GetString("berserker-not-worn"), args.Performer, args.Performer);
            return;
        }

        if (comp.Charge < comp.MaxCharge)
        {
            _popup.PopupEntity(
                Loc.GetString("berserker-not-charged", ("charge", (int) (comp.Charge / comp.MaxCharge * 100))),
                args.Performer, args.Performer);
            return;
        }

        StartBerserk(ent, comp, args.Performer);
    }

    private void StartBerserk(Entity<LavalandBerserkerComponent> ent, LavalandBerserkerComponent comp, EntityUid wearer)
    {
        comp.Active = true;
        comp.Wearer = wearer;

        var berserk = AddComp<BerserkingComponent>(wearer);
        berserk.Helmet = ent.Owner;

        // A red rage glow around the wearer (server-settable stand-in for TG's atom tint).
        if (!HasComp<SharedPointLightComponent>(wearer))
            berserk.LightAdded = true;

        var light = _light.EnsureLight(wearer);
        _light.SetColor(wearer, comp.BerserkColor, light);
        _light.SetRadius(wearer, 1.5f, light);
        _light.SetEnergy(wearer, 1.2f, light);

        // While berserked the helmet throws incoming fire back at the shooter.
        EntityManager.AddComponent(ent.Owner, new ReflectComponent
        {
            ReflectProb = 1f,
            Reflects = ReflectType.Energy | ReflectType.NonEnergy,
        });

        _movement.RefreshMovementSpeedModifiers(wearer);
        _audio.PlayPvs("/Audio/Magic/staff_healing.ogg", wearer);
        _popup.PopupEntity(Loc.GetString("berserker-mode-entered"), wearer, wearer, PopupType.LargeCaution);
    }

    private void EndBerserk(EntityUid uid, LavalandBerserkerComponent comp, EntityUid wearer)
    {
        comp.Active = false;
        comp.Wearer = null;

        if (TryComp<BerserkingComponent>(wearer, out var berserk))
        {
            if (berserk.LightAdded)
                RemComp<SharedPointLightComponent>(wearer);
            RemComp<BerserkingComponent>(wearer);
        }

        RemComp<ReflectComponent>(uid);

        _movement.RefreshMovementSpeedModifiers(wearer);
        _audio.PlayPvs("/Audio/Magic/staff_animation.ogg", wearer);
        _popup.PopupEntity(Loc.GetString("berserker-mode-exited"), wearer, wearer);
    }

    private void OnBerserkingShutdown(Entity<BerserkingComponent> ent, ref ComponentShutdown args)
    {
        // The wearer component is the source of truth for the rage light, so always remove it here.
        if (ent.Comp.LightAdded)
            RemComp<SharedPointLightComponent>(ent);

        if (TryComp<LavalandBerserkerComponent>(ent.Comp.Helmet, out var helmet))
        {
            helmet.Active = false;
            helmet.Wearer = null;
            RemComp<ReflectComponent>(ent.Comp.Helmet);
        }
    }

    private void OnHelmetUnequipped(Entity<LavalandBerserkerComponent> ent, ref GotUnequippedEvent args)
    {
        if (ent.Comp.Active && ent.Comp.Wearer is { } wearer)
            EndBerserk(ent, ent.Comp, wearer);
    }

    private void OnHelmetShutdown(Entity<LavalandBerserkerComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Active && ent.Comp.Wearer is { } wearer)
            RemComp<BerserkingComponent>(wearer);
    }

    private void OnWearerStateChanged(Entity<BerserkingComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Critical && args.NewMobState != MobState.Dead)
            return;

        if (!TryComp<LavalandBerserkerComponent>(ent.Comp.Helmet, out var helmet))
            return;

        EndBerserk(ent.Comp.Helmet, helmet, ent);
    }

    private void OnUnequipAttempt(IsUnequippingAttemptEvent args)
    {
        if (args.Cancelled || !TryComp<LavalandBerserkerComponent>(args.Equipment, out var comp) || !comp.Active)
            return;

        args.Cancel();
        if (_net.IsServer && comp.Wearer is { } wearer)
            _popup.PopupEntity(Loc.GetString("berserker-locked"), wearer, wearer);
    }

    #endregion

    #region Berserk effects

    private void OnBerserkDamage(Entity<BerserkingComponent> ent, ref DamageModifyEvent args)
    {
        if (!TryComp<LavalandBerserkerComponent>(ent.Comp.Helmet, out var comp))
            return;

        foreach (var type in BruteTypes)
        {
            if (args.Damage.DamageDict.TryGetValue(type, out var value))
                args.Damage.DamageDict[type] = value * comp.BruteResistMultiplier;
        }
    }

    private void OnRefreshSpeed(Entity<BerserkingComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!TryComp<LavalandBerserkerComponent>(ent.Comp.Helmet, out var comp))
            return;

        args.ModifySpeed(comp.MovespeedModifier, comp.MovespeedModifier);
    }

    private void OnMeleeAttack(Entity<BerserkingComponent> ent, ref MeleeAttackEvent args)
    {
        if (!TryComp<MeleeWeaponComponent>(args.Weapon, out var weapon))
            return;

        if (!TryComp<LavalandBerserkerComponent>(ent.Comp.Helmet, out var comp))
            return;

        var rate = weapon.NextAttack - _timing.CurTime;
        if (rate <= TimeSpan.Zero)
            return;

        weapon.NextAttack -= rate - rate / comp.AttackSpeedModifier;
    }

    private void OnShotAttempt(ref ShotAttemptedEvent args)
    {
        if (!HasComp<BerserkingComponent>(args.User))
            return;

        args.Cancel();
        if (_net.IsServer)
            _popup.PopupEntity(Loc.GetString("berserker-no-guns"), args.User, args.User);
    }

    #endregion

    #region Drake hide empowerment

    private void OnEmpower(Entity<LavalandBerserkerGearComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || _net.IsClient)
            return;

        if (!TryComp<StackComponent>(args.Used, out var stack) || stack.StackTypeId != "AshDrakeHide")
            return;

        args.Handled = true;

        if (HasComp<LavalandBerserkerEmpoweredComponent>(ent))
        {
            _popup.PopupEntity(Loc.GetString("berserker-empowered-already"), args.User, args.User);
            return;
        }

        _stacks.SetCount(args.Used, stack.Count - 1);
        AddComp<LavalandBerserkerEmpoweredComponent>(ent);

        _popup.PopupEntity(Loc.GetString("berserker-empowered"), args.User, args.User);
    }

    /// <summary>
    /// Drakeskin empowerment: like TG's armor plate, it adds flat protection against
    /// brute damage on top of the piece's base armor values.
    /// </summary>
    private void OnEmpoweredDamage(Entity<LavalandBerserkerEmpoweredComponent> ent, ref InventoryRelayedEvent<DamageModifyEvent> args)
    {
        foreach (var type in BruteTypes)
        {
            if (args.Args.Damage.DamageDict.TryGetValue(type, out var value))
                args.Args.Damage.DamageDict[type] = value * EmpowerMultiplier;
        }
    }

    #endregion
}
