namespace Content.Shared._Mini.Converter;

[RegisterComponent]
public sealed partial class ConverterComponent : Component
{
    /// <summary>
    /// Progress required to mint one telecrystal.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int PointsPerTelecrystal = 5000;

    /// <summary>
    /// Value of a regular technology disk.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int TechnologyDiskPoints = 1000;

    /// <summary>
    /// Value of a rare technology disk.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int RareTechnologyDiskPoints = 2000;

    /// <summary>
    /// Current progress stored inside the converter.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int StoredPoints = 0;
}
