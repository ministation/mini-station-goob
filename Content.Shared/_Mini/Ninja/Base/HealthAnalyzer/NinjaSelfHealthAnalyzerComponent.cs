// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Ninja.Base.HealthAnalyzer;

/// <summary>
/// Allows the suit wearer to instantly scan their own health with the suit health analyzer.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NinjaSelfHealthAnalyzerComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionNinjaSelfAnalyze";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;
}

public sealed partial class NinjaSelfAnalyzeEvent : InstantActionEvent;
