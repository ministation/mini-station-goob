// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// Explodes a projectile when it hits a wall or an object, using the projectile's
/// <see cref="Content.Shared.Explosion.Components.ExplosiveComponent"/>.
/// Port of TG's frost orb and ice blast, which call EX_ACT(EXPLODE_HEAVY) on turf or object impacts.
/// A hit mob still only takes the plain projectile damage.
/// </summary>
[RegisterComponent]
public sealed partial class ExplodeOnHitComponent : Component;
