// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System;
using System.Collections.Generic;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lavaland.Weapons.CainAbel;

/// <summary>
/// A crystal trap left by the crystal throw mode. Damages any mob standing on it and applies
/// the dagger stun, once per mob.
/// </summary>
[RegisterComponent]
public sealed partial class CainAbelCrystalComponent : Component
{
    [DataField] public float LargeDamage = 50f;

    [DataField] public float SmallDamage = 5f;

    [DataField] public EntProtoId StunEffect = "LavalandStatusEffectDaggerStun";

    [DataField] public EntProtoId StunVisual = "LavalandCainAbelStunVisual";

    [DataField] public TimeSpan StunDuration = TimeSpan.FromSeconds(2);

    [DataField] public SoundSpecifier HitSound = new SoundPathSpecifier("/Audio/Weapons/bladeslice.ogg");

    [DataField] public float CheckRadius = 0.5f;

    [DataField] public float CheckInterval = 0.2f;

    public TimeSpan NextCheck;

    public HashSet<EntityUid> Hit = new();
}

/// <summary>
/// A thrown dagger projectile. On hit it leaves an engraved dagger behind and either fills the
/// area with crystal traps or pulls the thrower to the impact point.
/// </summary>
[RegisterComponent]
public sealed partial class CainAbelThrowProjectileComponent : Component
{
    [DataField] public bool Launch;

    [DataField] public EntProtoId Engraved = "LavalandCainAbelEngraved";

    [DataField] public EntProtoId Crystal = "LavalandCainAbelCrystal";

    [DataField] public string ChainBeam = "LavalandCainAbelChainBeam";

    [DataField] public int Rings = 3;

    [DataField] public float RingInterval = 0.5f;

    [DataField] public SoundSpecifier CrystalSound = new SoundPathSpecifier("/Audio/_Lavaland/Weapons/CainAbel/crystal_dagger_sound.ogg");
}

/// <summary>
/// Status effect of the dagger swing ability: damages every living mob around the user on a tick.
/// </summary>
[RegisterComponent]
public sealed partial class DaggerSwingingComponent : Component
{
    [DataField] public float LargeDamage = 25f;

    [DataField] public float SmallDamage = 5f;

    /// <summary>Circular range that covers the eight surrounding tiles.</summary>
    [DataField] public float Radius = 1.5f;

    [DataField] public float TickInterval = 0.2f;

    [DataField] public SoundSpecifier HitSound = new SoundPathSpecifier("/Audio/Weapons/bladeslice.ogg");

    public TimeSpan NextTick;
}
