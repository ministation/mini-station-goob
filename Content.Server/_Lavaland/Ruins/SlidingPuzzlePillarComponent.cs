// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Lavaland.Ruins;

/// <summary>
/// One sliding-puzzle pillar. The picture fragment it carries never changes;
/// <see cref="SlotIndex"/> tracks where it currently stands.
/// </summary>
[RegisterComponent]
public sealed partial class SlidingPuzzlePillarComponent : Component
{
    /// <summary>
    /// The picture fragment this pillar shows; the puzzle is solved when every
    /// pillar stands on the slot matching its fragment.
    /// </summary>
    [DataField]
    public int FragmentIndex;

    /// <summary>
    /// Which of the nine slots this pillar currently occupies.
    /// </summary>
    [ViewVariables]
    public int? SlotIndex;

    /// <summary>
    /// The puzzle room this pillar belongs to.
    /// </summary>
    [ViewVariables]
    public EntityUid? Puzzle;
}
