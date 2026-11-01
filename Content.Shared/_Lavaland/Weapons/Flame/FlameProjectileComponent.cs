// SPDX-License-Identifier: AGPL-3.0-or-later

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
