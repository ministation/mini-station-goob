using Content.Server._Mini.Research.Systems;

namespace Content.Server._Mini.Research.Components;

[RegisterComponent, Access(typeof(ResearchDataDiskSystem))]
public sealed partial class ResearchDataDiskComponent : Component
{
    [DataField]
    public bool HasDataSnapshot;

    [DataField]
    public List<string> StoredTechnologies = new();

    [DataField]
    public string? SnapshotServerName;
}
