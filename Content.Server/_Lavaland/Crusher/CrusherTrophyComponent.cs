// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.Prototypes;

namespace Content.Server._Lavaland.Crusher;

/// <summary>
/// Marks an item as a crusher trophy. The crusher trophy slot accepts items with this
/// component and the effect fields below enable the TG trophy behaviours.
/// </summary>
[RegisterComponent]
public sealed partial class CrusherTrophyComponent : Component
{
    /// <summary>
    /// Heals the user for this amount on every successful melee hit.
    /// </summary>
    [DataField]
    public float MeleeHeal;

    /// <summary>
    /// Heals the user for this amount when a damage marker is detonated.
    /// </summary>
    [DataField]
    public float DetonationHeal;

    /// <summary>
    /// Deals this much heat damage to mobs around the user when a damage marker is detonated.
    /// </summary>
    [DataField]
    public float DetonationHeatDamage;

    /// <summary>
    /// Radius in meters of the detonation heat damage and pushback.
    /// </summary>
    [DataField]
    public float DetonationPushRadius = 2f;

    /// <summary>
    /// Grants heavy damage reduction to the user for this long on detonation. Zero disables the effect.
    /// </summary>
    [DataField]
    public TimeSpan DetonationDamageReductionDuration;

    /// <summary>
    /// Tile chaser spawned at the marked target on detonation.
    /// </summary>
    [DataField]
    public EntProtoId? DetonationChaser;

    /// <summary>
    /// Prototype spawned on detonation, chance based.
    /// </summary>
    [DataField]
    public EntProtoId? DetonationSpawn;

    [DataField]
    public float DetonationSpawnChance = 1f;

    /// <summary>
    /// Spawn a plus shaped patch of <see cref="DetonationSpawn"/> instead of a single entity.
    /// </summary>
    [DataField]
    public bool DetonationSpawnPatch;

    /// <summary>
    /// Spawn the detonation spawn at the marked target instead of at the user.
    /// </summary>
    [DataField]
    public bool DetonationSpawnAtTarget;

    /// <summary>
    /// Prototype spawned when the trophy is used in hand.
    /// </summary>
    [DataField]
    public EntProtoId? UseSpawn;

    [DataField]
    public TimeSpan UseSpawnCooldown;

    /// <summary>
    /// Temporary lava immunity granted when the trophy is used in hand.
    /// </summary>
    [DataField]
    public TimeSpan LavaImmunityDuration;

    [DataField]
    public TimeSpan LavaImmunityCooldown;

    /// <summary>
    /// Next time the in hand effect can be used.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextUse;
}

/// <summary>
/// Temporary damage reduction granted by the miner eye trophy on marker detonation.
/// </summary>
[RegisterComponent]
public sealed partial class CrusherTrophyDamageReductionComponent : Component
{
    [DataField]
    public float Multiplier = 0.1f;

    [ViewVariables]
    public TimeSpan EndTime;
}
