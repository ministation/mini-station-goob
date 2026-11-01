// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// On death splits into several smaller legions, TG-style.
/// The smallest tier has no splits left and just drops loot.
/// </summary>
[RegisterComponent]
public sealed partial class LegionSplitComponent : Component
{
    /// <summary>
    /// What to spawn on death.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId SpawnOnDeath;

    /// <summary>
    /// How many entities to spawn on death.
    /// </summary>
    [DataField]
    public int SpawnCount = 3;

    /// <summary>
    /// How many times left this legion can split.
    /// </summary>
    [DataField]
    public int SplitsRemaining = 1;
}
