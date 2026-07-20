using System.Threading;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Mini.DNALocker;

[RegisterComponent]
public sealed partial class DNALockerComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionHardsuitSaveDNA";

    [DataField]
    public EntityUid? ActionEntity;

    [DataField]
    public string DNA = string.Empty;

    public bool IsLocked => !string.IsNullOrEmpty(DNA);

    [DataField]
    public bool DNAWasStored;

    [DataField]
    public bool Activated;

    [DataField("actionIcon")]
    public SpriteSpecifier? ActionIcon;

    /// <summary>
    /// Emag sound effects.
    /// </summary>
    [DataField]
    public SoundSpecifier SparkSound = new SoundCollectionSpecifier("sparks")
    {
        Params = AudioParams.Default.WithVolume(8),
    };

    [DataField]
    public bool Nonlethal;

    [DataField("lockSound")]
    public SoundSpecifier LockSound = new SoundPathSpecifier("/Audio/_Mini/Misc/dna-lock.ogg");

    [DataField("lockerExplodeSound")]
    public SoundSpecifier LockerExplodeSound = new SoundPathSpecifier("/Audio/Effects/Grenades/SelfDestruct/SDS_Charge.ogg")
    {
        Params = AudioParams.Default.WithVolume(1),
    };

    [DataField("deniedSound")]
    public SoundSpecifier DeniedSound = new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");

    /// <summary>
    /// Cancels the self-destruct countdown (emag, unlock, or suit deletion).
    /// </summary>
    [NonSerialized]
    public CancellationTokenSource? ExplosionToken;
}
