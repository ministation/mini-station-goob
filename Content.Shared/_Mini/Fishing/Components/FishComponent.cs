// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

using Content.Shared._Mini.Fishing.Prototypes;

namespace Content.Shared._Mini.Fishing.Components;

/// <summary>
/// The fish itself. Fish are items with a size and a weight that are rolled when they spawn.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FishComponent : Component
{
    public const float DefaultDifficulty = 0.021f;

    /// <summary>
    /// Species data. Legacy Goob fish leave this null.
    /// </summary>
    [DataField]
    public ProtoId<FishPrototype>? Fish;

    /// <summary>
    /// Kept for the research experiments which compare fish difficulty.
    /// </summary>
    [DataField("difficulty")]
    public float FishDifficulty = DefaultDifficulty;

    [DataField, AutoNetworkedField]
    public float Size = 50f;

    [DataField, AutoNetworkedField]
    public float Weight = 1000f;

    [DataField, AutoNetworkedField]
    public bool Alive = true;

    /// <summary>
    /// Who caught this fish, shown on trophy mounts.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? Catcher;

    [DataField, AutoNetworkedField]
    public TimeSpan? CatchDate;

    /// <summary>
    /// When the fish was last fed, for aquarium hunger.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan? LastFed;

    /// <summary>
    /// How long the fish has been lying on dry land.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TimeOutOfWater;

    /// <summary>
    /// Traits added to this fish instance by genetic engineering.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<FishTraitPrototype>> ExtraTraits = new();

    /// <summary>
    /// Set after the first gene-gun operation. The next one kills the fish.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool GeneGunned;

    /// <summary>
    /// Traits removed from this fish instance by genetic engineering.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<FishTraitPrototype>> RemovedTraits = new();

    /// <summary>
    /// True once size and weight have been rolled, so they are not rolled twice on load.
    /// </summary>
    [DataField]
    public bool Initialized;
}