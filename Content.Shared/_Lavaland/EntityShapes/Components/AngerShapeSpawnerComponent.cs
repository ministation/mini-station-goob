// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

﻿using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.EntityShapes.Components;

/// <summary>
/// Scales <see cref="ShapeSpawnerCounterComponent"/> with anger
/// of an owner that spawned this EntityShapeSpawner.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AngerShapeSpawnerComponent : Component
{
    [DataField("counterRange"), AutoNetworkedField]
    public Vector2i? MaxCounterRange;

    [DataField("inverseCounter"), AutoNetworkedField]
    public bool InverseCounter;

    [DataField("periodRange"), AutoNetworkedField]
    public Vector2? SpawnPeriodRange;

    [DataField("inversePeriod"), AutoNetworkedField]
    public bool InverseSpawnPeriod;

    /// <summary>
    /// TG parity: while the spawned counter series is running the user's megafauna AI is paused,
    /// like TG sets blinking = TRUE for the whole cross_blast_spam. The AI is restored when the
    /// series ends.
    /// </summary>
    [DataField("pauseAi"), AutoNetworkedField]
    public bool PauseAi;
}
