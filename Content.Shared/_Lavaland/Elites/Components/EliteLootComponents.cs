// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lavaland.Elites.Components;

/// <summary>
/// Marker for lavaland elites. The tumor shard only revives mobs with this component.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandEliteComponent : Component
{
}

/// <summary>
/// Herald cloak trophy: when the wearer is hit by a projectile, sometimes answers with a ring of bolts.
/// </summary>
[RegisterComponent]
public sealed partial class HeraldCloakComponent : Component
{
    /// <summary>
    /// Chance to answer a projectile hit, from 0 to 1.
    /// </summary>
    [DataField]
    public float Chance = 0.2f;

    [DataField]
    public EntProtoId Projectile = "LavalandHeraldBolt";

    [DataField]
    public int Count = 8;

    [DataField]
    public float Speed = 14f;
}

/// <summary>
/// Tumor shard trophy: revives a dead elite as a passive mob that is friendly to the crew.
/// </summary>
[RegisterComponent]
public sealed partial class TumorShardComponent : Component
{
    /// <summary>
    /// Fraction of the elite's dead threshold applied as damage on revival, so it comes back hurt.
    /// </summary>
    [DataField]
    public float HealthFraction = 0.5f;

    [DataField]
    public ProtoId<NpcFactionPrototype> Faction = "NanoTrasen";
}
