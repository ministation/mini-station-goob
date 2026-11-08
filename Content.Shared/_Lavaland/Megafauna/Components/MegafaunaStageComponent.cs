// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

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

    /// <summary>
    /// If set, the boss moves to MaxStage once its health drops below this fraction.
    /// Used by bosses without <see cref="Content.Shared._Lavaland.MobPhases.MobPhasesComponent"/>,
    /// for example Bubblegum enrages at half health.
    /// </summary>
    [DataField]
    public float? NextStageHealthFraction;
}
