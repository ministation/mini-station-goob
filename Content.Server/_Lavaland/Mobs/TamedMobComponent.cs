// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// Marks a mob as tamed and remembers who did it.
/// </summary>
[RegisterComponent]
public sealed partial class TamedMobComponent : Component
{
    [ViewVariables]
    public EntityUid? Tamer;
}
