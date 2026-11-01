// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lavaland.Necropolis;

/// <summary>
/// Pulsating tumor from TG lavaland: when someone comes close, it spawns one
/// of the four elite monsters. A ghost may take control of the elite.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class EliteTumorComponent : Component
{
    /// <summary>
    /// Proximity radius that wakes the tumor, in meters.
    /// </summary>
    [DataField]
    public float TriggerRadius = 6f;

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

    public bool Triggered;

    [ViewVariables]
    public EntityUid? SpawnedElite;
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
