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
    /// What to spawn on death. Used when <see cref="SpawnOnDeathList"/> is not set.
    /// </summary>
    [DataField]
    public EntProtoId? SpawnOnDeath;

    /// <summary>
    /// What to spawn on death, one entity per entry.
    /// Takes priority over <see cref="SpawnOnDeath"/> and <see cref="SpawnCount"/>.
    /// </summary>
    [DataField]
    public List<EntProtoId>? SpawnOnDeathList;

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
