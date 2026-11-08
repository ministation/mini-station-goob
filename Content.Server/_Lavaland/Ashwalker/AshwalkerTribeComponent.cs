// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Player;

namespace Content.Server._Lavaland.Ashwalker;

/// <summary>
/// State of one ash walker tribe. Lives on the nest, or on a grave entity once the nest is destroyed.
/// </summary>
[RegisterComponent]
public sealed partial class AshwalkerTribeComponent : Component
{
    /// <summary>
    /// The nest this tribe defends. Invalid for a fallen tribe.
    /// </summary>
    [ViewVariables]
    public EntityUid Nest;

    /// <summary>
    /// Minds that took an ash walker role in this tribe.
    /// </summary>
    [ViewVariables]
    public readonly HashSet<EntityUid> Members = new();

    /// <summary>
    /// Corpses the nest consumed.
    /// </summary>
    [ViewVariables]
    public int Sacrifices;

    /// <summary>
    /// Eggs the nest hatched.
    /// </summary>
    [ViewVariables]
    public int EggsCreated;

    /// <summary>
    /// Ckeys that already hatched from this tribe's eggs. TG's team players_spawned:
    /// one ash walker per person until a sacrifice clears the entry.
    /// </summary>
    [ViewVariables]
    public readonly HashSet<NetUserId> SpawnedCkeys = new();
}
