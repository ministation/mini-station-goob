// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

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

    /// <summary>When true, respawn UI offers the original job spawn location.</summary>
    public bool AllowBaseSpawn;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? RespawnAvailableAt;

    public bool RespawnUiOpen;

    /// <summary>Corpse to clean up after the next successful war respawn.</summary>
    public EntityUid? PendingCorpse;
}
