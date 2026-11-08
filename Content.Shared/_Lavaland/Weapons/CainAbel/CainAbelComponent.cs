// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System;
using System.Collections.Generic;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lavaland.Weapons.CainAbel;

/// <summary>
/// Cain and Abel twin daggers. Every successful hit on a large mob grows the combo counter,
/// and every combo point is both extra damage and one stored blood wisp.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class CainAbelComponent : Component
{
    [DataField] public int MaxCombo = 6;

    [DataField] public float DamageBoost = 1.15f;

    /// <summary>Attack rate against large mobs: 0.6 s between swings.</summary>
    [DataField] public float LargeAttackRate = 1.6667f;

    /// <summary>Attack rate against everything else: 0.8 s between swings.</summary>
    [DataField] public float SmallAttackRate = 1.25f;

    [DataField, AutoNetworkedField] public int Combo;

    /// <summary>True for the crystal trap throw mode, false for the launch throw mode.</summary>
    [DataField, AutoNetworkedField] public bool Crystals = true;

    [DataField] public float ThrowRange = 9f;

    [DataField] public float CrystalsCooldown = 15f;

    [DataField] public float LaunchCooldown = 7f;

    [DataField] public EntProtoId CrystalsProjectile = "LavalandCainAbelThrowProjectileCrystal";

    [DataField] public EntProtoId LaunchProjectile = "LavalandCainAbelThrowProjectileLaunch";

    [DataField] public EntProtoId WispVisual = "LavalandCainAbelWisp";

    [DataField] public EntProtoId WispProjectile = "LavalandCainAbelWispProjectile";

    [DataField] public EntProtoId SwingStatusEffect = "LavalandStatusEffectDaggerSwinging";

    [DataField] public EntProtoId SwingVisual = "LavalandCainAbelSlash";

    [DataField] public SoundSpecifier ThrowSound = new SoundPathSpecifier("/Audio/_Lavaland/Weapons/CainAbel/fwoosh.ogg");

    /// <summary>Holder of the dagger, null when it is not in anyone's hands. Server only.</summary>
    public EntityUid? Holder;

    /// <summary>One wisp visual per combo point. Server only.</summary>
    public List<EntityUid> Wisps = new();

    /// <summary>Earliest time the next throw is allowed. Server only.</summary>
    public TimeSpan NextThrow;

    /// <summary>Whether the last melee hit landed on a large mob. Drives the attack rate.</summary>
    public bool LastTargetLarge;
}
