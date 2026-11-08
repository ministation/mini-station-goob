// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

namespace Content.Server._Lavaland.Ashwalker;

/// <summary>
/// TG tribal bonfire: starts unlit, is lit by a hot item and burns everything standing on its tile.
/// </summary>
[RegisterComponent]
public sealed partial class TribeBonfireComponent : Component
{
    /// <summary>
    /// Is the bonfire currently burning.
    /// </summary>
    [DataField]
    public bool Lit;

    /// <summary>
    /// How often burning entities get fire stacks, in seconds. TG processes the bonfire every 2 seconds.
    /// </summary>
    [DataField]
    public float BurnInterval = 2f;

    /// <summary>
    /// Fire stacks applied per second of burning, TG: BONFIRE_FIRE_STACK_STRENGTH (5) * 0.5.
    /// </summary>
    [DataField]
    public float FireStacksPerSecond = 2.5f;

    [ViewVariables]
    public float Accumulator;
}
