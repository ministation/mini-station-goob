// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Objectives.Components;

namespace Content.Goobstation.Server.BloodBrother;

public sealed class BloodBrotherConvertConditionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodBrotherConvertConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(Entity<BloodBrotherConvertConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        if (!TryComp<BloodBrotherTeamObjectiveComponent>(ent.Owner, out var teamObjective) ||
            teamObjective.Team == null)
        {
            args.Progress = 0f;
            return;
        }

        args.Progress = teamObjective.Team.Minds.Count > 1 ? 1f : 0f;
    }
}
