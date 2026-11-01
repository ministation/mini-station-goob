// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Administration.Systems;
using Content.Shared._Lavaland.Weapons.Voice;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Lavaland.Weapons;

/// <summary>
/// Revives a nearby corpse, or fully heals the user if nobody is dead around.
/// </summary>
public sealed class RenewalCrystalSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RenewalCrystalComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<RenewalCrystalComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        var comp = ent.Comp;
        var coords = Transform(args.User).Coordinates;

        EntityUid? revived = null;
        var dead = _lookup.GetEntitiesInRange<MobStateComponent>(coords, comp.Range);
        foreach (var mob in dead)
        {
            if (mob.Comp.CurrentState != MobState.Dead || mob.Owner == args.User)
                continue;

            revived = mob;
            break;
        }

        _audio.PlayPvs("/Audio/Effects/radiation.ogg", ent, AudioParams.Default.WithVolume(-2f));

        if (revived != null)
        {
            _rejuvenate.PerformRejuvenate(revived.Value);
            _popup.PopupEntity(Loc.GetString("renewal-crystal-revived", ("target", revived.Value)), ent, args.User);
        }
        else
        {
            _rejuvenate.PerformRejuvenate(args.User);
            _popup.PopupEntity(Loc.GetString("renewal-crystal-healed"), ent, args.User);
        }

        QueueDel(ent);
    }
}
