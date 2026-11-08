// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Fauna;

/// <summary>
/// Marks the gutlunch family (gutlunch, warrior gunther, milk gubbuck, grublunch).
/// Used for the trough breeding population cap and to find breeding partners.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GutlunchComponent : Component
{
    /// <summary>
    /// True for milk gubbucks — the ones that spawn offspring at a trough.
    /// </summary>
    [DataField]
    public bool IsMother;
}
