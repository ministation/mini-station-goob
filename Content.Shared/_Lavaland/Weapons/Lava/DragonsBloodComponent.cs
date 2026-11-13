// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Weapons.Lava;

/// <summary>
/// Bottle of dragon's blood from TG lavaland: drinking it gives a random
/// permanent effect. Outcome set follows the bug report: a cursed skeleton
/// polymorph, permanent lava immunity, or the ash drake shapeshift action.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DragonsBloodComponent : Component
{
    /// <summary>
    /// Polymorph into a skeleton.
    /// </summary>
    [DataField]
    public string SkeletonPolymorph = "DragonsBloodSkeleton";

    /// <summary>
    /// Action that lets the drinker turn into an ash drake.
    /// </summary>
    [DataField]
    public string DragonFormAction = "ActionDragonsBloodDragonForm";
}

/// <summary>
/// Granted to a mob that drank dragon's blood: permanent lava immunity.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LavaImmunityComponent : Component
{
}
