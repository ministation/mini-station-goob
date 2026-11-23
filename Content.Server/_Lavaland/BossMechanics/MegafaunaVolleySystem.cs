// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Events;
using Content.Shared._Lavaland.Megafauna.Systems;
using Content.Shared._Goobstation.Wizard.Projectiles;
using Content.Shared.Chat;
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// Handles <see cref="MegafaunaVolleyActionEvent"/>: fires a series of projectile volleys,
/// either with random spread or with an explicit alternating angle pattern.
/// Re-aims every volley at the current target, so a moving player can still be tracked.
/// </summary>
public sealed class MegafaunaVolleySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly MegafaunaBlinkSystem _blink = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MegafaunaVolleyActionEvent>(OnVolleyAction);
        SubscribeLocalEvent<MegafaunaVolleyComponent, ComponentShutdown>(OnVolleyShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<MegafaunaVolleyComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (TerminatingOrDeleted(uid))
            {
                RemCompDeferred(uid, comp);
                continue;
            }

            if (comp.Steps.Count > 0)
            {
                UpdateSteps(uid, comp, now);
                continue;
            }

            if (now < comp.NextVolley)
                continue;

            if (comp.VolleyIndex >= comp.Volleys)
            {
                RemCompDeferred(uid, comp);
                continue;
            }

            // Keep tracking the target while the burst is firing.
            if (comp.Target is { } target && !TerminatingOrDeleted(target))
                comp.BaseAngle = (Transform(target).WorldPosition - _xform.GetWorldPosition(uid)).ToWorldAngle();

            FireVolley(uid, comp);
            comp.VolleyIndex++;
            comp.NextVolley = now + comp.Interval;
        }
    }

    private void UpdateSteps(EntityUid uid, MegafaunaVolleyComponent comp, TimeSpan now)
    {
        if (comp.StepIndex >= comp.Steps.Count)
        {
            RemCompDeferred(uid, comp);
            return;
        }

        if (now < comp.NextVolley)
            return;

        // Keep tracking the target while the barrage is firing.
        if (comp.Target is { } target && !TerminatingOrDeleted(target))
            comp.BaseAngle = (Transform(target).WorldPosition - _xform.GetWorldPosition(uid)).ToWorldAngle();

        var step = comp.Steps[comp.StepIndex];
        FirePattern(uid, comp, step.Pattern);
        FireRandomBurst(uid, comp, step.RandomCount, step.RandomSpread);
        FireScatter(uid, comp, step.ScatterCount, step.ScatterRadius);

        comp.StepIndex++;

        if (comp.StepIndex < comp.Steps.Count)
        {
            comp.NextVolley = now + TimeSpan.FromSeconds(Math.Max(0.01f, comp.Steps[comp.StepIndex].Interval));
            return;
        }

        RemCompDeferred(uid, comp);
    }

    private void OnVolleyShutdown(Entity<MegafaunaVolleyComponent> ent, ref ComponentShutdown args)
    {
        if (!ent.Comp.PauseAi || !ent.Comp.AiWasActive || TerminatingOrDeleted(ent.Owner))
            return;

        if (!TryComp<MegafaunaAiComponent>(ent.Owner, out var ai) || ai.Active || ai.Schedule.Count == 0)
            return;

        ai.Active = true;
    }

    private void OnVolleyAction(MegafaunaVolleyActionEvent args)
    {
        // A world target that failed validation is still performed with the event's unset
        // (invalid) coordinates, so guard them like the other boss systems do.
        if (args.Handled || TerminatingOrDeleted(args.Performer) || !args.Target.IsValid(EntityManager))
            return;

        // One volley at a time: a boss in the middle of a long barrage (the colossus finale) must not
        // have its remaining waves cut off because another volley attack fired over it.
        if (TryComp<MegafaunaVolleyComponent>(args.Performer, out var active)
            && (active.StepIndex < active.Steps.Count || active.VolleyIndex < active.Volleys))
        {
            args.Handled = true;
            return;
        }

        args.Handled = true;

        var performer = args.Performer;
        var targetMap = args.Target.ToMap(EntityManager, _xform);
        var start = _timing.CurTime;

        var comp = EnsureComp<MegafaunaVolleyComponent>(performer);
        comp.Projectile = args.Projectile;
        comp.Speed = args.Speed;
        comp.RandomSpread = args.RandomSpread;
        comp.Homing = args.Homing;
        comp.HomingSpeed = args.HomingSpeed;
        comp.Patterns = args.Patterns;
        comp.CountPerAngle = Math.Max(1, args.CountPerAngle);
        comp.VolleyIndex = 0;
        comp.Volleys = Math.Max(1, args.Volleys);
        comp.Interval = TimeSpan.FromSeconds(Math.Max(0.01f, args.Interval));
        comp.BaseAngle = (targetMap.Position - _xform.GetWorldPosition(performer)).ToWorldAngle();
        comp.Target = TryComp<MegafaunaAiTargetingComponent>(performer, out var targeting)
            ? targeting.TargetEnt
            : null;
        comp.Steps = args.Steps;
        comp.StepIndex = 0;
        comp.PauseAi = args.PauseAi;
        comp.AiWasActive = false;

        if (args.PauseAi && TryComp<MegafaunaAiComponent>(performer, out var ai))
        {
            comp.AiWasActive = ai.Active;
            ai.Active = false;
        }

        // Scripted boss lines must not be silenced by the missing Speech component on megafauna prototypes.
        if (!string.IsNullOrEmpty(args.Announce))
            _chat.TrySendInGameICMessage(performer, args.Announce, InGameICChatType.Speak, false, ignoreActionBlocker: true);

        if (args.BlinkDelay > 0f)
        {
            var delay = TimeSpan.FromSeconds(args.BlinkDelay);
            SoundSpecifier? sound = TryComp<MegafaunaBlinkComponent>(performer, out var blink)
                ? blink.Sound
                : null;
            _blink.Blink(performer, args.Target, delay, sound);
            start += delay;
        }

        comp.NextVolley = start;

        // A scripted barrage waits for its first step's interval, the way TG sleeps before the first wave.
        if (comp.Steps.Count > 0)
            comp.NextVolley += TimeSpan.FromSeconds(Math.Max(0.01f, comp.Steps[0].Interval));
    }

    private void FireVolley(EntityUid performer, MegafaunaVolleyComponent comp)
    {
        if (comp.Patterns.Count == 0)
        {
            FireRandomVolley(performer, comp);
            return;
        }

        var pattern = comp.Patterns[comp.VolleyIndex % comp.Patterns.Count];

        // An empty pattern is a wave of random bolts, the same as having no patterns at all.
        if (pattern.Count == 0)
        {
            FireRandomVolley(performer, comp);
            return;
        }

        FirePattern(performer, comp, pattern);
    }

    private void FirePattern(EntityUid performer, MegafaunaVolleyComponent comp, List<float> pattern)
    {
        foreach (var offset in pattern)
            FireProjectile(performer, comp, comp.BaseAngle + Angle.FromDegrees(offset));
    }

    private void FireRandomVolley(EntityUid performer, MegafaunaVolleyComponent comp)
        => FireRandomBurst(performer, comp, comp.CountPerAngle, comp.RandomSpread);

    private void FireRandomBurst(EntityUid performer, MegafaunaVolleyComponent comp, int count, float spread)
    {
        for (var i = 0; i < count; i++)
        {
            var angle = comp.BaseAngle + Angle.FromDegrees(_random.NextFloat(-spread, spread));
            FireProjectile(performer, comp, angle);
        }
    }

    private void FireScatter(EntityUid performer, MegafaunaVolleyComponent comp, int count, int radius)
    {
        for (var i = 0; i < count; i++)
        {
            // A random tile in the arena, never the tile the boss stands on (TG skips get_turf(firer)).
            var offset = Vector2.Zero;
            for (var attempt = 0; attempt < 8 && offset == Vector2.Zero; attempt++)
                offset = new Vector2(_random.Next(-radius, radius + 1), _random.Next(-radius, radius + 1));

            if (offset == Vector2.Zero)
                continue;

            FireProjectile(performer, comp, offset.ToWorldAngle());
        }
    }

    private void FireProjectile(EntityUid shooter, MegafaunaVolleyComponent comp, Angle angle)
    {
        var projectile = Spawn(comp.Projectile, Transform(shooter).Coordinates);
        _xform.SetLocalRotation(projectile, angle);

        if (TryComp<ProjectileComponent>(projectile, out var projectileComp))
        {
            projectileComp.Shooter = shooter;
            projectileComp.Weapon = shooter;
        }

        _physics.SetLinearVelocity(projectile, angle.ToWorldVec() * comp.Speed);

        if (!comp.Homing)
            return;

        var homing = EnsureComp<HomingProjectileComponent>(projectile);
        homing.Target = comp.Target;
        homing.HomingSpeed = comp.HomingSpeed;
    }
}
