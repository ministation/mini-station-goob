// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.Chasm;

/// <summary>
/// Keeps items that fell into a chasm, so they can be fished back out later.
/// </summary>
[RegisterComponent]
public sealed partial class ChasmStorageComponent : Component
{
    public const string ContainerId = "chasm_storage";

    /// <summary>
    /// Chance to pull a stored item instead of rolling the normal chasm loot table.
    /// </summary>
    [DataField]
    public float StoredChance = 0.75f;
}
