// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// Makes a tamed mob rideable. Without <see cref="SaddleItems"/> it becomes rideable right
/// after taming (TG goldgrub), otherwise a saddle item has to be affixed first (TG goliath).
/// </summary>
[RegisterComponent]
public sealed partial class MountableMobComponent : Component
{
    /// <summary>
    /// Items used to saddle the mob. Empty means no saddle is required.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<EntityPrototype>> SaddleItems = new();

    /// <summary>
    /// Time to affix the saddle. TG: 5.5 seconds for a goliath.
    /// </summary>
    [DataField]
    public TimeSpan SaddleDelay = TimeSpan.FromSeconds(5.5);

    /// <summary>
    /// Speed the mob moves at while ridden, in meters per second. TG gives a ridden goliath
    /// a flat -9.5 move delay, which lands around running crew speed.
    /// </summary>
    [DataField]
    public float RideSpeed = 4.5f;

    /// <summary>
    /// Whether a saddle is attached and the mob can be buckled to.
    /// </summary>
    [ViewVariables]
    public bool Saddled;

    /// <summary>
    /// True while a rider is buckled to the mob, drives the riding speed boost.
    /// </summary>
    [ViewVariables]
    public bool Riding;
}
