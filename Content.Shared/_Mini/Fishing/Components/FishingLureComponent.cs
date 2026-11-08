// SPDX-License-Identifier: LicenseRef-MiniStation-1.0
// Mechanics reference: tgstation/tgstation (code/modules/fishing).

using Robust.Shared.GameStates;

namespace Content.Shared._Mini.Fishing.Components;

/// <summary>
/// The float / bobber that is currently cast out. Held by the fishing rod while the challenge runs.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishingLureComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Rod;

    /// <summary>
    /// Entity that hosts the fishing source, if any.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? SourceEntity;

    /// <summary>
    /// Whether a fish is currently nibbling the float. Drives the bobber animation.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Biting;

    /// <summary>
    /// Whether this lure is snagged onto an item or creature instead of fishing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Snagged;

    /// <summary>
    /// Trap applied to a creature by the jawed hook, freed when the line is reeled in or snaps.
    /// </summary>
    [DataField]
    public EntityUid? Snare;
}
