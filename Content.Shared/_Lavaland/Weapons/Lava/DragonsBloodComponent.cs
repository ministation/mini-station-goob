// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Weapons.Lava;

/// <summary>
/// Bottle of dragon's blood from TG lavaland: drinking it gives a random
/// permanent effect. Adapted subset of TG outcomes: fire breath spell,
/// permanent lava immunity, or a cursed skeleton polymorph.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DragonsBloodComponent : Component
{
    /// <summary>
    /// Polymorph into a skeleton.
    /// </summary>
    [DataField]
    public string SkeletonPolymorph = "DragonsBloodSkeleton";
}

/// <summary>
/// Granted to a mob that drank dragon's blood: permanent lava immunity.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LavaImmunityComponent : Component
{
}
