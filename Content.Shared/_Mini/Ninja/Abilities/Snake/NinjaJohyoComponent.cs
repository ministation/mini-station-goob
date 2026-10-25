// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Shared.GameStates;
using Robust.Shared.Localization;
using Robust.Shared.Utility;

namespace Content.Shared._Mini.Ninja.Abilities.Snake;

/// <summary>
/// Snake tier 1: the chain kunai. Lives on the weapon summoned by the action.
/// The projectile it fires copies these settings on every shot.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NinjaJohyoComponent : Component
{
    /// <summary>
    /// Phrases the ninja says when firing the chain kunai.
    /// </summary>
    [DataField]
    public List<LocId> ShotPhrases = new();

    /// <summary>
    /// Acceleration applied to the impaled target, in meters per second squared.
    /// </summary>
    [DataField]
    public float PullAcceleration = 400f;

    /// <summary>
    /// Target speed limit of the pull along the direction to the ninja.
    /// </summary>
    [DataField]
    public float MaxPullSpeed = 15f;

    /// <summary>
    /// Distance at which the chain snaps and the target is released.
    /// </summary>
    [DataField]
    public float ArrivalDistance = 1f;

    /// <summary>
    /// Maximum length of the chain. The projectile is deleted past it.
    /// </summary>
    [DataField]
    public float MaxDistance = 10f;

    /// <summary>
    /// Sprite of the chain drawn between the kunai and the ninja.
    /// </summary>
    [DataField]
    public SpriteSpecifier ChainSprite =
        new SpriteSpecifier.Rsi(new ResPath("/Textures/_Mini/Ninja/Objects/Weapons/ninja.rsi"), "chain");
}

/// <summary>
/// The chain kunai projectile. Remembers the shooter and pulls the impaled target to them.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NinjaJohyoProjectileComponent : Component
{
    /// <summary>
    /// Who fired the kunai. The target is pulled towards them.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Shooter;

    /// <summary>
    /// What the kunai is embedded in.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Target;

    [DataField]
    public float PullAcceleration;

    [DataField]
    public float MaxPullSpeed;

    [DataField]
    public float ArrivalDistance;

    [DataField]
    public float MaxDistance;

    [DataField]
    public bool Pulling;
}
