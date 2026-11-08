// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lavaland.Weapons.Berserker;

/// <summary>
/// Berserker helmet from TG lavaland: absorbs the pain of its wearer to charge,
/// then unlocks a berserk mode (attack speed, brute resistance, projectile reflection,
/// no guns) that drains the charge until it runs out.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LavalandBerserkerComponent : Component
{
    /// <summary>
    /// Current berserk charge, from 0 to <see cref="MaxCharge"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Charge;

    /// <summary>
    /// Charge needed to unleash berserk mode.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxCharge = 100f;

    /// <summary>
    /// Berserk mode is currently active.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Active;

    /// <summary>
    /// The mob wearing the helmet right now, if any.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Wearer;

    /// <summary>
    /// How much of the taken damage (pre-armor) turns into charge.
    /// </summary>
    [DataField]
    public float DamageToCharge = 0.75f;

    /// <summary>
    /// Hits taken from projectiles charge the helmet this many times faster.
    /// </summary>
    [DataField]
    public float ProjectileChargeMultiplier = 1.5f;

    /// <summary>
    /// Charge drained per second while berserk is active.
    /// </summary>
    [DataField]
    public float DrainPerSecond = 5f;

    /// <summary>
    /// Melee attack speed multiplier while berserk is active.
    /// </summary>
    [DataField]
    public float AttackSpeedModifier = 4f;

    /// <summary>
    /// Incoming brute damage (blunt/slash/piercing) multiplier while berserk is active.
    /// </summary>
    [DataField]
    public float BruteResistMultiplier = 0.5f;

    /// <summary>
    /// Movement speed multiplier while berserk is active.
    /// </summary>
    [DataField]
    public float MovespeedModifier = 1.2f;

    /// <summary>
    /// The color of the rage light that surrounds the wearer while berserk is active.
    /// </summary>
    [DataField]
    public Color BerserkColor = Color.FromHex("#b34b4b");
}

/// <summary>
/// Applied to the wearer of a berserking helmet for as long as the mode lasts.
/// Holds everything needed to undo the changes when the mode ends.
/// </summary>
[RegisterComponent]
public sealed partial class BerserkingComponent : Component
{
    /// <summary>
    /// The helmet that is currently empowering the wearer.
    /// </summary>
    [DataField]
    public EntityUid Helmet;

    /// <summary>
    /// Whether berserk added the rage light to the wearer (and so should remove it).
    /// </summary>
    [DataField]
    public bool LightAdded;
}

/// <summary>
/// Marks a piece of the berserker set (armor or helmet) that can be permanently
/// empowered by feeding it ash drake hide, like TG's armor plate.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandBerserkerGearComponent : Component;

/// <summary>
/// Marks a berserker armor or helmet piece that has been fed ash drake hide,
/// permanently improving its armor values (TG's armor plate empowerment).
/// </summary>
[RegisterComponent]
public sealed partial class LavalandBerserkerEmpoweredComponent : Component;

/// <summary>
/// Action event fired by the berserker helmet to enter berserk mode.
/// </summary>
public sealed partial class BerserkModeActionEvent : InstantActionEvent
{
}
