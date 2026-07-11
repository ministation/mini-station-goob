using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Mini.TypanWar;

/// <summary>
/// Persists war faction and respawn data on the mind across death/ghost.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TypanWarCombatMindComponent : Component
{
    public TypanWarSide Side;

    public EntityUid Station;

    public ProtoId<JobPrototype> Job = default!;

    public EntityCoordinates BaseSpawn;

    /// <summary>SecurityOfficer (NT) or TypanPatrol may respawn at roundstart spawn.</summary>
    public bool AllowBaseSpawn;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? RespawnAvailableAt;

    public bool RespawnUiOpen;
}
