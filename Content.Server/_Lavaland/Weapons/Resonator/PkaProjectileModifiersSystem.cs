// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Server.Gatherable;
using Content.Server.Gatherable.Components;
using Content.Shared._Lavaland.Weapons.Resonator;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Weapons.Resonator;

/// <summary>
/// Handles the on-hit effects of the kinetic accelerator projectile modkits.
/// </summary>
public sealed class PkaProjectileModifiersSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly GatherableSystem _gatherable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ResonatorSystem _resonator = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PkaProjectileExplosionComponent, ProjectileHitEvent>(OnExplosionHit);
        SubscribeLocalEvent<PkaProjectileRepeaterComponent, ProjectileHitEvent>(OnRepeaterHit);
        SubscribeLocalEvent<PkaProjectileResonatorBlastComponent, ProjectileHitEvent>(OnResonatorBlastHit);
        SubscribeLocalEvent<PkaProjectileSyphonComponent, ProjectileHitEvent>(OnSyphonHit);
        SubscribeLocalEvent<PkaSyphonMarkComponent, MobStateChangedEvent>(OnSyphonMarkStateChanged);
        SubscribeLocalEvent<PkaDeathSyphonComponent, GunShotEvent>(OnSyphonGunShot);
    }

    private void OnExplosionHit(Entity<PkaProjectileExplosionComponent> ent, ref ProjectileHitEvent args)
    {
        var comp = ent.Comp;
        var target = args.Target;
        var shooter = args.Shooter;
        var coords = Transform(ent).Coordinates;

        if (comp.Turfs)
        {
            foreach (var uid in _lookup.GetEntitiesInRange(coords, comp.Radius))
            {
                if (uid == target || TerminatingOrDeleted(uid) || !HasComp<GatherableComponent>(uid))
                    continue;

                _gatherable.Gather(uid, shooter);
            }
        }

        if (comp.MobDamageModifier <= 0f)
            return;

        var damage = args.Damage * comp.MobDamageModifier;
        foreach (var (mob, _) in _lookup.GetEntitiesInRange<MobStateComponent>(coords, comp.Radius))
        {
            if (mob == target || mob == shooter || TerminatingOrDeleted(mob))
                continue;

            _damage.TryChangeDamage(mob, damage, origin: shooter);
        }
    }

    private void OnRepeaterHit(Entity<PkaProjectileRepeaterComponent> ent, ref ProjectileHitEvent args)
    {
        var validRepeat = HasComp<GatherableComponent>(args.Target)
            || (TryComp<MobStateComponent>(args.Target, out var mobState) && mobState.CurrentState != MobState.Dead);

        if (!validRepeat
            || !TryComp<ProjectileComponent>(ent, out var projectile)
            || projectile.Weapon is not { } gun
            || !TryComp<RechargeBasicEntityAmmoComponent>(gun, out var recharge))
            return;

        recharge.NextCharge = _timing.CurTime
            + TimeSpan.FromSeconds(recharge.RechargeCooldown * ent.Comp.CooldownFactor);
        Dirty(gun, recharge);
    }

    private void OnResonatorBlastHit(Entity<PkaProjectileResonatorBlastComponent> ent, ref ProjectileHitEvent args)
    {
        // Rock is already drilled by the bolt itself, a field there would be wasted.
        if (HasComp<GatherableComponent>(args.Target))
            return;

        _resonator.CreateField(Transform(ent).Coordinates, args.Shooter, null, ResonatorMode.Auto,
            ent.Comp.Multiplier, 100);
    }

    private void OnSyphonHit(Entity<PkaProjectileSyphonComponent> ent, ref ProjectileHitEvent args)
    {
        if (!HasComp<MobStateComponent>(args.Target)
            || !TryComp<ProjectileComponent>(ent, out var projectile)
            || projectile.Weapon is not { } gun)
            return;

        var mark = EnsureComp<PkaSyphonMarkComponent>(args.Target);
        mark.Gun = gun;
        mark.Shooter = args.Shooter;
    }

    private void OnSyphonMarkStateChanged(Entity<PkaSyphonMarkComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (ent.Comp.Gun is { } gun && TryComp<PkaDeathSyphonComponent>(gun, out var syphon))
            syphon.Kills = MathF.Min(syphon.Kills + 1f, syphon.MaxKills);

        RemCompDeferred<PkaSyphonMarkComponent>(ent);
    }

    private void OnSyphonGunShot(Entity<PkaDeathSyphonComponent> ent, ref GunShotEvent args)
    {
        if (ent.Comp.Kills <= 0f)
            return;

        var multiplier = 1f + ent.Comp.Kills * ent.Comp.BonusPerKill;
        foreach (var (ammo, _) in args.Ammo)
        {
            if (ammo is { } ammoUid && TryComp<ProjectileComponent>(ammoUid, out var projectile))
            {
                projectile.Damage *= multiplier;
                Dirty(ammoUid, projectile);
            }
        }
    }
}
