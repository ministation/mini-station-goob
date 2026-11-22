// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// Turns the hierophant's blink into a blink slam: it lands on a random tile near the target
/// and the area around the landing spot is hit right after the teleport.
/// </summary>
[RegisterComponent]
public sealed partial class HierophantBlinkSlamComponent : Component
{
    /// <summary>
    /// Spawner that is placed on the landing spot once the blink finishes.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Slam;

    /// <summary>
    /// Max distance from the target at which the hierophant lands.
    /// </summary>
    [DataField]
    public float Radius = 2.5f;

    /// <summary>
    /// Delay between the blink and the slam, in seconds.
    /// </summary>
    [DataField]
    public float SlamDelay = 0.9f;

    /// <summary>
    /// Slams that were scheduled but not spawned yet.
    /// </summary>
    [ViewVariables]
    public List<(TimeSpan Time, EntityCoordinates Coords)> Pending = new();
}
