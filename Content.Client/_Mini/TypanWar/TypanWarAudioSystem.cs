// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Client.Audio;
using Content.Shared._Mini.TypanWar;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;

namespace Content.Client._Mini.TypanWar;

/// <summary>
/// Suppresses round ambient music during Typan station war and respects <see cref="CCVars.EventMusicEnabled"/> for war BGM.
/// </summary>
/// <remarks>
/// Cannot subscribe to <see cref="AudioComponent"/> <c>ComponentStartup</c>/<c>ComponentShutdown</c> —
/// those directed component events are exclusive to <c>Robust.Client.Audio.AudioSystem</c>.
/// </remarks>
public sealed class TypanWarAudioSystem : EntitySystem
{
    [Dependency] private readonly ContentAudioSystem _audioContent = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private bool _eventMusicEnabled = true;
    private bool _suppressAmbient;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayAmbientMusicEvent>(OnPlayAmbientMusic);
        SubscribeNetworkEvent<TypanWarStatusEvent>(OnWarStatus);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        Subs.CVar(_cfg, CCVars.EventMusicEnabled, OnEventMusicChanged, true);
    }

    public override void Update(float frameTime)
    {
        // Stop war BGM the server spawned while event music is off (only relevant mid-war).
        if (_eventMusicEnabled || !_suppressAmbient)
            return;

        StopWarMusicStreams();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _suppressAmbient = false;
    }

    private void OnWarStatus(TypanWarStatusEvent ev)
    {
        _suppressAmbient = ev.Phase != TypanWarPhase.Inactive;

        if (_suppressAmbient)
            _audioContent.DisableAmbientMusic();
    }

    private void OnPlayAmbientMusic(ref PlayAmbientMusicEvent ev)
    {
        if (ev.Cancelled || !_suppressAmbient)
            return;

        ev.Cancelled = true;
    }

    private void OnEventMusicChanged(bool enabled)
    {
        _eventMusicEnabled = enabled;

        if (!enabled)
            StopWarMusicStreams();
    }

    private void StopWarMusicStreams()
    {
        var query = EntityQueryEnumerator<AudioComponent>();
        while (query.MoveNext(out var uid, out var audio))
        {
            if (!TypanWarSounds.IsBackgroundMusicTrack(audio.FileName))
                continue;

            _audio.Stop(uid);
        }
    }
}
