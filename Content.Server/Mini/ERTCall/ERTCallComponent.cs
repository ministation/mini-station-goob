using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Mini.ERTCall;

[RegisterComponent]
public sealed partial class ERTCallComponent : Component
{
    [ViewVariables]
    public ERTTeamPrototype? ERTTeams;

    [ViewVariables]
    public ERTTeamDetail? ERTCalledTeam;

    [DataField("ertTeamPrototype", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<ERTTeamPrototype>))]
    public string ERTTeamPrototype = default!;

    [ViewVariables(VVAccess.ReadWrite)]
    public bool ERTCalled = false;

    [DataField("timeToApprove")]
    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan TimeToApprove { get; private set; } = TimeSpan.FromMinutes(5);

    [DataField("timeToAnotherSpawn")]
    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan TimeToAnotherSpawn { get; private set; } = TimeSpan.FromMinutes(15);

    [DataField("wasApproved")]
    public bool WasApproved = false;

    [DataField("awaitsSpawn")]
    public bool AwaitsSpawn = false;

    [ViewVariables(VVAccess.ReadWrite)]
    public float ApproveCooldownRemaining = 0;

    [ViewVariables(VVAccess.ReadWrite)]
    public float SpawnCooldownRemaining = 0;

    [ViewVariables(VVAccess.ReadWrite)]
    public float NewCallCooldownRemaining = 0;
}
