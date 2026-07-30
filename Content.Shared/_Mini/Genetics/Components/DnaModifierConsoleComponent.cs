using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Genetics;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DnaModifierConsoleComponent : Component
{
    public const string ScannerPort = "MedicalScannerSender";

    [ViewVariables]
    public EntityUid? GeneticScanner = null;

    [DataField("maxDistance")]
    public float MaxDistance = 4f;

    public bool GeneticScannerInRange = true;

    public TimeSpan NextUpdate;
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Runtime cooldown timestamps. Not DataFields — CurTime values must not
    /// serialize into maps or fail PrototypeSaveTest / SaveLoadSaveTest.
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan LastInjectorTime;

    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan LastSubjectInjectTime;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan InjectorCooldown = TimeSpan.FromMinutes(2);

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan SubjectInjectCooldown = TimeSpan.FromMinutes(2);

    [DataField("clickSound"), ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier ClickSound = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");
}
