// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// Periodically heals living mobs of the same npc faction around the owner.
/// TG mooks heal their kin with a healing touch; the fork has no per-click heal
/// action, so the healer mook is a passive aura instead.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandFactionHealerComponent : Component
{
    /// <summary>
    /// Damage applied to every friendly mob in range each tick. Negative values heal.
    /// </summary>
    [DataField]
    public DamageSpecifier Heal = new();

    /// <summary>
    /// Radius in tiles.
    /// </summary>
    [DataField]
    public float Range = 5f;

    /// <summary>
    /// Seconds between heals.
    /// </summary>
    [DataField]
    public float Interval = 1f;

    [ViewVariables]
    public float Accumulator;
}
