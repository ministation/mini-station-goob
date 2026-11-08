// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Weapons.Flame;

/// <summary>
/// Projectile that sets the target on fire when it hits.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FlameProjectileComponent : Component
{
    /// <summary>
    /// How many fire stacks to add on hit.
    /// </summary>
    [DataField]
    public float FireStacks = 3f;
}
