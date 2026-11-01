// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Wishgranter;

/// <summary>
/// Wish granter cube: grants one wish, TG-style. The wish always twists.
/// </summary>
[RegisterComponent]
public sealed partial class WishgranterComponent : Component
{
    /// <summary>
    /// How many wishes are left.
    /// </summary>
    [DataField]
    public int Charges = 1;

    /// <summary>
    /// The granter only listens after a second, confirming touch.
    /// </summary>
    [DataField]
    public bool Insisting;
}

/// <summary>
/// Marks the avatar of the Wish Granter.
/// </summary>
[RegisterComponent]
public sealed partial class WishgranterAvatarComponent : Component;

/// <summary>
/// Landmark spawner for the wish granter cube.
/// </summary>
[RegisterComponent]
public sealed partial class WishgranterSpawnerComponent : Component;
