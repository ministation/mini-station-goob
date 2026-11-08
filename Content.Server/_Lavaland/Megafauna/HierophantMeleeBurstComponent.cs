// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Server._Lavaland.Megafauna;

/// <summary>
/// TG parity: when the hierophant lands a melee hit on a living target,
/// it releases an expanding AoE burst around itself.
/// </summary>
[RegisterComponent]
public sealed partial class HierophantMeleeBurstComponent : Component
{
    /// <summary>
    /// Shape spawner entity released at the hierophant's position.
    /// </summary>
    [DataField]
    public EntProtoId Shape = "HierophantShapeBoxExpanding";
}
