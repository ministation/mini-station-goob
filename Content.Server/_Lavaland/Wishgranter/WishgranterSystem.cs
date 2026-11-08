// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Server.Atmos.Components;
using Content.Server.Mind;
using Content.Shared._Lavaland.Weapons.Lava;
using Content.Shared._Lavaland.Wishgranter;
using Content.Shared._Trauma.Genetics.Mutations;
using Content.Shared.Administration.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._Lavaland.Wishgranter;

/// <summary>
/// TG wish granter cube: the second touch makes the wisher the avatar
/// of the Wish Granter, a solo antagonist with twisted "gifts".
/// </summary>
public sealed class WishgranterSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly MutationSystem _mutations = default!;

    private static readonly EntProtoId<MutationComponent>[] WishMutations =
    {
        "MutationHulk",
        "MutationPressureAdaptation",
        "MutationTelekinesis",
    };

    private const string HijackObjective = "WishgranterHijackObjective";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WishgranterComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<WishgranterSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
    }

    private void OnSpawnerMapInit(Entity<WishgranterSpawnerComponent> ent, ref MapInitEvent args)
    {
        // No self-delete: the prototype sweep requires every entity to stay deletable.
        Spawn("LavalandWishgranter", Transform(ent).Coordinates);
    }

    private void OnInteractHand(Entity<WishgranterComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || ent.Comp.Charges <= 0)
            return;

        // Only players with a mind may wish, like TG's human check.
        if (!_mind.TryGetMind(args.User, out var mindId, out var mind))
            return;

        if (HasComp<WishgranterAvatarComponent>(args.User))
            return;

        if (!ent.Comp.Insisting)
        {
            ent.Comp.Insisting = true;
            _popup.PopupEntity(Loc.GetString("wishgranter-first-touch"), args.User, args.User);
            args.Handled = true;
            return;
        }

        ent.Comp.Charges--;
        ent.Comp.Insisting = false;

        // The "gifts": fire and vacuum cannot touch the avatar anymore.
        EnsureComp<LavaImmunityComponent>(args.User);
        RemComp<BarotraumaComponent>(args.User);
        _rejuvenate.PerformRejuvenate(args.User);

        // TG wish powers: hulk, pressure adaptation and telekinesis. X-ray is not ported,
        // the engine has no see through walls vision, so only real mutations are granted.
        if (TryComp<MutatableComponent>(args.User, out var mutatable))
            _mutations.AddMutations((args.User, (MutatableComponent?) mutatable), WishMutations, user: args.User);

        _roles.MindAddRole(mindId, "MindRoleWishgranter");
        EnsureComp<WishgranterAvatarComponent>(args.User);

        // TG wish granter is a station hijacker, not just a buffed ghost role.
        _mind.TryAddObjective(mindId, mind, HijackObjective);

        _popup.PopupEntity(Loc.GetString("wishgranter-avatar-gained"), args.User, args.User);
        _popup.PopupCoordinates(Loc.GetString("wishgranter-spent"), Transform(ent).Coordinates);

        args.Handled = true;
    }
}
