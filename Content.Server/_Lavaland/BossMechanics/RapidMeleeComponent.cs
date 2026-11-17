// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// TG rapid_melee: a landed melee hit starts a short burst of follow-up hits on the same target.
/// The burst ignores the melee cooldown, like TG attacks scheduled from MeleeAction.
/// </summary>
[RegisterComponent]
public sealed partial class RapidMeleeComponent : Component
{
    /// <summary>
    /// Total hits in one burst, the initial hit included. TG Bubblegum has rapid_melee 8.
    /// </summary>
    [DataField]
    public int Hits = 8;

    /// <summary>
    /// Delay between burst hits. TG hits every 1/4 second.
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(0.25);

    [ViewVariables]
    public EntityUid? Weapon;

    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public int HitsLeft;

    [ViewVariables]
    public TimeSpan NextHit;
}
