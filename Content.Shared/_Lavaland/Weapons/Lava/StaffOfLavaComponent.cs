// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Weapons.Lava;

/// <summary>
/// Staff of lava from TG lavaland: surrounds the user with lava pools,
/// or removes nearby pools on a second use.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class StaffOfLavaComponent : Component
{
    /// <summary>
    /// Radius of the lava ring, in meters.
    /// </summary>
    [DataField]
    public float Radius = 2.5f;
}

public sealed partial class StaffOfLavaActionEvent : InstantActionEvent
{
}
