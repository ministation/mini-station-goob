// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lavaland.Elites.Events;

/// <summary>
/// Spawns a limited pack of goliath children around the performer.
/// </summary>
public sealed partial class SpawnBroodActionEvent : WorldTargetActionEvent
{
    [DataField(required: true)]
    public EntProtoId Child;

    [DataField]
    public int Count = 2;

    [DataField]
    public int MaxChildren = 8;
}

/// <summary>
/// Spawns a plus shaped patch of tentacles at the target position.
/// </summary>
public sealed partial class SpawnTentaclePatchActionEvent : WorldTargetActionEvent
{
    [DataField(required: true)]
    public EntProtoId Tentacle;
}

/// <summary>
/// Places one of a limited number of turrets at the target position.
/// </summary>
public sealed partial class SpawnTurretActionEvent : WorldTargetActionEvent
{
    [DataField(required: true)]
    public EntProtoId Turret;

    [DataField]
    public int MaxTurrets = 2;
}
