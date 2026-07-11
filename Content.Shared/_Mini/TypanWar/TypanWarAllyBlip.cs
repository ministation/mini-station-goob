using Robust.Shared.Serialization;

namespace Content.Shared._Mini.TypanWar;

[Serializable, NetSerializable]
public readonly struct TypanWarAllyBlip
{
    public readonly float WorldX;
    public readonly float WorldY;
    public readonly TypanWarSide Side;

    public TypanWarAllyBlip(float worldX, float worldY, TypanWarSide side)
    {
        WorldX = worldX;
        WorldY = worldY;
        Side = side;
    }
}
