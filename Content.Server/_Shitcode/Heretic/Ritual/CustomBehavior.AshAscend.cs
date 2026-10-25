// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Atmos;
using Content.Goobstation.Common.Body.Components;
using Content.Goobstation.Common.Flammability;
using Content.Goobstation.Common.Temperature.Components;
using Content.Server.Atmos.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Heretic.Prototypes;

namespace Content.Server.Heretic.Ritual;

public sealed partial class RitualAshAscendBehavior : RitualSacrificeBehavior
{
    private List<EntityUid> burningUids = new();

    // check for burning corpses
    public override bool Execute(RitualData args, out string? outstr)
    {
        // The behavior instance is shared by the prototype, so the previous attempt must not leak into this one.
        burningUids.Clear();

        if (!base.Execute(args, out outstr))
            return false;

        foreach (var uid in uids)
        {
            if (!args.EntityManager.TryGetComponent<FlammableComponent>(uid, out var flam))
                continue;

            if (flam.OnFire)
                burningUids.Add(uid);

            if (burningUids.Count >= Max)
                break;
        }

        if (burningUids.Count < Min)
        {
            outstr = Loc.GetString("heretic-ritual-fail-sacrifice-ash");
            return false;
        }

        outstr = null;
        return true;
    }

    public override void Finalize(RitualData args)
    {
        base.Finalize(args);

        // The Ashlord rite makes the heretic immune to fire, but those components are only applied by the
        // knowledge event, which is processed after the ascension event itself. Grant them here, the moment
        // the ritual completes, so a heretic who is already burning never takes damage from their own flames.
        // The flames stay on them as a purely visual effect, nothing is extinguished.
        args.EntityManager.EnsureComponent<FireImmunityComponent>(args.Performer);
        args.EntityManager.EnsureComponent<SpecialLowTempImmunityComponent>(args.Performer);
        args.EntityManager.EnsureComponent<SpecialHighTempImmunityComponent>(args.Performer);
        args.EntityManager.EnsureComponent<SpecialBreathingImmunityComponent>(args.Performer);
        args.EntityManager.EnsureComponent<SpecialPressureImmunityComponent>(args.Performer);
    }
}
