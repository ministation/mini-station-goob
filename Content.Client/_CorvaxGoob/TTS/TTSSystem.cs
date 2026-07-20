using Content.Shared.Chat;
using Content.Shared.Ghost;
using Content.Shared._CorvaxGoob.CCCVars;
using Content.Shared._CorvaxGoob.TTS;
using Content.Shared._Mini.MiniCCVars;
using Robust.Client.Audio;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;
using Content.Shared._CorvaxGoob;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio.Components;
using Robust.Shared.Player;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Client._CorvaxGoob.TTS;

/// <summary>
/// Plays TTS audio in world
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed partial class TTSSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IResourceManager _res = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private ISawmill _sawmill = default!;
    private static MemoryContentRoot _contentRoot = new();
    private static readonly ResPath Prefix = ResPath.Root / "TTS";

    private static readonly float MinimalPitchToPlay = 0.3f;

    private static bool _contentRootAdded;

    /// <summary>
    /// Reducing the volume of the TTS when whispering. Will be converted to logarithm.
    /// </summary>
    private const float WhisperFade = 2f;

    /// <summary>
    /// The volume at which the TTS sound will not be heard.
    /// </summary>
    private const float MinimalVolume = -5f;
    private static readonly SoundPathSpecifier RadioStaticSound = new("/Audio/_Mini/TTS/radio.ogg");
    private const float RadioStaticFade = 7f;
    private const float RadioTtsFade = 2f;
    private const int RadioLeadInMs = 160;

    private float _volume = 0.0f;
    private bool _radioGhostEnabled = true;
    private int _fileIdx = 0;

    public override void Initialize()
    {
        if (!_contentRootAdded)
        {
            _contentRootAdded = true;
            _res.AddRoot(Prefix, _contentRoot);
        }

        _sawmill = Logger.GetSawmill("tts");
        _cfg.OnValueChanged(CCCVars.TTSVolume, OnTtsVolumeChanged, true);
        _cfg.OnValueChanged(CCCVars.AnnouncementsSound, OnAnnouncementsVolumeChanged, true);
        _cfg.OnValueChanged(MiniCCVars.TTSRadioGhostEnabled, OnRadioGhostEnabledChanged, true);
        SubscribeNetworkEvent<PlayTTSEvent>(OnPlayTTS);
        SubscribeNetworkEvent<TTSAnnouncedEvent>(OnAnnounced);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(CCCVars.TTSVolume, OnTtsVolumeChanged);
        _cfg.UnsubValueChanged(CCCVars.AnnouncementsSound, OnAnnouncementsVolumeChanged);
        _cfg.UnsubValueChanged(MiniCCVars.TTSRadioGhostEnabled, OnRadioGhostEnabledChanged);
    }

    public void RequestPreviewTTS(string voiceId)
    {
        RaiseNetworkEvent(new RequestPreviewTTSEvent(voiceId));
    }

    private void OnTtsVolumeChanged(float volume)
    {
        _volume = volume;
    }

    private void OnRadioGhostEnabledChanged(bool enabled)
    {
        _radioGhostEnabled = enabled;
    }

    private void OnPlayTTS(PlayTTSEvent ev)
    {
        _sawmill.Verbose($"Play TTS audio {ev.Data.Length} bytes from {ev.SourceUid} entity");

        if (ev.IsRadio)
        {
            if (!_radioGhostEnabled && IsLocalPlayerGhost())
                return;

            PlayRadioStatic();
            // Capture data now — delayed callback must not touch a disposed system / IoC.
            var radioEv = ev;
            Timer.Spawn(TimeSpan.FromMilliseconds(RadioLeadInMs), () =>
            {
                try
                {
                    PlayTtsAudio(radioEv);
                }
                catch (Exception e)
                {
                    _sawmill.Error($"Failed to play delayed radio TTS: {e}");
                }
            });
            return;
        }

        PlayTtsAudio(ev);
    }

    private bool IsLocalPlayerGhost()
    {
        return _playerManager.LocalEntity is { } local
               && HasComp<GhostComponent>(local);
    }

    private void PlayTtsAudio(PlayTTSEvent ev)
    {
        var filePath = new ResPath($"{_fileIdx++}.ogg");
        _contentRoot.AddOrUpdateFile(filePath, ev.Data);

        var audioResource = new AudioResource();
        audioResource.Load(IoCManager.Instance!, Prefix / filePath);

        var audioParams = AudioParams.Default
            .WithVolume(AdjustVolume(ev.IsWhisper, ev.IsRadio))
            .WithMaxDistance(AdjustDistance(ev.IsWhisper));

        if (ev.Pitch.HasValue)
            audioParams = audioParams.WithPitchScale(ev.Pitch.Value);

        var soundSpecifier = new ResolvedPathSpecifier(Prefix / filePath);

        (EntityUid Entity, AudioComponent Component)? audio;

        if (ev.SourceUid != null)
        {
            var sourceUid = GetEntity(ev.SourceUid.Value);

            if (!Exists(sourceUid) || Deleted(sourceUid))
            {
                _contentRoot.RemoveFile(filePath);
                return;
            }

            audio = _audio.PlayEntity(audioResource.AudioStream, sourceUid, soundSpecifier, audioParams);
        }
        else
        {
            audio = _audio.PlayGlobal(audioResource.AudioStream, soundSpecifier, audioParams);
        }

        // Edits TimedDespawn time property for correctly pitch appling
        if (audio.HasValue
            && ev.Pitch.HasValue
            && ev.Pitch.Value != 1
            && ev.Pitch.Value > MinimalPitchToPlay
            && TryComp<TimedDespawnComponent>(audio.Value.Entity, out var timedDespawn))
        {
            timedDespawn.Lifetime = timedDespawn.Lifetime / ev.Pitch.Value;
        }

        _contentRoot.RemoveFile(filePath);
    }

    private void PlayRadioStatic()
    {
        var staticVolume = AdjustVolume(true) - SharedAudioSystem.GainToVolume(RadioStaticFade);
        var noiseParams = AudioParams.Default.WithVolume(staticVolume);
        _audio.PlayGlobal(RadioStaticSound, Filter.Local(), false, noiseParams);
    }

    private float AdjustVolume(bool isWhisper, bool isRadio = false)
    {
        var volume = MinimalVolume + SharedAudioSystem.GainToVolume(_volume);

        if (isWhisper)
        {
            volume -= SharedAudioSystem.GainToVolume(WhisperFade);
        }

        if (isRadio)
        {
            volume -= SharedAudioSystem.GainToVolume(RadioTtsFade);
        }

        return volume;
    }

    private float AdjustDistance(bool isWhisper)
    {
        return isWhisper ? SharedChatSystem.WhisperMuffledRange : SharedChatSystem.VoiceRange;
    }
}
