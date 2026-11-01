// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// Mayhem in a bottle from TG lavaland, adapted: shattering it drives every
/// aggressive fauna nearby into a murderous frenzy (they all attack each other).
/// </summary>
[RegisterComponent]
public sealed partial class MayhemBottleComponent : Component
{
    /// <summary>
    /// Radius of the frenzy, in meters.
    /// </summary>
    [DataField]
    public float Range = 7f;
}
