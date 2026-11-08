// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.Map;

namespace Content.Server._Lavaland.Ruins;

/// <summary>
/// State of the lavaland sliding puzzle room. The room layout and the sliding
/// minigame live in <see cref="SlidingPuzzleSystem"/>.
/// </summary>
[RegisterComponent]
public sealed partial class PuzzleRoomSpawnerComponent : Component
{
    /// <summary>
    /// Which of the nine slots starts free. TG: one random tile is removed.
    /// </summary>
    [DataField]
    public int EmptySlot = 8;

    /// <summary>
    /// Entities spawned per slot; a missing key is the free slot.
    /// </summary>
    [ViewVariables]
    public Dictionary<int, EntityUid> Placement = new();

    /// <summary>
    /// True once every fragment sits on its slot and the chest has spawned.
    /// </summary>
    [ViewVariables]
    public bool Solved;

    /// <summary>
    /// True once a pillar has been destroyed: the reward is voided, TG parity.
    /// </summary>
    [ViewVariables]
    public bool Broken;

    [ViewVariables]
    public EntityUid Puzzle;

    [ViewVariables]
    public EntityCoordinates BaseCoords;

    [DataField]
    public string FloorPrototype = "LavalandStoneTileBurnt";

    /// <summary>
    /// Prefix of the nine fragment prototypes LavalandPuzzlePillar0..8.
    /// </summary>
    [DataField]
    public string PillarPrototype = "LavalandPuzzlePillar";

    [DataField]
    public string ChestPrototype = "LavalandPuzzleCrate";
}
