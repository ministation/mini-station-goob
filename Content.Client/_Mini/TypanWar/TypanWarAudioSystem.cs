// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Client.Audio;
using Content.Shared._Mini.MiniCCVars;
using Content.Shared._Mini.TypanWar;
using Content.Shared.GameTicking;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;

namespace Content.Client._Mini.TypanWar;

/// <summary>
/// Suppresses round ambient music while war BGM may play, and respects <see cref="MiniCCVars.WarMusicEnabled"/>.
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

    private bool _warMusicEnabled = true;
    private bool _warActive;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayAmbientMusicEvent>(OnPlayAmbientMusic);
        SubscribeNetworkEvent<TypanWarStatusEvent>(OnWarStatus);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        Subs.CVar(_cfg, MiniCCVars.WarMusicEnabled, OnWarMusicChanged, true);
    }

    public override void Update(float frameTime)
    {
        // Server-replicated war BGM keeps coming back unless we continuously stop it while disabled.
        if (_warMusicEnabled || !_warActive)
            return;

        StopWarMusicStreams();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _warActive = false;
    }

    private void OnWarStatus(TypanWarStatusEvent ev)
    {
        _warActive = ev.Phase != TypanWarPhase.Inactive;
        UpdateAmbientSuppression();
    }

    private void OnPlayAmbientMusic(ref PlayAmbientMusicEvent ev)
    {
        if (ev.Cancelled || !ShouldSuppressAmbient())
            return;

        ev.Cancelled = true;
    }

    private void OnWarMusicChanged(bool enabled)
    {
        _warMusicEnabled = enabled;

        if (!enabled)
            StopWarMusicStreams();

        UpdateAmbientSuppression();
    }

    /// <summary>
    /// Ambient is only muted while war is active AND war music is enabled (so BGM isn't layered).
    /// If the player turns war music off, normal ambient music may resume.
    /// </summary>
    private bool ShouldSuppressAmbient() => _warActive && _warMusicEnabled;

    private void UpdateAmbientSuppression()
    {
        if (ShouldSuppressAmbient())
            _audioContent.DisableAmbientMusic();
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
