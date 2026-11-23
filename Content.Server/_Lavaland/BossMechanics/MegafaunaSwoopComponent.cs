// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// Turns a megafauna's blink into TG's lava swoop: the boss flies to the target while it cannot be
/// damaged and slams the landing tile. The flight time is the blink delay of the same entity.
/// </summary>
[RegisterComponent]
public sealed partial class MegafaunaSwoopComponent : Component
{
    /// <summary>
    /// Spawner dropped on the landing tile, for example the ash drake slam burst.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Slam;

    /// <summary>
    /// Red marker TG paints on the tile the boss is going to hit, so the swoop can be dodged.
    /// </summary>
    [DataField]
    public EntProtoId LandingMarker = "LavalandDrakeSwoopLanding";

    /// <summary>
    /// Shadow drawn on the ground under the flying boss, so the swoop reads as a jump.
    /// </summary>
    [DataField]
    public EntProtoId Shadow = "LavalandDrakeSwoopShadow";

    /// <summary>
    /// Sprite state used while the boss is in the air.
    /// </summary>
    [DataField]
    public string FlightState = "dragon_swoop";

    /// <summary>
    /// Sprite state restored on landing. The random sprite component is left in place with this
    /// state instead of being removed: the client only applies states on component updates, so a
    /// removal without a restore would leave the boss stuck in the flight sprite.
    /// </summary>
    [DataField]
    public string BaseState = "dragon";

    /// <summary>
    /// Touchdown moment, null while the boss is not swooping.
    /// </summary>
    [ViewVariables]
    public TimeSpan? LandAt;

    /// <summary>
    /// Flight duration in seconds, taken from the blink delay on takeoff.
    /// </summary>
    [ViewVariables]
    public float FlightSeconds;

    /// <summary>
    /// Takeoff position and the cast landing tile, only meaningful while <see cref="LandAt"/> is set.
    /// </summary>
    [ViewVariables]
    public MapCoordinates Origin;

    [ViewVariables]
    public EntityCoordinates Target;

    /// <summary>
    /// Live target of the AI, if any: the boss keeps aiming at it during the flight, like TG's
    /// step towards the target tile.
    /// </summary>
    [ViewVariables]
    public EntityUid? TargetEnt;

    /// <summary>
    /// True if the swoop added godmode itself, so that landing removes it again.
    /// </summary>
    [ViewVariables]
    public bool OwnsGodmode;

    /// <summary>
    /// True if the swoop changed the boss sprite and scale, so that landing restores them.
    /// </summary>
    [ViewVariables]
    public bool OwnsVisuals;

    /// <summary>
    /// AI is switched off while the boss is in the air, TG's swoop cannot cast mid flight.
    /// </summary>
    [ViewVariables]
    public bool PausedAi;

    [ViewVariables]
    public bool AiWasActive;

    [ViewVariables]
    public EntityUid? LandingMarkerEnt;

    [ViewVariables]
    public EntityUid? ShadowEnt;
}
