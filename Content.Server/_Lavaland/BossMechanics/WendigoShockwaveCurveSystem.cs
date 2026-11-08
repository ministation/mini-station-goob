// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.Projectiles;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// TG parity for <see cref="WendigoShockwaveCurveComponent"/>: the projectile direction
/// rotates a little every tick while it flies.
/// </summary>
public sealed class WendigoShockwaveCurveSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<WendigoShockwaveCurveComponent, ProjectileComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var curve, out var projectile, out var body))
        {
            var turn = Angle.FromDegrees(curve.TurnDegrees);
            projectile.Angle += turn;
            Dirty(uid, projectile);

            // ProjectileComponent.Angle is cosmetic on its own: the flight direction lives in the
            // linear velocity, so turn that too or the shockwave keeps flying dead straight.
            var velocity = body.LinearVelocity;
            if (velocity.LengthSquared() <= 0.01f)
                continue;

            var radians = (float) turn.Theta;
            var cos = MathF.Cos(radians);
            var sin = MathF.Sin(radians);
            var turned = new Vector2(
                velocity.X * cos - velocity.Y * sin,
                velocity.X * sin + velocity.Y * cos);

            _physics.SetLinearVelocity(uid, turned, body);
        }
    }
}
