// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Ninja.Abilities.Ghost;

/// <summary>
/// Ghost tier 3: holographic clones. The suit spawns several short living copies of the ninja
/// around the wearer and swaps places with one of them. The component lives on the suit.
/// </summary>
[RegisterComponent]
public sealed partial class NinjaGhostClonesComponent : Component
{
    /// <summary>
    /// Entity prototype of a single clone.
    /// </summary>
    [DataField]
    public EntProtoId CloneProto = "NinjaGhostHologram";

    /// <summary>
    /// Suit charge spent by one use.
    /// </summary>
    [DataField]
    public float EnergyCost = 50f;

    /// <summary>
    /// How many clones are spawned per use.
    /// </summary>
    [DataField]
    public int CloneAmount = 3;

    /// <summary>
    /// Inner radius of the ring around the wearer where clones appear.
    /// </summary>
    [DataField]
    public float MinSpawnRadius = 1f;

    /// <summary>
    /// Outer radius of the ring around the wearer where clones appear.
    /// </summary>
    [DataField]
    public float MaxSpawnRadius = 3f;

    /// <summary>
    /// How many random points are tried for a single clone.
    /// </summary>
    [DataField]
    public int MaxSpawnAttempts = 20;
}

/// <summary>
/// Marker component of a holographic clone. The clone splits itself on melee attacks, which is
/// configured by the fields below.
/// </summary>
[RegisterComponent]
public sealed partial class NinjaGhostCloneComponent : Component
{
    [ViewVariables]
    public EntityUid Owner;

    [DataField]
    public EntProtoId CloneProto = "NinjaGhostHologram";

    /// <summary>
    /// Chance to spawn another clone on every melee attack.
    /// </summary>
    [DataField]
    public float CloningChance = 0.25f;

    [DataField]
    public float MinSpawnRadius = 1f;

    [DataField]
    public float MaxSpawnRadius = 2f;
}

/// <summary>
/// Tells the client whose sprite a holographic clone has to copy.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class NinjaGhostCloneVisualComponent : Component
{
    // NetEntity, чтобы удаление ниндзя не оставляло в сетевом состоянии ссылку на него.
    [DataField, AutoNetworkedField]
    public NetEntity? Source;
}

/// <summary>
/// Raised when the holographic clones action is used.
/// </summary>
public sealed partial class NinjaGhostClonesEvent : InstantActionEvent;