// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lavaland.Necropolis;

/// <summary>
/// Necropolis gate from TG lavaland: knock on it enough times and Legion is released.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NecropolisGateComponent : Component
{
    /// <summary>
    /// How many knocks (with pauses) are needed to open the gate.
    /// </summary>
    [DataField]
    public int KnocksRequired = 2;

    /// <summary>
    /// Minimal delay between two knocks, in seconds.
    /// </summary>
    [DataField]
    public float KnockCooldown = 2f;

    /// <summary>
    /// Knocks older than this window are reset, in seconds.
    /// </summary>
    [DataField]
    public float KnockWindow = 15f;

    /// <summary>
    /// What to spawn when the gate opens. TG spawns Legion.
    /// </summary>
    [DataField]
    public EntProtoId SpawnOnOpen = "LavalandBossMegaLegion";

    public int Knocks;
    public TimeSpan? LastKnock;
    public TimeSpan? LastKnockAttempt;

    [ViewVariables]
    public bool Open;
}

[Serializable, NetSerializable]
public enum NecropolisGateVisuals
{
    State,
}

[Serializable, NetSerializable]
public enum NecropolisGateVisualState
{
    Closed,
    Opening,
    Open,
}

/// <summary>
/// Marker entity that builds the whole necropolis gate landmark:
/// stone arena, tower of indestructible walls and the gate itself.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NecropolisGateSpawnerComponent : Component
{
    /// <summary>
    /// Half-size of the stone arena patch, in tiles.
    /// </summary>
    [DataField]
    public int ArenaRadius = 5;

    /// <summary>
    /// Offset of the gate from the spawner, in tiles. The gate faces the arena.
    /// </summary>
    [DataField]
    public Vector2i GateOffset = (0, 6);

    /// <summary>
    /// Height of the stone tower behind the gate, in tiles.
    /// </summary>
    [DataField]
    public int TowerHeight = 3;
}
