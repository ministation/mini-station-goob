// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.BloodBrother;
using Content.Shared.Antag;
using Content.Shared.Ghost;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Client.BloodBrother;

/// <summary>
/// Shows the blood brother faction icon to the members of the same conspiracy,
/// to ghosts and to admins with antag icons enabled.
/// </summary>
public sealed partial class BloodBrotherIconSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodBrotherComponent, GetStatusIconsEvent>(OnGetStatusIcons);
        SubscribeLocalEvent<BloodBrotherComponent, CanDisplayStatusIconsEvent>(OnCanDisplayStatusIcons);
    }

    private void OnGetStatusIcons(Entity<BloodBrotherComponent> ent, ref GetStatusIconsEvent args)
    {
        var canEv = new CanDisplayStatusIconsEvent(_player.LocalSession?.AttachedEntity);
        RaiseLocalEvent(ent, ref canEv);

        if (canEv.Cancelled || !_prototype.TryIndex(ent.Comp.StatusIcon, out var icon))
            return;

        args.StatusIcons.Add(icon);
    }

    private void OnCanDisplayStatusIcons(Entity<BloodBrotherComponent> ent, ref CanDisplayStatusIconsEvent args)
    {
        if (!CanDisplayIcon(args.User, ent.Comp))
            args.Cancelled = true;
    }

    private bool CanDisplayIcon(EntityUid? uid, BloodBrotherComponent brother)
    {
        if (uid is not { } user)
            return false;

        if (HasComp<ShowAntagIconsComponent>(user))
            return true;

        // The heart is only shown to the brothers of the same conspiracy.
        if (TryComp<BloodBrotherComponent>(user, out var viewer))
            return viewer.TeamId == brother.TeamId;

        return brother.IconVisibleToGhost && HasComp<GhostComponent>(user);
    }
}
