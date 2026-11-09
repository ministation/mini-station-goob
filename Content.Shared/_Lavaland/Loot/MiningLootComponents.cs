// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lavaland.Loot;

/// <summary>
/// Rod of Asclepius: heals its holder and everyone living around them while held.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RodOfAsclepiusAuraComponent : Component
{
    [DataField]
    public float Range = 2.5f;

    [DataField]
    public float Interval = 2f;

    [DataField]
    public float HealAmount = 3f;

    public float Accumulator;
}

/// <summary>
/// Immortality talisman: grants a short window of absolute invulnerability on use.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImmortalityTalismanComponent : Component
{
    [DataField]
    public float Duration = 10f;
}

/// <summary>
/// Memento mori necklace: binds its wearer to itself on use.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MementoMoriComponent : Component
{
}

/// <summary>
/// Placed on the wearer of a bound memento mori necklace.
/// </summary>
[RegisterComponent]
public sealed partial class MementoMoriBoundComponent : Component
{
    [DataField]
    public EntityUid Necklace;
}

/// <summary>
/// Strange elixir: grants flight on use.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FlightPotionComponent : Component
{
}

/// <summary>
/// Book of Babel: teaches every known language on use.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BookOfBabelComponent : Component
{
}

/// <summary>
/// Spooky lantern: releases and recalls a friendly wisp that follows its user.
/// </summary>
[RegisterComponent]
public sealed partial class WispLanternComponent : Component
{
    [DataField]
    public EntProtoId WispProto = "LavalandFriendlyWisp";

    [ViewVariables]
    public EntityUid? Wisp;

    [ViewVariables]
    public bool GaveThermalVision;
}

/// <summary>
/// Warp cube: teleports its user to its linked twin. The red cube spawns the blue one.
/// </summary>
[RegisterComponent]
public sealed partial class WarpCubeComponent : Component
{
    [DataField]
    public EntProtoId? PartnerProto;

    [DataField]
    public EntityUid? Partner;

    [ViewVariables]
    public bool SpawnedPartner;
}

/// <summary>
/// Brimdust sac: coats its user in explosive brimdust when used.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BrimdustSacComponent : Component
{
    /// <summary>
    /// How many coating stacks one use applies.
    /// </summary>
    [DataField]
    public int Stacks = 3;
}

/// <summary>
/// Brimdust coating, tracked by the BrimdustCoating status effect.
/// </summary>
[RegisterComponent]
public sealed partial class BrimdustCoatingComponent : Component
{
    public int Stacks;
}

