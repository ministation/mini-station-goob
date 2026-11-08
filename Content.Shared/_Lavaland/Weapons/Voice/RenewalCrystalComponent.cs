// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Weapons.Voice;

/// <summary>
/// Anomalous crystal of renewal: single-use item that revives a nearby
/// corpse, or fully heals the user if nobody is dead around.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RenewalCrystalComponent : Component
{
    /// <summary>
    /// Range of corpse search, in meters.
    /// </summary>
    [DataField]
    public float Range = 3f;
}
