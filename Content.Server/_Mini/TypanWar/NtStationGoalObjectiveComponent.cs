// SPDX-FileCopyrightText: 2026 Egorik1
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Server._CorvaxGoob.StationGoal;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.TypanWar;

[RegisterComponent]
public sealed partial class NtStationGoalObjectiveComponent : Component
{
    [DataField]
    public ProtoId<StationGoalPrototype> GoalId;
}
