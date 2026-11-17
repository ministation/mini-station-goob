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
    /// Takeoff position and landing tile, only meaningful while <see cref="LandAt"/> is set.
    /// </summary>
    [ViewVariables]
    public MapCoordinates Origin;

    [ViewVariables]
    public EntityCoordinates Target;

    /// <summary>
    /// True if the swoop added godmode itself, so that landing removes it again.
    /// </summary>
    [ViewVariables]
    public bool OwnsGodmode;
}
