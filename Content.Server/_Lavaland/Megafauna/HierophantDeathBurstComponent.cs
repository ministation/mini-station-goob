// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Server._Lavaland.Megafauna;

/// <summary>
/// TG parity: on death the hierophant shrinks and releases a massive expanding energy burst.
/// </summary>
[RegisterComponent]
public sealed partial class HierophantDeathBurstComponent : Component
{
    /// <summary>
    /// Expanding shape spawner entity released at the death position.
    /// </summary>
    [DataField]
    public EntProtoId Burst = "HierophantShapeDeathBurst";
}
