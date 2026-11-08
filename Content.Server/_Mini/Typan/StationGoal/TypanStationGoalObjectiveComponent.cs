// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Server._CorvaxGoob.StationGoal;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.Typan.StationGoal;

[RegisterComponent]
public sealed partial class TypanStationGoalObjectiveComponent : Component
{
    [DataField]
    public ProtoId<StationGoalPrototype> GoalId;
}
