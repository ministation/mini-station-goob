// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

﻿using System.Numerics;
using Content.Shared._Lavaland.EntityShapes.Shapes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lavaland.EntityShapes.Components;

/// <summary>
/// Spawns an entity shape on MapInit.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ShapeSpawnerComponent : Component
{
    [DataField(required: true)]
    public EntityShape Shape;

    [DataField(required: true)]
    public EntProtoId Spawn;

    /// <summary>
    /// If true, aligns center coordinates of a spawner to the nearest tile.
    /// Used for tile patterns to be more stable when the origin
    /// is located on the edge between 2 or more tiles.
    /// </summary>
    [DataField]
    public bool AlignCoords;

    /// <summary>
    /// If set, overrides <see cref="EntityShape.DefaultSize"/> with a size scaled by the anger of the user
    /// that spawned this shape, from X (no anger) to Y (max anger). Used by TG parity to grow the
    /// hierophant blast and beam radii. Other shapes leave the field empty and keep their YAML size.
    /// </summary>
    [DataField]
    public Vector2i? AngerSizeRange;
}
