// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared._Lavaland.Ashwalker;
using Content.Shared.Objectives.Components;

namespace Content.Server._Lavaland.Ashwalker;

/// <summary>
/// Ash walker protect objective: complete while the necropolis nest still stands.
/// </summary>
public sealed class NecropolisAliveConditionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NecropolisAliveConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(Entity<NecropolisAliveConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = ent.Comp.Nest is { } nest && !TerminatingOrDeleted(nest) ? 1f : 0f;
    }
}
