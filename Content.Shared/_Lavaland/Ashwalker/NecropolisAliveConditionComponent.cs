// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Lavaland.Ashwalker;

/// <summary>
/// Ash walker protect objective condition: complete while the nest it points at exists.
/// </summary>
[RegisterComponent]
public sealed partial class NecropolisAliveConditionComponent : Component
{
    /// <summary>
    /// The nest the objective wants protected. Set when the tribe hands the objective out.
    /// </summary>
    [DataField]
    public EntityUid? Nest;
}
