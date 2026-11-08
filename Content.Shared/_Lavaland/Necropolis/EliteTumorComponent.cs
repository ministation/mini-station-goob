// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lavaland.Necropolis;

/// <summary>
/// Pulsating tumor from TG lavaland. Inactive tumors can be woken by hand:
/// they spawn one of the four elites after a short delay and seal the arena.
/// Killing the elite drops a boosted loot crate, dying to it makes the tumor
/// passive so the fight can be retried.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class EliteTumorComponent : Component
{
    /// <summary>
    /// Elites that can be spawned, one is picked at random.
    /// </summary>
    [DataField]
    public List<EntProtoId> Elites = new()
    {
        "LavalandEliteLegionnaire",
        "LavalandElitePandora",
        "LavalandEliteHerald",
        "LavalandEliteGoliathBroodmother",
    };

    /// <summary>
    /// Delay between the activation and the elite spawn.
    /// </summary>
    [DataField]
    public TimeSpan SpawnDelay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Radius of the forcefield arena built around the tumor on activation, in tiles.
    /// </summary>
    [DataField]
    public int ArenaRadius = 12;

    /// <summary>
    /// Wall prototype used for the arena forcefield.
    /// </summary>
    [DataField]
    public EntProtoId ArenaWall = "WallHierophantArenaTemporary";

    /// <summary>
    /// Item used to boost the tumor before the fight starts.
    /// </summary>
    [DataField]
    public EntProtoId BoostItem = "MaterialLegionSkull1";

    /// <summary>
    /// Loot crate spawned when the elite dies.
    /// </summary>
    [DataField]
    public EntProtoId LootCrate = "LavalandCrateNecropolis";

    /// <summary>
    /// Loot spawned in the crate when the trophy roll fails.
    /// </summary>
    [DataField]
    public EntProtoId FallbackLoot = "LavalandTumorShard";

    /// <summary>
    /// Chance that the boosted crate contains the elite trophy instead of the fallback loot.
    /// </summary>
    [DataField]
    public float TrophyChance = 0.5f;

    /// <summary>
    /// The dead health threshold of a boosted elite is multiplied by this after it wins.
    /// TG uses 0.4 and restores it back on the next activation.
    /// </summary>
    [DataField]
    public float PostBattleHealthMultiplier = 0.4f;

    /// <summary>
    /// Trophy dropped by each elite. Keys are elite prototype ids.
    /// </summary>
    [DataField]
    public Dictionary<string, EntProtoId> Trophies = new()
    {
        { "LavalandEliteLegionnaire", "LavalandLegionnaireSpine" },
        { "LavalandElitePandora", "LavalandPandoraHope" },
        { "LavalandEliteHerald", "LavalandHeraldCloak" },
        { "LavalandEliteGoliathBroodmother", "LavalandBroodmotherTongue" },
    };

    /// <summary>
    /// Current fight state. Only driven by the server, the client uses appearance data.
    /// </summary>
    [ViewVariables]
    public EliteTumorState State = EliteTumorState.Inactive;

    [ViewVariables]
    public EntityUid? Activator;

    [ViewVariables]
    public EntityUid? SpawnedElite;

    [ViewVariables]
    public EntProtoId? SpawnEliteProto;

    [ViewVariables]
    public TimeSpan SpawnAt;

    [ViewVariables]
    public bool Boosted;

    [ViewVariables]
    public int TimesWon;

    [ViewVariables]
    public List<EntityUid> Walls = new();

    /// <summary>
    /// Dead threshold recorded when the elite spawned, used to restore it for a rematch.
    /// </summary>
    [ViewVariables]
    public FixedPoint2? BaseDeadThreshold;
}

/// <summary>
/// Marks the entity that woke a tumor, so its death can be tracked.
/// </summary>
[RegisterComponent]
public sealed partial class EliteTumorActivatorComponent : Component
{
    public EntityUid Tumor;
}

/// <summary>
/// Marks an elite spawned by a tumor, so its death can be tracked.
/// </summary>
[RegisterComponent]
public sealed partial class EliteTumorTrackerComponent : Component
{
    public EntityUid Tumor;
}

[Serializable, NetSerializable]
public enum EliteTumorState
{
    Inactive,
    Active,
    Passive,
}

[Serializable, NetSerializable]
public enum EliteTumorVisuals
{
    State,
}

[Serializable, NetSerializable]
public enum EliteTumorVisualState
{
    Dormant,
    Advanced,
    Popped,
}
