using Robust.Shared.GameStates;

namespace Content.Shared.Genetics;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DnaClientComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public bool ConnectedToServer = false;

    /// <summary>
    /// Runtime-only link to the DNA server. Not a DataField so deleted servers
    /// cannot be written into map saves as missing entity references.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Server;
}
