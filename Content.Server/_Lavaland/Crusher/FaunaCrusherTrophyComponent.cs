// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Lavaland.Crusher;

/// <summary>
/// Effects of the crusher trophies taken from regular fauna. All of them fire when a damage marker is
/// detonated on a mob, except <see cref="RechargeCooldownModifier"/> which changes the crusher itself.
/// </summary>
[RegisterComponent]
public sealed partial class FaunaCrusherTrophyComponent : Component
{
    /// <summary>
    /// Extra blunt damage on detonation, multiplied by the damage the user has already taken.
    /// Used by the goliath tentacle.
    /// </summary>
    [DataField]
    public float DetonationMissingHealthDamage;

    /// <summary>
    /// How many tiles the user steps away from the marked target on detonation.
    /// Used by the icewing watcher wing.
    /// </summary>
    [DataField]
    public int BackstepTiles;

    /// <summary>
    /// Loc strings shown above the marked target on detonation, one is picked at random.
    /// Used by the brimdemon fang.
    /// </summary>
    [DataField]
    public string[] DetonationPopups = [];

    /// <summary>
    /// Sound played at the marked target on detonation. Used by the brimdemon fang.
    /// </summary>
    [DataField]
    public SoundSpecifier? DetonationSound;

    /// <summary>
    /// How long the marked target cannot attack after a detonation.
    /// Used by the lobster claw.
    /// </summary>
    [DataField]
    public TimeSpan RebukeDuration;

    /// <summary>
    /// TG parity for the raptor feather: shots fired from the crusher pass through
    /// the shooter's allies instead of hitting them.
    /// </summary>
    [DataField]
    public bool PassThroughAllies;

    /// <summary>
    /// Detonate the marker when the marked target attacks someone instead of when it is hit.
    /// Used by the magmawing watcher wing.
    /// </summary>
    [DataField]
    public bool DetonateOnTargetAttack;

    /// <summary>
    /// Projectile fired in each cardinal direction on detonation.
    /// Used by the bileworm spewlet.
    /// </summary>
    [DataField]
    public EntProtoId? DetonationProjectile;

    [DataField]
    public float ProjectileSpeed = 20f;

    /// <summary>
    /// Battery charge removed from the marked target on detonation, delaying its ranged attack.
    /// Used by the watcher wing.
    /// </summary>
    [DataField]
    public float RangedChargeDrain;

    /// <summary>
    /// Multiplier applied to the crusher recharge cooldown while this trophy is inserted.
    /// Used by the legion skull.
    /// </summary>
    [DataField]
    public float RechargeCooldownModifier = 1f;
}

/// <summary>
/// Temporary state of a mob rebuked by the lobster claw trophy: its next attack is interrupted.
/// </summary>
[RegisterComponent]
public sealed partial class FaunaTrophyRebukedComponent : Component
{
    [ViewVariables]
    public TimeSpan EndTime;
}
