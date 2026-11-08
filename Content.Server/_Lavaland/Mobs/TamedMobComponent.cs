// SPDX-License-Identifier: AGPL-3.0-or-later

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
