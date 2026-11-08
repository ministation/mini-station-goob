// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using Content.Server.Beam;
using Content.Server.NPC.HTN;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Weapons.CainAbel;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Content.Shared.Projectiles;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Weapons.CainAbel;

/// <summary>
/// Side effects of Cain and Abel: crystal traps, engraved daggers on throw impact, the swing
/// damage tick and projectile parry.
/// </summary>
public sealed class CainAbelEffectsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly BeamSystem _beam = default!;

    private static readonly EntProtoId SwingStatusEffect = "LavalandStatusEffectDaggerSwinging";
    private static readonly SoundSpecifier ParrySound = new SoundPathSpecifier("/Audio/_Lavaland/Weapons/CainAbel/parry.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CainAbelCrystalComponent, MapInitEvent>(OnCrystalMapInit);
        SubscribeLocalEvent<CainAbelThrowProjectileComponent, ProjectileHitEvent>(OnThrowProjectileHit);
        SubscribeLocalEvent<DaggerSwingingComponent, StatusEffectAppliedEvent>(OnSwingApplied);
        // Projectile hits are dispatched from SpawnOnDeathSystem, the fork allows only one handler
        // per component and event pair. See SpawnOnDeathSystem.OnProjectileHit.
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateCrystals();
        UpdateSwinging();
    }

    private void OnCrystalMapInit(Entity<CainAbelCrystalComponent> ent, ref MapInitEvent args)
    {
        // Mobs already standing on the tile are hit immediately, like TG's atom entered check.
        ent.Comp.NextCheck = _timing.CurTime;
    }

    private void UpdateCrystals()
    {
        var query = EntityQueryEnumerator<CainAbelCrystalComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.NextCheck)
                continue;

            comp.NextCheck = _timing.CurTime + TimeSpan.FromSeconds(comp.CheckInterval);

            var coords = _transform.GetMapCoordinates(uid);
            foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(coords, comp.CheckRadius))
            {
                if (!comp.Hit.Add(mob.Owner))
                    continue;

                DamageFromCrystal(uid, comp, mob.Owner);
            }
        }
    }

    private void DamageFromCrystal(EntityUid crystal, CainAbelCrystalComponent comp, EntityUid mob)
    {
        if (!_mobState.IsAlive(mob))
            return;

        var damage = new DamageSpecifier();
        damage.DamageDict["Slash"] = IsLargeMob(mob) ? comp.LargeDamage : comp.SmallDamage;
        _damageable.TryChangeDamage(mob, damage);

        _status.TryAddStatusEffectDuration(mob, comp.StunEffect, comp.StunDuration);
        _stun.TryAddStunDuration(mob, comp.StunDuration);

        _audio.PlayPvs(comp.HitSound, crystal);

        var visual = Spawn(comp.StunVisual, Transform(mob).Coordinates);
        _transform.SetParent(visual, mob);
    }

    private void OnThrowProjectileHit(Entity<CainAbelThrowProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (!Exists(args.Target))
            return;

        var comp = ent.Comp;
        var impact = Transform(args.Target).Coordinates;
        var engraved = Spawn(comp.Engraved, impact);

        var shooter = args.Shooter;
        if (shooter is { } user && !TerminatingOrDeleted(user))
        {
            _beam.TryCreateBeam(user, engraved, comp.ChainBeam);

            if (comp.Launch)
            {
                _throwing.TryThrow(user, impact, baseThrowSpeed: 15f, user: user, pushbackRatio: 0f, doSpin: false, playSound: false);
                Timer.Spawn(TimeSpan.FromSeconds(0.8), () =>
                {
                    if (!TerminatingOrDeleted(engraved))
                        QueueDel(engraved);
                });
                return;
            }
        }

        if (comp.Launch)
            return;

        var crystal = comp.Crystal;
        var sound = comp.CrystalSound;
        for (var ring = 0; ring < comp.Rings; ring++)
        {
            var index = ring;
            Timer.Spawn(TimeSpan.FromSeconds(index * comp.RingInterval), () => SpawnCrystalRing(crystal, impact, index, sound));
        }
    }

    private void SpawnCrystalRing(EntProtoId crystal, EntityCoordinates impact, int ring, SoundSpecifier sound)
    {
        for (var dx = -ring; dx <= ring; dx++)
        {
            var rest = ring - Math.Abs(dx);
            Spawn(crystal, impact.Offset(new Vector2(dx, rest)));
            if (rest > 0)
                Spawn(crystal, impact.Offset(new Vector2(dx, -rest)));
        }

        _audio.PlayPvs(sound, impact);
    }

    private void OnSwingApplied(Entity<DaggerSwingingComponent> ent, ref StatusEffectAppliedEvent args)
    {
        ent.Comp.NextTick = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.TickInterval);
    }

    private void UpdateSwinging()
    {
        var query = EntityQueryEnumerator<DaggerSwingingComponent, StatusEffectComponent>();
        while (query.MoveNext(out _, out var comp, out var status))
        {
            if (status.AppliedTo is not { } target || TerminatingOrDeleted(target))
                continue;

            if (_timing.CurTime < comp.NextTick)
                continue;

            comp.NextTick = _timing.CurTime + TimeSpan.FromSeconds(comp.TickInterval);

            var coords = _transform.GetMapCoordinates(target);
            var hitAny = false;
            foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(coords, comp.Radius))
            {
                if (mob.Owner == target || !_mobState.IsAlive(mob.Owner))
                    continue;

                var damage = new DamageSpecifier();
                damage.DamageDict["Slash"] = IsLargeMob(mob.Owner) ? comp.LargeDamage : comp.SmallDamage;
                _damageable.TryChangeDamage(mob.Owner, damage, origin: target);
                hitAny = true;
            }

            if (hitAny)
                _audio.PlayPvs(comp.HitSound, target);
        }
    }

    /// <summary>
    /// TG parry of the dagger swing: a projectile that hits the swinger is turned around and
    /// deals no damage. Called from SpawnOnDeathSystem, the single projectile hit handler.
    /// </summary>
    public bool TryParry(Entity<ProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (args.Target == args.Shooter)
            return false;

        if (!_status.TryGetStatusEffect(args.Target, SwingStatusEffect, out _))
            return false;

        args.Damage = new DamageSpecifier();
        ent.Comp.DeleteOnCollide = false;

        if (TryComp<PhysicsComponent>(ent, out var physics))
        {
            var velocity = _physics.GetMapLinearVelocity(ent, physics);
            _physics.SetLinearVelocity(ent, -velocity, body: physics);
        }

        _audio.PlayPvs(ParrySound, args.Target);
        return true;
    }

    private bool IsLargeMob(EntityUid uid)
    {
        if (!HasComp<MobStateComponent>(uid) || !_mobState.IsAlive(uid))
            return false;

        return HasComp<MegafaunaAiComponent>(uid) || HasComp<HTNComponent>(uid);
    }
}
