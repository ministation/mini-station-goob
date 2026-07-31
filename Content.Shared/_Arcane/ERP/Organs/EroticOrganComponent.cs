using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.ERP.Organs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EroticOrganComponent : Component
{
    /// <summary>
    /// Base sensitivity multiplier for arousal calculations. 1.0 = normal.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Sensitivity = 1.0f;

    /// <summary>
    /// Size modifier. 1.0 = average. Used in interaction condition checks and descriptions.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Size = 1.0f;

    /// <summary>
    /// Whether this organ is currently exposed (not covered by clothing).
    /// Managed by the clothing coverage system.
    /// </summary>
    [AutoNetworkedField]
    public bool Visible = true;
}
