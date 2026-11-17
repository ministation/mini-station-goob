// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

﻿using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.EntityShapes.Components;

/// <summary>
/// Used for different shape spawner components to count new steps for spawns.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShapeSpawnerCounterComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan SpawnPeriod = TimeSpan.FromSeconds(1f);

    [DataField, AutoNetworkedField]
    public int MaxCounter = 1;

    [ViewVariables, AutoNetworkedField]
    public TimeSpan NextSpawn;

    [ViewVariables, AutoNetworkedField]
    public int Counter = 1; // We spawn 1 shape not in a loop, so we have to start from 1.

    /// <summary>
    /// Megafauna whose AI this series paused, null when the series does not pause AI.
    /// </summary>
    [ViewVariables]
    public EntityUid? PausedAiUser;

    /// <summary>
    /// True while <see cref="PausedAiUser"/> is waiting for this series to finish.
    /// </summary>
    [ViewVariables]
    public bool PausedAi;
}
