using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Mini.CustomGhost;

[Prototype("customGhost")]
public sealed partial class CustomGhostPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("sprite", required: true)]
    public ResPath CustomSpritePath { get; private set; } = default!;

    [DataField("alpha")]
    public float AlphaOverride { get; private set; } = -1;

    [DataField("ghostName")]
    public string GhostName = string.Empty;

    [DataField("ghostDescription")]
    public string GhostDescription = string.Empty;

    [DataField("size")]
    public Vector2 SizeOverride = Vector2.One;

    [DataField]
    public ComponentRegistry Components = new();

    [DataField("price")]
    public int Price { get; private set; } = 0;

    /// <summary>
    /// Больше нуля — тема покупается за наигранное время (часы, трекер Overall), а не за монеты.
    /// </summary>
    [DataField("playtimeHours")]
    public int PlaytimeHours { get; private set; } = 0;

    [DataField]
    public string Ckey { get; private set; } = string.Empty;

    [DataField("order")]
    public int Order { get; private set; } = 0;

    [DataField("name")]
    public string Name { get; private set; } = string.Empty;

    [DataField("description")]
    public string Description { get; private set; } = string.Empty;
}

[Serializable, NetSerializable]
public enum CustomGhostAppearance
{
    Sprite,
    AlphaOverride,
    SizeOverride,
    YAMLKOSTIL,
}
