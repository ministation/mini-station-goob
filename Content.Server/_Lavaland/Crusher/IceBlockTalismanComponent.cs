// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.Prototypes;

namespace Content.Server._Lavaland.Crusher;

/// <summary>
/// Ice block talisman crusher trophy: on mark detonation the target is frozen in an ice cube
/// and cannot move for a few seconds. Port of TG's ice_block_talisman status effect.
/// </summary>
[RegisterComponent]
public sealed partial class IceBlockTalismanComponent : Component
{
    [DataField]
    public EntProtoId Cube = "LavalandIceBlock";

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(4);
}
