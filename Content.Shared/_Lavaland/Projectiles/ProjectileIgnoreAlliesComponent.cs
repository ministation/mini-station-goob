// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Lavaland.Projectiles;

/// <summary>
/// TG parity for the raptor feather crusher trophy: this projectile passes through
/// entities friendly to its shooter instead of hitting them.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ProjectileIgnoreAlliesComponent : Component;
