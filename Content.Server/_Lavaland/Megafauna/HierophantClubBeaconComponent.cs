// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;

namespace Content.Server._Lavaland.Megafauna;

/// <summary>
/// TG parity: the hierophant club can detach its beacon at the user's feet, then
/// channel a group teleport of everyone near the user to the beacon.
/// </summary>
[RegisterComponent]
public sealed partial class HierophantClubBeaconComponent : Component
{
    /// <summary>
    /// The beacon entity.
    /// </summary>
    [ViewVariables]
    public EntityUid? Beacon;

    /// <summary>
    /// Deploying the beacon takes a 5 second ritual in TG.
    /// </summary>
    [DataField]
    public TimeSpan DeployDuration = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The group teleport channels for 4 seconds in TG.
    /// </summary>
    [DataField]
    public TimeSpan TeleportDuration = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Everyone within this range of the user travels to the beacon (TG range(1)).
    /// </summary>
    [DataField]
    public float TeleportRange = 1f;

    /// <summary>
    /// The user must be at least this far from the beacon to teleport (TG: too close).
    /// </summary>
    [DataField]
    public float MinBeaconDistance = 2f;

    [DataField]
    public SoundSpecifier? DeploySound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");

    [DataField]
    public SoundSpecifier? DepartSound = new SoundPathSpecifier("/Audio/Effects/teleport_departure.ogg");

    [ViewVariables]
    public bool Teleporting;
}

/// <summary>
/// Marker for the placed beacon entity.
/// </summary>
[RegisterComponent]
public sealed partial class HierophantBeaconComponent : Component;
