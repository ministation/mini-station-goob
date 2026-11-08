// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

namespace Content.Shared._Lavaland.Megafauna.Components;

/// <summary>
/// Regenerating armor pool of a megafauna. It absorbs incoming damage before health,
/// one to one, and breaks for good once depleted.
/// </summary>
[RegisterComponent]
public sealed partial class MegafaunaArmorComponent : Component
{
    /// <summary>
    /// Current armor, filled up to <see cref="MaxArmor"/> on map init.
    /// </summary>
    [DataField]
    public float Armor;

    [DataField]
    public float MaxArmor = 600f;

    /// <summary>
    /// Seconds without taking damage before the pool starts regenerating.
    /// </summary>
    [DataField]
    public float RegenDelay = 8f;

    /// <summary>
    /// Armor restored per second once regeneration starts.
    /// </summary>
    [DataField]
    public float RegenRate = 40f;

    /// <summary>
    /// Set when the pool is depleted, a broken pool never regenerates.
    /// </summary>
    [DataField]
    public bool Broken;

    /// <summary>
    /// Earliest time regeneration may tick, pushed forward by every hit.
    /// </summary>
    [ViewVariables]
    public TimeSpan RegenAt;

    /// <summary>
    /// Earliest time the boss HUD may be refreshed again. Regeneration is continuous,
    /// the networked HUD is only synced a few times per second.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextHudUpdate;
}
