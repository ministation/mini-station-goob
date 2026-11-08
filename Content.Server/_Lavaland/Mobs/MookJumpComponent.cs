// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// Mook jump: a village mook that wanders too far hops back to the village.
/// TG parity: mook_jump ability (cooldown 14s, teleport to the
/// /obj/effect/landmark/mook_village when it leaves its village behind).
/// </summary>
[RegisterComponent]
public sealed partial class MookJumpComponent : Component
{
    /// <summary>
    /// Cooldown between jumps. TG: 14s.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(14);

    /// <summary>
    /// Distance from the village beyond which the mook jumps back.
    /// TG blackboard: BB_MAXIMUM_DISTANCE_TO_VILLAGE 7-10.
    /// </summary>
    [DataField]
    public float VillageDistance = 9f;

    [ViewVariables]
    public TimeSpan NextJump;
}
