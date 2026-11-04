// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Lavaland.Megafauna.Events;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// Handles the wendigo attacks that are not plain projectile patterns: teleporting,
/// the shockwave patterns and the scream, plus the stomp it does while moving.
/// </summary>
public sealed class WendigoBossSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private static readonly EntProtoId DizzyEffect = "StatusEffectWoozy";

    // TG screams first: 0.5 second windup plus the 1 second scream tail before the first ring.
    private const float ScreamDelay = 1.5f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WendigoBossComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<WendigoTeleportActionEvent>(OnTeleport);
        SubscribeLocalEvent<WendigoShockwaveActionEvent>(OnShockwave);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        UpdateStomp();
        UpdateShockwaves(now);
    }

    private void OnMapInit(Entity<WendigoBossComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Origin = Transform(ent).Coordinates;
        ent.Comp.LastStompPosition = _xform.GetWorldPosition(ent);
        ent.Comp.HasLastStompPosition = true;
    }

    private void OnTeleport(WendigoTeleportActionEvent args)
    {
        if (args.Handled
            || TerminatingOrDeleted(args.Performer)
            || !TryComp<WendigoBossComponent>(args.Performer, out var boss))
            return;

        args.Handled = true;
        var uid = args.Performer;

        EntityCoordinates destination;
        if (args.ToOrigin)
        {
            if (!boss.Origin.IsValid(EntityManager))
                return;

            destination = boss.Origin;
        }
        else
        {
            var target = args.Target.IsValid(EntityManager) ? args.Target : Transform(uid).Coordinates;
            destination = target;

            // TG teleports to a random spot on a ring around the target, avoiding walls.
            for (var i = 0; i < 12; i++)
            {
                var candidate = target.Offset(_random.NextAngle().ToVec() * args.Radius).SnapToGrid(EntityManager);
                if (!_turf.TryGetTileRef(candidate, out var tileRef)
                    || _turf.IsTileBlocked(tileRef.Value, CollisionGroup.MobMask))
                    continue;

                destination = candidate;
                break;
            }
        }

        // A running shockwave pattern roots the boss, teleporting has to let go first.
        if (TryComp<TransformComponent>(uid, out var xform) && xform.Anchored)
            _xform.Unanchor(uid);

        _xform.SetCoordinates(uid, destination);
        boss.LastStompPosition = _xform.GetWorldPosition(uid);
        boss.HasLastStompPosition = true;

        if (args.EnragedProjectile is not { } projectile || !IsEnraged(uid))
            return;

        var self = _xform.GetWorldPosition(uid);
        var targetPos = args.Target.IsValid(EntityManager)
            ? args.Target.ToMap(EntityManager, _xform).Position
            : self;
        var baseAngle = (targetPos - self).ToWorldAngle();

        for (var i = 0; i < args.EnragedCount; i++)
        {
            var t = args.EnragedCount == 1 ? 0.5f : i / (float) (args.EnragedCount - 1);
            var angle = baseAngle + Angle.FromDegrees((t - 0.5f) * args.EnragedSpread);
            FireProjectile(uid, projectile, angle, args.EnragedSpeed);
        }
    }

    private void OnShockwave(WendigoShockwaveActionEvent args)
    {
        if (args.Handled
            || TerminatingOrDeleted(args.Performer)
            || !TryComp<WendigoBossComponent>(args.Performer, out var boss)
            || HasComp<WendigoShockwaveComponent>(args.Performer))
            return;

        args.Handled = true;
        var uid = args.Performer;

        // TG teleports the boss back to the arena center before every shockwave attack.
        if (boss.Origin.IsValid(EntityManager))
            _xform.SetCoordinates(uid, boss.Origin);

        Scream(uid, boss);

        var self = _xform.GetWorldPosition(uid);
        var targetPos = args.Target.IsValid(EntityManager)
            ? args.Target.ToMap(EntityManager, _xform).Position
            : self;

        var wave = EnsureComp<WendigoShockwaveComponent>(uid);
        wave.Projectile = args.Projectile;
        wave.Pattern = args.Pattern;
        wave.Count = Math.Max(1, args.Count);
        wave.Repeats = Math.Max(1, args.Repeats);
        wave.Interval = MathF.Max(0.05f, args.Interval);
        wave.Speed = args.Speed;
        wave.Step = args.Step;
        wave.Growth = args.Growth;
        wave.BaseAngle = (targetPos - self).ToWorldAngle();
        wave.Direction = _random.Prob(0.5f) ? 1 : -1;
        wave.Repeat = 0;
        wave.Next = _timing.CurTime + TimeSpan.FromSeconds(ScreamDelay);
        wave.End = wave.Next + TimeSpan.FromSeconds(wave.Interval * wave.Repeats + 0.5f);

        // The pattern roots the boss in place, like TG's can_move = FALSE.
        _physics.SetLinearVelocity(uid, Vector2.Zero);
        if (!Transform(uid).Anchored)
            _xform.AnchorEntity(uid);
    }

    private void UpdateStomp()
    {
        var query = EntityQueryEnumerator<WendigoBossComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var boss, out var xform))
        {
            if (xform.Anchored)
                continue;

            var position = _xform.GetWorldPosition(xform);
            if (!boss.HasLastStompPosition)
            {
                boss.HasLastStompPosition = true;
                boss.LastStompPosition = position;
                continue;
            }

            if (Vector2.Distance(position, boss.LastStompPosition) < boss.StompDistance)
                continue;

            boss.LastStompPosition = position;

            var burst = Spawn(boss.StompBurst, xform.Coordinates);
            if (TryComp<WendigoGroundSlamComponent>(burst, out var slam))
                slam.User = uid;
        }
    }

    private void UpdateShockwaves(TimeSpan now)
    {
        var query = EntityQueryEnumerator<WendigoShockwaveComponent>();
        while (query.MoveNext(out var uid, out var wave))
        {
            if (wave.Repeat < wave.Repeats)
            {
                if (now < wave.Next)
                    continue;

                FireRepeat(uid, wave);
                wave.Repeat++;
                wave.Next += TimeSpan.FromSeconds(wave.Interval);
                continue;
            }

            if (now < wave.End)
                continue;

            FinishShockwave(uid);
        }
    }

    private void FinishShockwave(EntityUid uid)
    {
        if (!TerminatingOrDeleted(uid)
            && TryComp<TransformComponent>(uid, out var xform)
            && xform.Anchored)
            _xform.Unanchor(uid);

        RemComp<WendigoShockwaveComponent>(uid);
    }

    private void FireRepeat(EntityUid uid, WendigoShockwaveComponent wave)
    {
        switch (wave.Pattern)
        {
            case WendigoShockwavePattern.Alternating:
            {
                var step = 360f / wave.Count;
                var offset = wave.Repeat % 2 == 0 ? 0f : step * 0.5f;
                for (var i = 0; i < wave.Count; i++)
                    FireProjectile(uid, wave.Projectile, Angle.FromDegrees(i * step + offset), wave.Speed);
                break;
            }
            case WendigoShockwavePattern.Spiral:
            {
                var index = wave.Repeat + 1;
                var rotation = (wave.Step + wave.Growth * index) * wave.Direction;
                var step = 360f / wave.Count;
                for (var i = 1; i <= wave.Count; i++)
                {
                    var angle = wave.BaseAngle + Angle.FromDegrees(index * rotation + i * step);
                    FireProjectile(uid, wave.Projectile, angle, wave.Speed);
                }
                break;
            }
            case WendigoShockwavePattern.Wave:
            {
                var step = 360f / wave.Count;
                var rotation = wave.Repeat * wave.Step * wave.Direction;
                for (var i = 1; i <= wave.Count; i++)
                    FireProjectile(uid, wave.Projectile, Angle.FromDegrees(i * step - rotation), wave.Speed);
                break;
            }
        }
    }

    private void Scream(EntityUid uid, WendigoBossComponent boss)
    {
        _audio.PlayPvs(boss.ScreamSound, uid);

        foreach (var target in _lookup.GetEntitiesInRange(uid, boss.ScreamRange))
        {
            if (target == uid || !HasComp<MobStateComponent>(target))
                continue;

            _status.TryAddStatusEffectDuration(target, DizzyEffect, boss.ScreamDizzyTime);
        }
    }

    private void FireProjectile(EntityUid shooter, EntProtoId proto, Angle angle, float speed)
    {
        var projectile = Spawn(proto, Transform(shooter).Coordinates);
        _xform.SetLocalRotation(projectile, angle);

        if (TryComp<ProjectileComponent>(projectile, out var projectileComp))
        {
            projectileComp.Shooter = shooter;
            projectileComp.Weapon = shooter;
        }

        _physics.SetLinearVelocity(projectile, angle.ToWorldVec() * speed);
    }

    private bool IsEnraged(EntityUid uid)
        => TryComp<BossEnrageComponent>(uid, out var enrage) && enrage.Enraged;
}
