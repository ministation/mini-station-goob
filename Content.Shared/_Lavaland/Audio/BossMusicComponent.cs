// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lavaland.Audio;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BossMusicComponent : Component
{
    [AutoNetworkedField]
    [DataField] public ProtoId<BossMusicPrototype> SoundId;
}