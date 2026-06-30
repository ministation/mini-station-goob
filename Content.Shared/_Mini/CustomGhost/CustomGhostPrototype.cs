using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Mini.CustomGhost;

[Prototype("customGhost")]
public sealed class CustomGhostPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    [DataField("sprite", required: true)]
    public ResPath CustomSpritePath { get; } = default!;

    [DataField("alpha")]
    public float AlphaOverride { get; } = -1;

    [DataField("ghostName")]
    public string GhostName = string.Empty;

    [DataField("ghostDescription")]
    public string GhostDescription = string.Empty;

    [DataField("size")]
    public Vector2 SizeOverride = Vector2.One;

    [DataField]
    public ComponentRegistry Components = new();

    [DataField("price")]
    public int Price { get; } = 0;

    [DataField]
    public string Ckey { get; } = string.Empty;

    [DataField("order")]
    public int Order { get; } = 0;

    [DataField("name")]
    public string Name { get; } = string.Empty;

    [DataField("description")]
    public string Description { get; } = string.Empty;
}

[Serializable, NetSerializable]
public enum CustomGhostAppearance
{
    Sprite,
    AlphaOverride,
    SizeOverride,
    YAMLKOSTIL,
}
