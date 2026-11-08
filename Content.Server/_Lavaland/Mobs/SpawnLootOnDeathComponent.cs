// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// Drops some loot when boss having this component dies.
/// </summary>
[RegisterComponent]
public sealed partial class SpawnLootOnDeathComponent : Component
{
    [DataField]
    public EntityTableSelector? Table;

    [DataField]
    public EntityTableSelector? SpecialTable;

    /// <summary>
    /// Whitelist for the weapon that has to deal the killing blow for the special loot to drop.
    /// Melee, projectile and other direct weapon damage are checked, environmental damage never passes.
    /// When the killing blow does not pass, the mob falls back to the normal loot table.
    /// </summary>
    [DataField("weaponWhitelist")]
    public EntityWhitelist? SpecialWeaponWhitelist;

    [DataField]
    public bool DeleteOnDeath;

    /// <summary>
    /// If true and the mob was killed with special weapon,
    /// and both loots are not null, drops both loots at once.
    /// </summary>
    [DataField]
    public bool DropBoth;

    /// <summary>
    /// True when the last hit that damaged this entity came from a weapon passing
    /// <see cref="SpecialWeaponWhitelist"/>, in other words when the mob was killed with a crusher.
    /// </summary>
    [ViewVariables]
    public bool DoSpecialLoot;
}
