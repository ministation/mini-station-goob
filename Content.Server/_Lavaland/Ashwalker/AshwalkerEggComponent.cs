// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Lavaland.Ashwalker;

/// <summary>
/// TG ash walker egg: hatches a ghost role member of the closest tribe, one per player,
/// and leaves a gibbed half-formed walker ("yolk") behind when broken.
/// </summary>
[RegisterComponent]
public sealed partial class AshwalkerEggComponent : Component
{
    /// <summary>
    /// Nest whose tribe the hatched walker joins. The per-ckey spawn limit and the
    /// sacrifice respawn unlock live on that tribe.
    /// </summary>
    [ViewVariables]
    public EntityUid Nest;
}
