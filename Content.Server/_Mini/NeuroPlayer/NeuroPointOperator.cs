// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Robust.Shared.Map;

namespace Content.Server._Mini.NeuroPlayer;

/// <summary>
/// Copies a pre-computed point (flee target or POI) from the blackboard into
/// <see cref="MoveToOperator"/>'s target coordinates. The point is chosen by
/// <see cref="NeuroPlayerSystem"/>, not by the planner.
/// </summary>
public sealed partial class NeuroPointOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField(required: true)]
    public string PointKey = "NeuroPoint";

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        if (!blackboard.TryGetValue<EntityCoordinates>(PointKey, out var point, _entManager))
            return HTNOperatorStatus.Failed;

        blackboard.SetValue("TargetCoordinates", point);
        return HTNOperatorStatus.Finished;
    }
}
