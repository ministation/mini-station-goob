// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// TG parity: wendigo shockwaves bend in flight (TG wave_speed changes the angle as the
/// projectile moves). Positive values turn left, negative turn right.
/// </summary>
[RegisterComponent]
public sealed partial class WendigoShockwaveCurveComponent : Component
{
    /// <summary>
    /// Angle added to the projectile direction every tick.
    /// </summary>
    [DataField]
    public float TurnDegrees = 8f;
}
