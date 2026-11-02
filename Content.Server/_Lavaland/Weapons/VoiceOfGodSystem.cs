// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Lavaland.Weapons.Voice;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Lavaland.Weapons;

/// <summary>
/// Voice of God: the target is forced to its knees by a word of command.
/// </summary>
public sealed class VoiceOfGodSystem : EntitySystem
{
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VocalCordsComponent, VoiceOfGodActionEvent>(OnVoiceOfGod);
    }

    private void OnVoiceOfGod(Entity<VocalCordsComponent> ent, ref VoiceOfGodActionEvent args)
    {
        if (args.Handled || !HasComp<MobStateComponent>(args.Target))
            return;
        args.Handled = true;

        _stun.TryKnockdown(args.Target, TimeSpan.FromSeconds(3), true);
        _stun.TryAddParalyzeDuration(args.Target, TimeSpan.FromSeconds(1.5f));

        _audio.PlayPvs("/Audio/_Lavaland/Effects/invoke_general.ogg", ent, AudioParams.Default.WithVolume(-2f));
        _popup.PopupEntity(Loc.GetString("voice-of-god-hit", ("user", args.Performer)), args.Target, args.Target, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("voice-of-god-self", ("target", args.Target)), args.Performer, args.Performer);
    }
}
