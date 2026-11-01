// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Mobs;

/// <summary>
/// Heals the owner for a portion of melee damage dealt.
/// Used by the Guidance variant of the Blood-Drunk Miner.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class VampiricMeleeComponent : Component
{
    /// <summary>
    /// How much healing per point of damage dealt.
    /// </summary>
    [DataField]
    public float HealFraction = 0.5f;
}
