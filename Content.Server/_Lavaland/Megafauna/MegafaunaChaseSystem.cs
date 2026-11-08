// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using Content.Shared._Lavaland.Aggression;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Lavaland.Megafauna;

/// <summary>
/// Walks active megafauna at whoever is fighting it and bites when close.
/// The data driven megafauna AI only drives special attacks, so bosses without NPC pathing
/// use to stand still between them, which made fights feel static.
/// </summary>
public sealed class MegafaunaChaseSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    private const float ChaseRange = 30f;
    private const float MeleeRange = 2.2f;
    private const float CircleRange = 3.5f;

    private EntityQuery<AggressorComponent> _aggressorQuery;
    private EntityQuery<MobStateComponent> _mobStateQuery;

    public override void Initialize()
    {
        base.Initialize();

        _aggressorQuery = GetEntityQuery<AggressorComponent>();
        _mobStateQuery = GetEntityQuery<MobStateComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MegafaunaAiComponent, AggressiveComponent, MovementSpeedModifierComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var ai, out var aggressive, out var speed, out var xform))
        {
            if (!ai.Active || xform.Anchored || !TryComp<PhysicsComponent>(uid, out var body))
                continue;

            if (!TryPickTarget(uid, aggressive, xform, out var target, out var targetXform))
            {
                if (body.LinearVelocity != Vector2.Zero)
                    _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);
                continue;
            }

            var selfPos = _xform.GetWorldPosition(xform);
            var targetPos = _xform.GetWorldPosition(targetXform);
            var diff = targetPos - selfPos;
            var distance = diff.Length();
            var dir = distance > 0.01f ? diff / distance : Vector2.Zero;

            if (distance > MeleeRange)
            {
                // Circle the target a little while closing in, so a fight is not a straight line.
                var speedNow = speed.BaseWalkSpeed;
                var velocity = dir * speedNow;
                if (distance < CircleRange)
                    velocity = Vector2.Lerp(velocity, new Vector2(-dir.Y, dir.X) * speedNow, 0.6f);

                _physics.SetLinearVelocity(uid, velocity, body: body);
                continue;
            }

            _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);

            if (TryComp<MeleeWeaponComponent>(uid, out var weapon))
                _melee.AttemptLightAttack(uid, uid, weapon, target);
        }
    }

    private bool TryPickTarget(EntityUid uid, AggressiveComponent aggressive, TransformComponent selfXform,
        out EntityUid target, out TransformComponent targetXform)
    {
        target = default;
        targetXform = default!;

        var selfPos = _xform.GetWorldPosition(selfXform);
        var bestDistance = ChaseRange;

        foreach (var aggressor in aggressive.Aggressors)
        {
            if (_entMan.Deleted(aggressor) || !TryComp(aggressor, out TransformComponent? xform))
                continue;

            if (xform.MapID != selfXform.MapID)
                continue;

            if (_mobStateQuery.TryComp(aggressor, out var mobState) && _mobState.IsDead(aggressor, mobState))
                continue;

            var distance = (_xform.GetWorldPosition(xform) - selfPos).Length();
            if (distance > bestDistance)
                continue;

            bestDistance = distance;
            target = aggressor;
            targetXform = xform;
        }

        return target != default;
    }
}
