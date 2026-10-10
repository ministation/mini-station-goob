// SPDX-License-Identifier: AGPL-3.0-or-later

// Mini: restored from Goobstation upstream (was deleted by CorvaxGoob) and extended with the
// transmit side (microphone capture + Opus + Lidgren UDP), the player opt-in toggle and
// proximity attenuation on playback.

using System.Net;
using System.Numerics;
using Concentus;
using Concentus.Enums;
using Concentus.Structs;
using Content.Goobstation.Common.CCVar;
using Content.Goobstation.Shared.VoiceChat;
using Lidgren.Network;
using NAudio.Wave;
using Robust.Client.Audio;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Goobstation.Client.Voice;

/// <summary>
/// Client-side manager for voice chat functionality.
/// Handles network messages, proximity playback, and microphone transmission.
/// </summary>
public sealed class VoiceChatClientManager : IVoiceChatManager, IEntityEventSubscriber
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IAudioManager _audioManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    private AudioSystem? _audioSystem = default!;

    private ISawmill _sawmill = default!;
    private IGameTiming _gameTiming = default!;
    private readonly Dictionary<EntityUid, VoiceStreamManager> _activeStreams = new();

    private const int SampleRate = 48000;
    private const int Channels = 1; // Mono
    private const int FrameSizeMs = 20;
    private const int FrameSamplesPerChannel = SampleRate / 1000 * FrameSizeMs; // 960
    private const int BytesPerSample = 2; // 16-bit audio

    /// <summary>Distance (meters) at which voices play at full volume.</summary>
    private const float FullVolumeDistance = 2f;
    /// <summary>Distance (meters) at which voices fade out completely.</summary>
    private const float MaxVoiceDistance = 14f;

    private int _sampleRate = SampleRate;
    private float _volume = 0.5f;
    private bool _hearSelf = false;
    private bool _clientEnabled = false;

    // --- Transmit (microphone) state ---
    private NetClient? _voiceClient;
    private NetConnection? _serverConn;
    private OpusEncoder? _encoder;
    private WaveInEvent? _waveIn;
    private bool _transmitting;
    private bool _micAvailable;
    private TimeSpan _nextConnectAttempt = TimeSpan.Zero;
    private readonly List<NetIncomingMessage> _pumpBuffer = new();

    public void Initalize()
    {
        IoCManager.InjectDependencies(this);
        _gameTiming = IoCManager.Resolve<IGameTiming>();
        _sawmill = Logger.GetSawmill("voiceclient");

        _volume = _cfg.GetCVar(GoobCVars.VoiceChatVolume);
        _hearSelf = _cfg.GetCVar(GoobCVars.VoiceChatHearSelf);
        _clientEnabled = _cfg.GetCVar(GoobCVars.VoiceChatClientEnabled);
        _sawmill.Info($"VoiceChatClientManager initialized with volume: {_volume}, hear_self: {_hearSelf}, enabled: {_clientEnabled}");
        _cfg.OnValueChanged(GoobCVars.VoiceChatVolume, OnVolumeChanged, true);
        _cfg.OnValueChanged(GoobCVars.VoiceChatHearSelf, OnHearSelfChanged, true);
        _cfg.OnValueChanged(GoobCVars.VoiceChatClientEnabled, OnClientEnabledChanged);

        _netManager.RegisterNetMessage<MsgVoiceChat>(OnVoiceMessageReceived);
        _netManager.RegisterNetMessage<MsgVoiceChatOptIn>(); // Mini: opt-in notification

        _netManager.Connected += OnMainConnected;
        _netManager.Disconnect += OnMainDisconnected;

        // Mini: the voice UDP server only approves in-game sessions, so a connection
        // attempted in the lobby is rejected — retry once we are attached to an entity.
        _entityManager.EventBus.SubscribeEvent<LocalPlayerAttachedEvent>(EventSource.Local, this, OnLocalPlayerAttached);

        if (_clientEnabled && _netManager.IsConnected)
            OnOptInChanged();
    }

    /// <summary>
    /// Handle volume changes from CVars.
    /// </summary>
    private void OnVolumeChanged(float volume)
    {
        _volume = volume;

        foreach (var stream in _activeStreams.Values)
        {
            stream.SetVolume(_volume);
        }

        _sawmill.Debug($"Voice chat volume changed to {volume}");
    }

    /// <summary>
    /// Handle hear_self changes from CVars.
    /// </summary>
    private void OnHearSelfChanged(bool hearSelf)
    {
        _hearSelf = hearSelf;
        _sawmill.Debug($"Voice chat hear_self changed to {hearSelf}");
    }

    private void OnClientEnabledChanged(bool enabled)
    {
        _clientEnabled = enabled;
        _sawmill.Info($"Voice chat client enabled: {enabled}");

        if (_netManager.IsConnected)
            OnOptInChanged();
        else
            ShutdownTransmit();
    }

    private void OnMainConnected(object? sender, NetChannelArgs e)
    {
        if (_clientEnabled)
            OnOptInChanged();
    }

    private void OnMainDisconnected(object? sender, NetDisconnectedArgs e)
    {
        ShutdownTransmit();
    }

    private void OnLocalPlayerAttached(LocalPlayerAttachedEvent ev)
    {
        // Mini: drop any dead voice peer (e.g. one rejected while we were in the lobby)
        // so the retry timer below connects for the in-game session.
        if (!_clientEnabled || !_netManager.IsConnected)
            return;

        ShutdownTransmit();
        _nextConnectAttempt = TimeSpan.Zero;
    }

    /// <summary>
    /// Sync our opt-in with the server (TTS suppression + voice relay permission).
    /// </summary>
    private void OnOptInChanged()
    {
        var msg = new MsgVoiceChatOptIn { OptedIn = _clientEnabled };
        _netManager.ClientSendMessage(msg);

        if (_clientEnabled)
            StartTransmit();
        else
            ShutdownTransmit();
    }

    /// <summary>
    /// Handle incoming voice chat network messages.
    /// </summary>
    private void OnVoiceMessageReceived(MsgVoiceChat message)
    {
        // Mini: the player disabled voice chat entirely — play nothing.
        if (!_clientEnabled)
            return;

        if (message.PcmData == null || message.SourceEntity == null)
        {
            _sawmill.Warning("Received invalid voice chat message (null data or source)");
            return;
        }

        var sourceUid = _entityManager.GetEntity(message.SourceEntity.Value);
        if (!sourceUid.IsValid())
        {
            _sawmill.Warning($"Received voice chat message for invalid entity: {message.SourceEntity}");
            return;
        }

        AddPacket(sourceUid, message.PcmData);
    }

    /// <inheritdoc/>
    public void AddPacket(EntityUid sourceEntity, byte[] pcmData)
    {
        _audioSystem ??= _entityManager.System<AudioSystem>();

        var localPlayer = _playerManager.LocalEntity;
        if (localPlayer == sourceEntity && !_hearSelf)
        {
            _sawmill.Debug($"[VOICE DEBUG] Filtering out audio from own entity {sourceEntity} (hear_self disabled)");
            return;
        }

        // Mini: proximity attenuation — full volume up close, fading out with distance so that
        // voice is heard "as if standing nearby", not PVS-wide.
        var attenuation = 1f;
        if (localPlayer != null)
        {
            var dist = TransformDistance(localPlayer.Value, sourceEntity);
            attenuation = 1f - Math.Clamp((dist - FullVolumeDistance) / (MaxVoiceDistance - FullVolumeDistance), 0f, 1f);
            if (attenuation <= 0.02f)
                return;
        }

        if (!TryGetStreamManager(sourceEntity, out var streamManager))
        {
            _sawmill.Info($"[VOICE DEBUG] Creating new voice stream for entity {sourceEntity}");
            streamManager = new VoiceStreamManager(_audioManager, _audioSystem, sourceEntity, _sampleRate);
            streamManager.SetVolume(_volume * attenuation);
            AddStreamManager(sourceEntity, streamManager);
        }
        else
        {
            streamManager.SetVolume(_volume * attenuation);
        }

        _sawmill.Debug($"[VOICE DEBUG] Adding packet to stream for entity {sourceEntity} (stream count: {_activeStreams.Count})");
        streamManager.AddPacket(pcmData);
    }

    private float TransformDistance(EntityUid a, EntityUid b)
    {
        var xformA = _entityManager.GetComponent<Robust.Shared.GameObjects.TransformComponent>(a);
        var xformB = _entityManager.GetComponent<Robust.Shared.GameObjects.TransformComponent>(b);
        if (xformA.MapID != xformB.MapID)
            return float.MaxValue;
        return (xformA.WorldPosition - xformB.WorldPosition).Length();
    }

    /// <summary>
    /// Mini: called by EntryPoint while the push-to-talk key is held/released.
    /// </summary>
    public void SetTransmitting(bool transmitting)
    {
        if (_transmitting == transmitting)
            return;

        if (transmitting && !_clientEnabled)
            return;

        _transmitting = transmitting;
        _sawmill.Debug($"Voice transmit: {transmitting}");
    }

    /// <inheritdoc/>
    public bool TryGetStreamManager(EntityUid sourceEntity, out VoiceStreamManager streamManager)
    {
        if (_activeStreams.TryGetValue(sourceEntity, out var manager))
        {
            streamManager = manager;
            return true;
        }

        streamManager = null!;
        return false;
    }

    /// <inheritdoc/>
    public void AddStreamManager(EntityUid sourceEntity, VoiceStreamManager streamManager)
    {
        _activeStreams[sourceEntity] = streamManager;
    }

    public void Shutdown()
    {
        _cfg.UnsubValueChanged(GoobCVars.VoiceChatVolume, OnVolumeChanged);
        _cfg.UnsubValueChanged(GoobCVars.VoiceChatHearSelf, OnHearSelfChanged);
        _cfg.UnsubValueChanged(GoobCVars.VoiceChatClientEnabled, OnClientEnabledChanged);
        _netManager.Connected -= OnMainConnected;
        _netManager.Disconnect -= OnMainDisconnected;

        ShutdownTransmit();

        _entityManager.EventBus.UnsubscribeEvent<LocalPlayerAttachedEvent>(EventSource.Local, this);

        foreach (var stream in _activeStreams.Values)
        {
            stream.Dispose();
        }
        _activeStreams.Clear();

        _sawmill.Info("VoiceChatClientManager has been shut down");
    }

    public void Update()
    {
        // Mini: Lidgren only advances NetConnection.Status when the app dequeues
        // StatusChanged messages — without pumping, the transmit gate never sees Connected.
        PumpVoiceClient();

        // Mini: the voice relay approves only in-game sessions (matched by IP), so
        // (re)connect only while attached to an entity; recycle rejected/dead peers.
        if (_clientEnabled
            && _netManager.IsConnected
            && _playerManager.LocalEntity != null
            && _gameTiming.RealTime > _nextConnectAttempt)
        {
            _nextConnectAttempt = _gameTiming.RealTime + TimeSpan.FromSeconds(5);

            if (_voiceClient == null)
            {
                StartTransmit();
            }
            else if (_serverConn == null
                || _serverConn.Status == NetConnectionStatus.Disconnected)
            {
                ShutdownTransmit();
                StartTransmit();
            }
        }

        List<EntityUid>? toRemove = null;

        foreach (var (uid, stream) in _activeStreams)
        {
            stream.Update();

            if (!_entityManager.EntityExists(uid))
            {
                toRemove ??= new List<EntityUid>();
                toRemove.Add(uid);
            }
        }

        if (toRemove != null)
        {
            foreach (var uid in toRemove)
            {
                if (_activeStreams.TryGetValue(uid, out var stream))
                {
                    _sawmill.Debug($"Removing voice stream for deleted entity {uid}");
                    stream.Dispose();
                    _activeStreams.Remove(uid);
                }
            }
        }
    }

    // --- Transmit implementation (Mini) ---

    private void PumpVoiceClient()
    {
        if (_voiceClient == null)
            return;

        _voiceClient.ReadMessages(_pumpBuffer);
        for (var i = 0; i < _pumpBuffer.Count; i++)
        {
            _voiceClient.Recycle(_pumpBuffer[i]);
        }
        _pumpBuffer.Clear();
    }

    private void StartTransmit()
    {
        if (_voiceClient != null)
            return;

        try
        {
            // Mini: the voice UDP server approves connections by matching the game session IP,
            // so it can only be established once we are connected to the game server.
            var session = _playerManager.LocalSession;
            if (session == null)
                return; // retried from Update()

            var host = session.Channel.RemoteEndPoint.Address;
            var port = _cfg.GetCVar(GoobCVars.VoiceChatPort);

            var config = new Lidgren.Network.NetPeerConfiguration("SS14VoiceChat");
            _voiceClient = new NetClient(config);
            _voiceClient.Start();
            _serverConn = _voiceClient.Connect(new IPEndPoint(host, port));

            _encoder = (OpusEncoder) OpusCodecFactory.CreateEncoder(SampleRate, Channels);
            _encoder.Bitrate = 32000;

            StartMic();
            _sawmill.Info($"Voice transmit connecting to {host}:{port}");
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to start voice transmit: {e.Message}");
            ShutdownTransmit();
        }
    }

    private void StartMic()
    {
        if (_micAvailable || _waveIn != null)
            return;

        try
        {
            _waveIn = new WaveInEvent
            {
                DeviceNumber = 0,
                WaveFormat = new WaveFormat(SampleRate, 16, Channels),
                BufferMilliseconds = FrameSizeMs
            };
            _waveIn.DataAvailable += OnMicDataAvailable;
            _waveIn.RecordingStopped += OnMicRecordingStopped;
            _waveIn.StartRecording();
            _micAvailable = true;
            _sawmill.Info("Microphone capture started");
        }
        catch (Exception e)
        {
            _sawmill.Error($"Microphone capture unavailable: {e.Message}");
            _micAvailable = false;
            if (_waveIn != null)
            {
                _waveIn.Dispose();
                _waveIn = null;
            }
        }
    }

    private void OnMicDataAvailable(object? sender, WaveInEventArgs e)
    {
        // Only capture while the push-to-talk key is held and everything is connected.
        if (!_transmitting
            || !_clientEnabled
            || _serverConn == null
            || _serverConn.Status != NetConnectionStatus.Connected
            || _encoder == null
            || e.BytesRecorded <= 0)
            return;

        try
        {
            var samplesIn = e.BytesRecorded / (BytesPerSample * Channels);
            var pcm = new short[samplesIn];
            Buffer.BlockCopy(e.Buffer, 0, pcm, 0, samplesIn * BytesPerSample);

            var encoded = new byte[samplesIn * 2];
            var encodedBytes = _encoder.Encode(pcm, 0, samplesIn, encoded, 0, encoded.Length);
            if (encodedBytes <= 0)
                return;

            var outMsg = _voiceClient!.CreateMessage(encodedBytes);
            outMsg.Write(encoded, 0, encodedBytes);
            _voiceClient.SendMessage(outMsg, Lidgren.Network.NetDeliveryMethod.Unreliable, 0);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Error encoding/sending voice: {ex.Message}");
        }
    }

    private void OnMicRecordingStopped(object? sender, EventArgs e)
    {
        if (_clientEnabled && _waveIn != null)
            _sawmill.Warning("Microphone recording stopped unexpectedly");
    }

    private void ShutdownTransmit()
    {
        _transmitting = false;

        try
        {
            _waveIn?.StopRecording();
        }
        catch
        {
            // ignore
        }

        _waveIn?.Dispose();
        _waveIn = null;
        _micAvailable = false;

        try
        {
            _voiceClient?.Disconnect("Voice chat disabled.");
        }
        catch
        {
            // ignore
        }

        _voiceClient = null;
        _serverConn = null;
    }
}
