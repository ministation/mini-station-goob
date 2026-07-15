// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Xenobiology.Components;
using Content.Shared.Interaction.Events;
using Robust.Shared.GameObjects;

namespace Content.Goobstation.Shared.Xenobiology.Systems;

// This handles slime taming, likely to be expanded in the future.
public partial class XenobiologySystem
{
    private void SubscribeTaming()
    {
        SubscribeLocalEvent<SlimeComponent, InteractionSuccessEvent>(OnInteractionSuccess);
        SubscribeLocalEvent<EntityTerminatingEvent>(OnTamerTerminating);
    }

    private void OnTamerTerminating(ref EntityTerminatingEvent args)
    {
        if (_net.IsClient)
            return;

        var tamer = args.Entity;
        var query = EntityQueryEnumerator<SlimeComponent>();

        while (query.MoveNext(out var uid, out var slime))
        {
            if (slime.Tamer != tamer)
                continue;

            slime.Tamer = null;
            Dirty(uid, slime);
        }
    }

    private void OnInteractionSuccess(Entity<SlimeComponent> ent, ref InteractionSuccessEvent args)
    {
        if (_net.IsClient) return;

        if (ent.Comp.Tamer.HasValue)
        {
            _popup.PopupEntity(Loc.GetString("slime-interaction-tame-fail"), args.User, args.User);
            return;
        }

        var (slime, comp) = ent;
        var coords = Transform(slime).Coordinates;

        // Hearts VFX - Slime taming is seperate to core Pettable Component/System
        Spawn(ent.Comp.TameEffect, coords);
        comp.Tamer = args.User;
        RaiseLocalEvent(ent, new SlimeTamedEvent(args.User));

        _popup.PopupEntity(Loc.GetString("slime-interaction-tame"), args.User, args.User);

        Dirty(ent);
    }
}
