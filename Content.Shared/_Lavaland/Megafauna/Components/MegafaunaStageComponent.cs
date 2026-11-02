// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Megafauna.Components;

/// <summary>
/// Current fight stage of a megafauna boss, shown in the boss HUD.
/// Stage 1 is the starting stage, MaxStage is the last stage.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MegafaunaStageComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Stage = 1;

    [DataField, AutoNetworkedField]
    public int MaxStage = 1;
}
