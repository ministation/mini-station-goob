// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared._Lavaland.BossMechanics;

/// <summary>
/// Phase machine of The Thing: damage is capped at <see cref="PhaseHealth"/> per phase and once
/// a phase runs out the boss goes down, invulnerable, for <see cref="PhaseInvulnerabilityTime"/>.
/// It either recovers half a phase or gets pushed into the next phase by overloading one of the
/// molecular accelerators.
/// </summary>
[RegisterComponent]
public sealed partial class ThingBossComponent : Component
{
    /// <summary>
    /// Damage needed to deplete a single phase.
    /// </summary>
    [DataField]
    public FixedPoint2 PhaseHealth = 600;

    [DataField]
    public int MaxPhases = 3;

    [DataField]
    public TimeSpan PhaseInvulnerabilityTime = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How much health is restored when the boss is not pushed into the next phase in time.
    /// </summary>
    [DataField]
    public FixedPoint2 RecoverHeal = 300;

    /// <summary>
    /// Current phase, only advanced by the accelerator overload.
    /// </summary>
    [ViewVariables]
    public int Phase = 1;

    [ViewVariables]
    public bool Invulnerable;

    [ViewVariables]
    public TimeSpan DownedUntil;

    [DataField]
    public SoundSpecifier? DepletedSound;

    [DataField]
    public SoundSpecifier? OverloadSound;

    [DataField]
    public SoundSpecifier? RecoveredSound;
}

/// <summary>
/// Appearance keys used by the sprite of The Thing.
/// </summary>
[Serializable, NetSerializable]
public enum ThingVisuals : byte
{
    State,
}

/// <summary>
/// Appearance key of the molecular accelerator sprite.
/// </summary>
[Serializable, NetSerializable]
public enum ThingAcceleratorVisuals : byte
{
    State,
}