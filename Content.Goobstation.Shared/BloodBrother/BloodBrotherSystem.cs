// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Antag;
using Content.Shared.Ghost;
using Robust.Shared.GameStates;
using Robust.Shared.Player;

namespace Content.Goobstation.Shared.BloodBrother;

/// <summary>
/// Handles shared blood brother logic.
/// </summary>
public sealed class BloodBrotherSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodBrotherComponent, ComponentGetStateAttemptEvent>(OnGetStateAttempt);
        SubscribeLocalEvent<ShowAntagIconsComponent, ComponentInit>(OnShowAntagIconsInit);
    }

    /// <summary>
    /// Admin ghosts start seeing antag icons only after this component is added,
    /// so the existing brother components have to be sent to them again.
    /// </summary>
    private void OnShowAntagIconsInit(Entity<ShowAntagIconsComponent> ent, ref ComponentInit args)
    {
        var query = AllEntityQuery<BloodBrotherComponent>();
        while (query.MoveNext(out var uid, out var comp))
            Dirty(uid, comp);
    }

    /// <summary>
    /// Blood brothers are a conspiracy, so the component must not be sent to
    /// players that have no business knowing who they are. Only brothers of the
    /// same conspiracy, ghosts and admins with antag icons see it.
    /// </summary>
    private void OnGetStateAttempt(Entity<BloodBrotherComponent> ent, ref ComponentGetStateAttemptEvent args)
    {
        args.Cancelled = !CanGetState(ent, args.Player);
    }

    private bool CanGetState(Entity<BloodBrotherComponent> ent, ICommonSession? player)
    {
        // Replays and other cases without a session can see everything.
        if (player?.AttachedEntity is not { } uid)
            return true;

        if (HasComp<ShowAntagIconsComponent>(uid) || HasComp<GhostComponent>(uid))
            return true;

        // Only brothers of the same conspiracy may know about each other.
        return TryComp<BloodBrotherComponent>(uid, out var viewer) && viewer.TeamId == ent.Comp.TeamId;
    }
}
