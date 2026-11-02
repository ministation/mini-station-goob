// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Lavaland.Crusher;

/// <summary>
/// Marks a crusher main upgrade that replaced the built in kinetic marker,
/// so ejecting it has to put the marker components back on the crusher.
/// </summary>
[RegisterComponent]
public sealed partial class KineticMarkerUpgradeComponent : Component;
