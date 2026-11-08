// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Fauna;

/// <summary>
/// A feeding trough that stores ore for hungry lavaland fauna.
/// TG parity: /obj/structure/ore_container/food_trough (gutlunchers + raptors).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FoodTroughComponent : Component
{
    /// <summary>
    /// Maximum number of ore pieces the trough accepts.
    /// </summary>
    [DataField]
    public int MaxOre = 20;

    /// <summary>
    /// Ore pieces currently stored. Inserted ore is consumed, not kept as items.
    /// </summary>
    [ViewVariables]
    public int OreCount;

    /// <summary>
    /// Tag identifying accepted ore items.
    /// </summary>
    [DataField]
    public string OreTag = "Ore";

    /// <summary>
    /// Radius in which two gutlunches (a mother and a partner) count as breeding
    /// around this trough.
    /// </summary>
    [DataField]
    public float BreedingRadius = 3.5f;

    /// <summary>
    /// Cooldown between breedings at one trough. TG: breed timer 3 minutes.
    /// </summary>
    [DataField]
    public TimeSpan BreedCooldown = TimeSpan.FromMinutes(3);

    /// <summary>
    /// What a successful breeding spawns. Null disables breeding at this trough.
    /// </summary>
    [DataField]
    public EntProtoId? BreedingOffspring = "MobLavalandGutlunchGrub";

    /// <summary>
    /// Maximum live offspring of the same family allowed in the world.
    /// TG: MAX_RAPTOR_POP / MAXIMUM_GUTLUNCH_POP cap breeding.
    /// </summary>
    [DataField]
    public int MaxOffspringPop = 20;

    /// <summary>
    /// Cooldown between raptor breedings at one trough. TG: breed timer 3 minutes.
    /// </summary>
    [DataField]
    public TimeSpan RaptorBreedCooldown = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Maximum live raptors allowed in the world. TG: MAX_RAPTOR_POP 64.
    /// </summary>
    [DataField]
    public int MaxRaptorPop = 64;

    [ViewVariables]
    public TimeSpan NextBreed;

    [ViewVariables]
    public TimeSpan NextRaptorBreed;
}
