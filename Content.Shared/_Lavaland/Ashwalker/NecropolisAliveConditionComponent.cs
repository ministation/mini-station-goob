// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

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
