// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Lavaland.Megafauna;

/// <summary>
///     Projectile that destroys the rock it hits in one hit instead of stopping at it,
///     so a boss cannot be fought from behind a rock wall.
/// </summary>
[RegisterComponent]
public sealed partial class RockBreakingProjectileComponent : Component;
