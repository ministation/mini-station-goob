using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lavaland.Megafauna.Events;

public sealed partial class SpawnEntityActionEvent : WorldTargetActionEvent
{
    [DataField(required: true)]
    public EntProtoId Spawn;

    /// <summary>
    /// If true, will attach spawned entity to the target.
    /// </summary>
    [DataField]
    public bool AttachToTarget;

    /// <summary>
    /// If this is true and <see cref="AttachToTarget"/> is false,
    /// will spawn the entity right at user's position.
    /// </summary>
    [DataField]
    public bool SpawnAtUser;
}

/// <summary>
/// Raised on a spawned entity by <see cref="SpawnEntityActionEvent"/>.
/// </summary>
[ByRefEvent]
public readonly record struct SpawnedByActionEvent(EntityUid User, EntityUid? Target);

public sealed partial class MegafaunaBlinkActionEvent : WorldTargetActionEvent;

public sealed partial class ToggleTileMovementActionEvent : EntityTargetActionEvent;

/// <summary>
///     Fired by the ash drake's lava arena action: encases the target in a fire arena
///     flooded with lava waves (TG enraged lava swoop).
/// </summary>
public sealed partial class MegafaunaLavaArenaActionEvent : WorldTargetActionEvent;
