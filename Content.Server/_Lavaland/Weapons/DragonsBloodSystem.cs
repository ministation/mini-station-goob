// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Polymorph.Systems;
using Content.Shared._Lavaland.Weapons.Lava;
using Content.Shared.Actions;
using Content.Shared.Atmos.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Lavaland.Weapons;

/// <summary>
/// Drinking dragon's blood grants one random permanent effect.
/// </summary>
public sealed class DragonsBloodSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private readonly EntProtoId FireBreathAction = "ActionFireball";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DragonsBloodComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<DragonsBloodComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        var user = args.User;
        _audio.PlayPvs("/Audio/Items/drink.ogg", ent, AudioParams.Default.WithVolume(-2f));

        switch (Random.Shared.Next(3))
        {
            case 0:
            {
                // Fire breath: learn the fireball spell.
                _actions.AddAction(user, FireBreathAction);
                _popup.PopupEntity(Loc.GetString("dragons-blood-firebreath"), user, user, PopupType.LargeCaution);
                break;
            }
            case 1:
            {
                // Permanent lava immunity: no fire, heat damage is nullified.
                if (!HasComp<LavaImmunityComponent>(user))
                {
                    EnsureComp<LavaImmunityComponent>(user);
                    RemComp<FlammableComponent>(user);
                }
                _popup.PopupEntity(Loc.GetString("dragons-blood-immunity"), user, user, PopupType.LargeCaution);
                break;
            }
            default:
            {
                // Cursed outcome: the blood remakes you into a skeleton.
                _popup.PopupEntity(Loc.GetString("dragons-blood-skeleton"), user, user, PopupType.LargeCaution);
                _polymorph.PolymorphEntity(user, ent.Comp.SkeletonPolymorph);
                break;
            }
        }

        _popup.PopupEntity(Loc.GetString("dragons-blood-drunk-others", ("user", user)), ent);
        QueueDel(ent);
    }
}
