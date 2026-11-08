// SPDX-FileCopyrightText: 2026 Mini Station
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

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
