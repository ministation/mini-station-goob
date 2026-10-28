using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.CustomGhost;

/// <summary>
/// Уникальная способность темы GhostThemePumpkin: раз в 5 минут разбивать
/// одну целую лампочку поблизости. Компонент добавляется вместе с темой
/// через ComponentRegistry прототипа customGhost, поэтому есть только у неё.
/// </summary>
[RegisterComponent]
public sealed partial class PumpkinGhostComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionPumpkinBreakLight";

    [ViewVariables]
    public EntityUid? ActionEntity;

    /// <summary>Радиус поиска лампочек ("в комнате").</summary>
    [DataField]
    public float LightBreakRadius = 8f;
}

public sealed partial class PumpkinBreakLightActionEvent : InstantActionEvent { }
