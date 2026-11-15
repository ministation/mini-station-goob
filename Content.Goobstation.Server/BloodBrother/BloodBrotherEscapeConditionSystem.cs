// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Shuttles.Systems;
using Content.Shared.Cuffs.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;

namespace Content.Goobstation.Server.BloodBrother;

/// <summary>
/// Team-aware version of <see cref="Content.Server.Objectives.Systems.EscapeShuttleConditionSystem"/>.
/// Every brother has to make it out alive, so a dead or captured brother drags the
/// progress down instead of the objective silently completing for the survivors.
/// </summary>
public sealed partial class BloodBrotherEscapeConditionSystem : EntitySystem
{
    [Dependency] private EmergencyShuttleSystem _emergencyShuttle = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodBrotherEscapeConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(Entity<BloodBrotherEscapeConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        if (!TryComp<BloodBrotherTeamObjectiveComponent>(ent.Owner, out var teamObjective) ||
            teamObjective.Team == null ||
            teamObjective.Team.Minds.Count == 0)
        {
            args.Progress = 0f;
            return;
        }

        var team = teamObjective.Team;
        var progress = 0f;
        foreach (var mindId in team.Minds)
        {
            if (!TryComp<MindComponent>(mindId, out var mind))
                continue;

            progress += GetMemberProgress(mind);
        }

        args.Progress = progress / team.Minds.Count;
    }

    private float GetMemberProgress(MindComponent mind)
    {
        if (mind.OwnedEntity == null || _mind.IsCharacterDeadIc(mind))
            return 0f;

        // Being restrained still counts as partially escaping, same as the traitor objective.
        if (TryComp<CuffableComponent>(mind.OwnedEntity, out var cuffed) && cuffed.CuffedHandCount > 0)
            return _emergencyShuttle.IsTargetEscaping(mind.OwnedEntity.Value) ? 0.5f : 0f;

        return _emergencyShuttle.IsTargetEscaping(mind.OwnedEntity.Value) ? 1f : 0f;
    }
}
