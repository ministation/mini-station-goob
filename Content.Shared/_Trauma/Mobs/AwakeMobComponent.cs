// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Trauma.Mobs;

/// <summary>
/// Marker component added to mobs that are both alive and awake.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AwakeMobComponent : Component;
