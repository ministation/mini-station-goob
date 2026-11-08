// SPDX-License-Identifier: LicenseRef-MiniStation-1.0


using Content.Server._Mini.ERT;
using Content.Server._Mini.SpawnERTShuttleCommand;
using Content.Shared._Mini.ERT.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.ERTCall;

[RegisterComponent, Access(typeof(ErtResponceSystem))]
public sealed partial class ErtSpawnRuleComponent : Component
{
    public ProtoId<ErtTeamPrototype> Team;

    /// <summary>
    /// Цель вызова ERT отряда.
    /// </summary>
    public string? CallReason;

    /// <summary>
    /// Цель для pinpointer (для CriticalForce - игрок, которого нужно спасти).
    /// </summary>
    public EntityUid? PinpointerTarget;

    [DataField(required: true)]
    public ProtoId<ERTShuttlePrototype> Shuttle;
}
