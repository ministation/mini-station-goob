// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

namespace Content.Shared._Lavaland.Weapons.Resonator;

// One component per projectile modkit so that several modkits can stack on the same bolt.

[RegisterComponent]
public sealed partial class PkaProjectileExplosionComponent : Component
{
    /// <summary>
    /// Drills rock in a radius around the impact.
    /// </summary>
    [DataField]
    public bool Turfs;

    /// <summary>
    /// Multiplier for damage dealt to mobs in a radius around the impact, 0 disables it.
    /// </summary>
    [DataField]
    public float MobDamageModifier;

    [DataField]
    public float Radius = 1.5f;
}

[RegisterComponent]
public sealed partial class PkaProjectileRepeaterComponent : Component
{
    /// <summary>
    /// Fraction of the accelerator cooldown restored when the bolt hits a living or rock target.
    /// </summary>
    [DataField]
    public float CooldownFactor = 0.25f;
}

[RegisterComponent]
public sealed partial class PkaProjectileResonatorBlastComponent : Component
{
    /// <summary>
    /// Damage multiplier of the resonance field left at the impact.
    /// </summary>
    [DataField]
    public float Multiplier = 0.25f;
}

[RegisterComponent]
public sealed partial class PkaProjectilePassthroughComponent : Component;

[RegisterComponent]
public sealed partial class PkaProjectileSyphonComponent : Component;

/// <summary>
/// Stored on the accelerator while a death syphon modkit is installed.
/// </summary>
[RegisterComponent]
public sealed partial class PkaDeathSyphonComponent : Component
{
    [DataField]
    public float BonusPerKill = 0.05f;

    [DataField]
    public float MaxKills = 25f;

    [ViewVariables]
    public float Kills;
}

/// <summary>
/// Marks a living target hit by a death syphon bolt.
/// </summary>
[RegisterComponent]
public sealed partial class PkaSyphonMarkComponent : Component
{
    [ViewVariables]
    public EntityUid? Gun;

    [ViewVariables]
    public EntityUid? Shooter;
}
