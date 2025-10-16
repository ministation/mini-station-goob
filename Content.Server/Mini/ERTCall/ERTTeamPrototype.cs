using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Utility;
using Robust.Shared.Prototypes;

namespace Content.Server.Mini.ERTCall;

[Prototype("ertTeams")]
public sealed partial class ERTTeamPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("teams")]
    public Dictionary<string, ERTTeamDetail> Teams { get; private set; } = new();
}

[DataDefinition]
public sealed partial class ERTTeamDetail
{
    [DataField("name")]
    public string Name { get; private set; } = string.Empty;

    [DataField("announcementAfterCall")]
    public string AnnouncementCall { get; private set; } = string.Empty;

    [DataField("shuttle", customTypeSerializer: typeof(ResPathSerializer))]
    public ResPath ShuttlePath { get; private set; } = new("/Maps/Shuttles/dart.yml");

    [DataField("humansList")]
    public Dictionary<string, int> HumansList { get; private set; } = new();

    [DataField("timeToSpawn")]
    public TimeSpan TimeToSpawn { get; private set; } = TimeSpan.FromMinutes(1);

    [DataField("mustApproveAdmin")]
    public bool MustApproveAdmin { get; private set; } = true;

    [DataField("requiredRoundDuration")]
    public int RequiredRoundDuration { get; private set; } = 5;
}
