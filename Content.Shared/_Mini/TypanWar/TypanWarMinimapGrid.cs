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
/// Grid bounds and identity for the war minimap.
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

    public TypanWarMinimapGrid(
        NetEntity grid,
        float minX,
        float minY,
        float maxX,
        float maxY,
        TypanWarMinimapGridKind kind,
        string name = "")
    {
        Grid = grid;
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
        Kind = kind;
        Name = name;
    }
}
