// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

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
}
