// SPDX-License-Identifier: LicenseRef-MiniStation-1.0


using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Mini.SpawnERTShuttleCommand;

/// <summary>
/// ERT shuttle id and path for loading it.
/// </summary>
[Prototype("ertShuttle")]
public sealed partial class ERTShuttlePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [DataField(required: true)] public ResPath Path = new("Maps/Shuttles/dart.yml");
}
