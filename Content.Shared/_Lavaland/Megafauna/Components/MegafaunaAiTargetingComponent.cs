using Robust.Shared.Map;

namespace Content.Shared._Lavaland.Megafauna.Components;

/// <summary>
/// Component that stores data for what Megafauna is currently targeting.
/// </summary>
[RegisterComponent]
public sealed partial class MegafaunaAiTargetingComponent : Component
{
    [DataField]
    public EntityUid? TargetEnt;

    /// <summary>
    /// Used to reference a general position instead of some specific entity.
    /// </summary>
    [DataField]
    public EntityCoordinates? TargetCoords;
}
