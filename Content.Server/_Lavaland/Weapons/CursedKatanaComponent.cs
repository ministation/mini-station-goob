// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;

namespace Content.Server._Lavaland.Weapons;

/// <summary>
/// The cursed katana's combo system. Swings are recorded as bits: a swing that
/// connects is R, a whiff is L; finishing one of the six sequences triggers the
/// move. TG parity: datum/component/combo_attacks on the cursed katana.
/// </summary>
[RegisterComponent]
public sealed partial class CursedKatanaComponent : Component
{
    /// <summary>
    /// Extra damage of the hilt strike (LLR) plus a throw. TG: 17, throw 5 tiles.
    /// </summary>
    [DataField]
    public DamageSpecifier HiltStrikeDamage = new() { DamageDict = { { "Blunt", 17f } } };

    /// <summary>
    /// Tiles the hilt strike throws the victim.
    /// </summary>
    [DataField]
    public float HiltStrikeThrowDistance = 5f;

    /// <summary>
    /// Damage of the wide slice arc (RLL) to everyone around the wielder.
    /// </summary>
    [DataField]
    public DamageSpecifier WideSliceDamage = new() { DamageDict = { { "Slash", 15f } } };

    /// <summary>
    /// Radius of the wide slice arc.
    /// </summary>
    [DataField]
    public float WideSliceRadius = 1.6f;

    /// <summary>
    /// Extra damage of the dash attack (LRR) plus teleporting behind the target.
    /// TG: 17 puncture, up to 8 tiles.
    /// </summary>
    [DataField]
    public DamageSpecifier DashAttackDamage = new() { DamageDict = { { "Piercing", 17f } } };

    /// <summary>
    /// Extra damage of the tendon cut (RRL). TG: 15 with bleed stacks.
    /// </summary>
    [DataField]
    public DamageSpecifier TendonCutDamage = new() { DamageDict = { { "Slash", 15f } } };

    /// <summary>
    /// Seconds of dark cloak (LRLR). TG: 5s.
    /// </summary>
    [DataField]
    public TimeSpan CloakDuration = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Damage of the shatter finisher (RLRL): mining fauna take the fauna value,
    /// everything else the other one. TG: 75 / 35.
    /// </summary>
    [DataField]
    public DamageSpecifier ShatterFaunaDamage = new() { DamageDict = { { "Slash", 75f } } };

    [DataField]
    public DamageSpecifier ShatterDamage = new() { DamageDict = { { "Slash", 35f } } };

    /// <summary>
    /// Seconds the katana vanishes after a shatter. TG: 45s.
    /// </summary>
    [DataField]
    public TimeSpan ShatterVanishDuration = TimeSpan.FromSeconds(45);

    /// <summary>
    /// The recorded swing sequence, oldest first. True = connected swing (R).
    /// </summary>
    [ViewVariables]
    public List<bool> Combo = new();

    /// <summary>
    /// The last connected victim, for finishers that end on a whiff (shatter).
    /// </summary>
    [ViewVariables]
    public EntityUid? LastVictim;
}
