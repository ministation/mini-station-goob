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
public sealed class TypanWarAudioSystem : EntitySystem
{
    [Dependency] private readonly ContentAudioSystem _audioContent = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private bool _eventMusicEnabled = true;
    private bool _suppressAmbient;
    private readonly List<EntityUid> _warMusicStreams = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayAmbientMusicEvent>(OnPlayAmbientMusic);
        SubscribeLocalEvent<AudioComponent, ComponentStartup>(OnAudioStartup);
        SubscribeLocalEvent<AudioComponent, ComponentShutdown>(OnAudioShutdown);
        SubscribeNetworkEvent<TypanWarStatusEvent>(OnWarStatus);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        Subs.CVar(_cfg, CCVars.EventMusicEnabled, OnEventMusicChanged, true);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _suppressAmbient = false;
        _warMusicStreams.Clear();
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

    private void OnAudioStartup(EntityUid uid, AudioComponent comp, ComponentStartup args)
    {
        if (!TypanWarSounds.IsBackgroundMusicTrack(comp.FileName))
            return;

        if (!_eventMusicEnabled)
        {
            _audio.Stop(uid);
            return;
        }

        _warMusicStreams.Add(uid);
    }

    private void OnAudioShutdown(EntityUid uid, AudioComponent comp, ComponentShutdown args)
    {
        _warMusicStreams.Remove(uid);
    }

    private void OnEventMusicChanged(bool enabled)
    {
        _eventMusicEnabled = enabled;

        if (enabled)
            return;

        foreach (var stream in _warMusicStreams)
            _audio.Stop(stream);

        _warMusicStreams.Clear();
    }
}
