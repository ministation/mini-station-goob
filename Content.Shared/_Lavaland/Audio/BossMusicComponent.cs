// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lavaland.Audio;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BossMusicComponent : Component
{
    [AutoNetworkedField]
    [DataField] public ProtoId<BossMusicPrototype> SoundId;
}