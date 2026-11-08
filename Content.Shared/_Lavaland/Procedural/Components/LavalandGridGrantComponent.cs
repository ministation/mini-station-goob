// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lavaland.Procedural.Components;

/// <summary>
/// Component that is used for granting components to entities that enter the ruin.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LavalandGridGrantComponent : Component
{
    /// <summary>
    /// List of components to grant to entities that enter the ruin.
    /// </summary>
    [DataField]
    public ComponentRegistry ComponentsToGrant = new();
}
