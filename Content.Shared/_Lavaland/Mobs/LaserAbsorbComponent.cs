// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Mobs;

/// <summary>
/// TG basilisk trait: laser damage is converted to cold damage,
/// so energy weapons barely help against it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LaserAbsorbComponent : Component
{
    /// <summary>
    /// Portion of laser damage converted to cold.
    /// </summary>
    [DataField]
    public float Rate = 1f;
}
