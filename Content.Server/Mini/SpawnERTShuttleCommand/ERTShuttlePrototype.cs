using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Mini.SpawnERTShuttleCommand;

/// <summary>
/// ERT shuttle id and path for loading it.
/// </summary>
[Prototype("ertShuttle")]
public sealed partial class ERTShuttlePrototype : IPrototype
{
    [IdDataField] public string ID { get; } = default!;

    [DataField(required: true)] public ResPath Path = new("Maps/Shuttles/dart.yml");
}
