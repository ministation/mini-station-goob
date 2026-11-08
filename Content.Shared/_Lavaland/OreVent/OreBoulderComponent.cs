// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.EntityTable;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lavaland.OreVent;

/// <summary>
/// A boulder produced by an ore vent. Miners break it with a pickaxe or a crusher
/// and it drops a roll of ore from <see cref="LootTable"/>.
/// </summary>
[RegisterComponent]
public sealed partial class OreBoulderComponent : Component
{
    /// <summary>
    /// How many successful hits the boulder takes before it crumbles.
    /// </summary>
    [DataField]
    public int HitsRemaining = 2;

    /// <summary>
    /// Table rolled when the boulder is broken.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<EntityTablePrototype> LootTable;

    /// <summary>
    /// Ores this boulder drops instead of <see cref="LootTable"/> when set by the vent that produced it.
    /// Empty for boulders spawned by hand or by admin, which keep their loot table.
    /// </summary>
    public List<EntProtoId> OreLoot = new();
}
