// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Mobs;

/// <summary>
///     Marker for whether a mob is fauna.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FaunaComponent : Component;
