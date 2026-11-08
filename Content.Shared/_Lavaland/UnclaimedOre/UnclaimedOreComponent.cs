// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.UnclaimedOre;

/// <summary>
///     Component that holds information about ore that hasn't been processed yet.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class UnclaimedOreComponent : Component
{
    [DataField, AutoNetworkedField]
    public float MiningPoints;
}
