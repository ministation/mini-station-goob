// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.Damage;
using Robust.Shared.Audio;

namespace Content.Shared._Lavaland.Weapons.Resonator;

public enum ResonatorMode : byte
{
    Auto,
    Manual,
    Matrix
}

/// <summary>
/// A mining tool that creates resonance fields that detonate and crush rock.
/// </summary>
[RegisterComponent]
public sealed partial class ResonatorComponent : Component
{
    [DataField]
    public ResonatorMode Mode = ResonatorMode.Auto;

    /// <summary>
    /// Whether the tool can switch into matrix mode.
    /// </summary>
    [DataField]
    public bool MatrixMode;

    [DataField]
    public int FieldLimit = 4;

    /// <summary>
    /// Damage multiplier for detonating a field early, before its timer runs out.
    /// </summary>
    [DataField]
    public float QuickBurstMod = 0.8f;

    /// <summary>
    /// Percentage penalty added to the failure chance of each field that spreads.
    /// </summary>
    [DataField]
    public int AddingFailure = 50;

    [DataField]
    public float AutoBurstDelay = 2f;

    [DataField]
    public float MatrixLifetime = 60f;

    [DataField]
    public SoundSpecifier FireSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/kinetic_accel.ogg");
}

/// <summary>
/// A field of resonating energy that drills rock and damages mobs when it ruptures.
/// </summary>
[RegisterComponent]
public sealed partial class ResonanceFieldComponent : Component
{
    [DataField]
    public DamageSpecifier Damage = new();

    /// <summary>
    /// Pressure in atmospheres above which the field loses its low pressure bonus.
    /// </summary>
    [DataField]
    public float PressureUpperBound = 0.5f;

    [DataField]
    public float PressureMultiplier = 3f;

    [DataField]
    public SoundSpecifier BurstSound = new SoundPathSpecifier("/Audio/WhiteDream/BloodCult/resonator_blast.ogg");

    [ViewVariables]
    public EntityUid? Creator;

    [ViewVariables]
    public EntityUid? Resonator;

    /// <summary>
    /// When this field was created; used to gate early detonation by clicks.
    /// </summary>
    [ViewVariables]
    public TimeSpan CreatedAt;

    [ViewVariables]
    public TimeSpan BurstAt;

    [ViewVariables]
    public float DamageMultiplier = 1f;

    /// <summary>
    /// Chance in percent that this field will not spread to adjacent rock.
    /// </summary>
    [ViewVariables]
    public float FailureProb;

    [ViewVariables]
    public int AddingFailure = 50;

    /// <summary>
    /// Matrix fields detonate when a mob walks onto them.
    /// </summary>
    [ViewVariables]
    public bool Matrix;

    [ViewVariables]
    public bool Bursting;
}
