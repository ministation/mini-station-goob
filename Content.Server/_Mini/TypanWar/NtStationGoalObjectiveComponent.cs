// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Server._CorvaxGoob.StationGoal;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.TypanWar;

[RegisterComponent]
public sealed partial class NtStationGoalObjectiveComponent : Component
{
    [DataField]
    public ProtoId<StationGoalPrototype> GoalId;
}
