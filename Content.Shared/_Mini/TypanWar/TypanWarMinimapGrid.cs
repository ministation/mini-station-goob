// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Numerics;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._Mini.TypanWar;

[Serializable, NetSerializable]
public enum TypanWarMinimapGridKind : byte
{
    NtStation,
    TypanStation,
    NtShuttle,
    TypanShuttle,
    Trade,
}

/// <summary>
/// Grid silhouette for the war minimap (same mesh layout as shuttle radar / mass scanner).
/// Vertices are in grid-local space; apply <see cref="WorldMatrix"/> then project to the map view.
/// When <see cref="Vertices"/> is null the client keeps its previously cached silhouette.
/// </summary>
[Serializable, NetSerializable]
public readonly struct TypanWarMinimapGrid
{
    public readonly NetEntity Grid;
    public readonly float MinX;
    public readonly float MinY;
    public readonly float MaxX;
    public readonly float MaxY;
    public readonly TypanWarMinimapGridKind Kind;
    public readonly string Name;

    /// <summary>Grid-local fill + edge vertices (edge starts at <see cref="EdgeIndex"/>).</summary>
    public readonly Vector2[]? Vertices;
    public readonly int EdgeIndex;
    public readonly uint ShapeVersion;

    // World matrix of the grid (row-major 2x3).
    public readonly float M11;
    public readonly float M12;
    public readonly float M21;
    public readonly float M22;
    public readonly float M31;
    public readonly float M32;

    public TypanWarMinimapGrid(
        NetEntity grid,
        float minX,
        float minY,
        float maxX,
        float maxY,
        TypanWarMinimapGridKind kind,
        string name,
        Vector2[]? vertices,
        int edgeIndex,
        uint shapeVersion,
        Matrix3x2 worldMatrix)
    {
        Grid = grid;
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
        Kind = kind;
        Name = name;
        Vertices = vertices;
        EdgeIndex = edgeIndex;
        ShapeVersion = shapeVersion;
        M11 = worldMatrix.M11;
        M12 = worldMatrix.M12;
        M21 = worldMatrix.M21;
        M22 = worldMatrix.M22;
        M31 = worldMatrix.M31;
        M32 = worldMatrix.M32;
    }

    public Matrix3x2 WorldMatrix => new(M11, M12, M21, M22, M31, M32);
}
