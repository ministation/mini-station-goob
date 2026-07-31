using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.ERP.Organs;

[RegisterComponent]
public sealed partial class EroticOrgansComponent : Component
{
    /// <summary>
    /// Organs spawned for all sexes in the groin slot.
    /// </summary>
    [DataField]
    public List<EroticOrganEntry> GroinCommon = [];

    /// <summary>
    /// Organs spawned for Male and Futanari in the groin slot.
    /// </summary>
    [DataField]
    public List<EroticOrganEntry> GroinMale = [];

    /// <summary>
    /// Organs spawned for Female and Futanari in the groin slot.
    /// </summary>
    [DataField]
    public List<EroticOrganEntry> GroinFemale = [];

    /// <summary>
    /// Organs spawned for Female and Futanari in the chest slot.
    /// </summary>
    [DataField]
    public List<EroticOrganEntry> ChestFemale = [];
}

[DataDefinition]
public sealed partial class EroticOrganEntry
{
    [DataField(required: true)]
    public EntProtoId Proto = default!;

    [DataField(required: true)]
    public string Slot = default!;
}
