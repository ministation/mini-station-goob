// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Aggression;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AggressorComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> Aggressives = new();
}
