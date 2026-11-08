// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Projectiles;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// TG parity for <see cref="WendigoShockwaveCurveComponent"/>: the projectile direction
/// rotates a little every tick while it flies.
/// </summary>
public sealed class WendigoShockwaveCurveSystem : EntitySystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<WendigoShockwaveCurveComponent, ProjectileComponent>();
        while (query.MoveNext(out var uid, out var curve, out var projectile))
        {
            projectile.Angle += Angle.FromDegrees(curve.TurnDegrees);
            Dirty(uid, projectile);
        }
    }
}
