// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using Content.Server.Power.EntitySystems;
using Content.Shared._Goobstation.Weapons.Ranged;
using Content.Shared._Lavaland.ItemUpgrades;
using Content.Shared._Lavaland.ItemUpgrades.Components;
using Content.Shared._Lavaland.Weapons.Marker;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Power.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Marker;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Crusher;

/// <summary>
/// Applies the effects of the crusher trophies taken from regular fauna.
/// </summary>
public sealed class FaunaCrusherTrophySystem : EntitySystem
{
    private static readonly Vector2[] Cardinals =
    {
        Vector2.UnitX,
        -Vector2.UnitX,
        Vector2.UnitY,
        -Vector2.UnitY,
    };

    private static readonly float[] CardinalDegrees = { 0f, 180f, 90f, 270f };

    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ItemUpgradesSystem _upgrades = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();

        // The vanilla crusher trophy system already handles MobStateComponent for this event.
        SubscribeLocalEvent<DamageableComponent, ApplyMarkerBonusEvent>(OnMarkerDetonated);
        SubscribeLocalEvent<FaunaCrusherTrophyComponent, RechargeBasicEntityAmmoGetCooldownModifiersEvent>(OnRechargeModifiers);
        SubscribeLocalEvent<MobStateComponent, AttackedEvent>(OnMarkedTargetAttacked);
        SubscribeLocalEvent<FaunaTrophyRebukedComponent, AttackAttemptEvent>(OnRebukedAttack);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FaunaTrophyRebukedComponent>();
        while (query.MoveNext(out var uid, out var rebuked))
        {
            if (_timing.CurTime >= rebuked.EndTime)
                RemCompDeferred(uid, rebuked);
        }
    }

    private void OnRechargeModifiers(Entity<FaunaCrusherTrophyComponent> ent, ref RechargeBasicEntityAmmoGetCooldownModifiersEvent args)
    {
        args.Multiplier *= ent.Comp.RechargeCooldownModifier;
    }

    private void OnRebukedAttack(Entity<FaunaTrophyRebukedComponent> ent, ref AttackAttemptEvent args)
    {
        args.Cancel();
        RemCompDeferred<FaunaTrophyRebukedComponent>(ent);
        _popup.PopupEntity(Loc.GetString("fauna-trophy-rebuked"), ent.Owner, args.Uid);
    }

    private void OnMarkerDetonated(Entity<DamageableComponent> target, ref ApplyMarkerBonusEvent args)
    {
        var user = args.User;
        if (TerminatingOrDeleted(user) || TerminatingOrDeleted(target.Owner))
            return;

        if (!TryComp<ItemUpgradeableComponent>(args.Weapon, out var upgradeable))
            return;

        foreach (var upgrade in _upgrades.GetCurrentUpgrades((args.Weapon, upgradeable)))
        {
            if (!TryComp<FaunaCrusherTrophyComponent>(upgrade.Owner, out var trophy))
                continue;

            ApplyDetonation(trophy, user, target.Owner);
        }
    }

    private void ApplyDetonation(FaunaCrusherTrophyComponent trophy, EntityUid user, EntityUid target)
    {
        if (trophy.DetonationMissingHealthDamage > 0f
            && TryComp<DamageableComponent>(user, out var userDamage)
            && userDamage.TotalDamage > 0)
        {
            var bonus = userDamage.TotalDamage * trophy.DetonationMissingHealthDamage;
            var damage = new DamageSpecifier(_proto.Index<DamageTypePrototype>("Blunt"), bonus);
            _damageable.TryChangeDamage(target, damage, origin: user);
        }

        if (trophy.BackstepTiles > 0)
            Backstep(user, target, trophy.BackstepTiles);

        if (trophy.DetonationPopups.Length > 0)
            _popup.PopupEntity(Loc.GetString(_random.Pick(trophy.DetonationPopups)), target);

        if (trophy.DetonationSound != null)
            _audio.PlayPvs(trophy.DetonationSound, target);

        if (trophy.RebukeDuration > TimeSpan.Zero)
        {
            var rebuked = EnsureComp<FaunaTrophyRebukedComponent>(target);
            rebuked.EndTime = _timing.CurTime + trophy.RebukeDuration;
        }

        if (trophy.RangedChargeDrain > 0f
            && TryComp<BatteryAmmoProviderComponent>(target, out var ammoProvider)
            && TryComp<BatteryComponent>(target, out var battery))
        {
            _battery.TryUseCharge(target, Math.Min(ammoProvider.FireCost, trophy.RangedChargeDrain));
        }

        if (trophy.DetonationProjectile is { } projectile)
            FireCardinals(user, target, projectile, trophy.ProjectileSpeed);
    }

    private void Backstep(EntityUid user, EntityUid target, int tiles)
    {
        var userCoords = Transform(user).Coordinates;
        var diff = _xform.GetWorldPosition(user) - _xform.GetWorldPosition(target);
        if (diff.LengthSquared() < 0.01f)
            return;

        var dir = Vector2.Normalize(diff);
        var coords = userCoords;

        for (var i = 0; i < tiles; i++)
        {
            var next = coords.Offset(dir);
            if (!_turf.TryGetTileRef(next, out var tile)
                || _turf.IsSpace(tile.Value)
                || _turf.IsTileBlocked(tile.Value, CollisionGroup.Impassable))
            {
                return;
            }

            coords = next;
        }

        _xform.SetCoordinates(user, coords);
    }

    private void FireCardinals(EntityUid user, EntityUid target, EntProtoId projectile, float speed)
    {
        var coords = Transform(user).Coordinates;

        for (var i = 0; i < Cardinals.Length; i++)
        {
            var shot = Spawn(projectile, coords);
            _xform.SetLocalRotation(shot, Angle.FromDegrees(CardinalDegrees[i]));

            if (TryComp<ProjectileComponent>(shot, out var shotComp))
            {
                shotComp.Shooter = user;
                shotComp.Weapon = target;
            }

            _physics.SetLinearVelocity(shot, Cardinals[i] * speed);
        }
    }

    private void OnMarkedTargetAttacked(Entity<MobStateComponent> victim, ref AttackedEvent args)
    {
        var attacker = args.User;
        if (TerminatingOrDeleted(attacker) || !TryComp<DamageMarkerComponent>(attacker, out var marker))
            return;

        if (!TryComp<ItemUpgradeableComponent>(marker.Marker, out var upgradeable))
            return;

        foreach (var upgrade in _upgrades.GetCurrentUpgrades((marker.Marker, upgradeable)))
        {
            if (!TryComp<FaunaCrusherTrophyComponent>(upgrade.Owner, out var trophy)
                || !trophy.DetonateOnTargetAttack)
            {
                continue;
            }

            _damageable.TryChangeDamage(attacker, marker.Damage, origin: marker.Marker);
            _audio.PlayPvs(marker.Sound, attacker);
            RemCompDeferred<DamageMarkerComponent>(attacker);
            return;
        }
    }
}