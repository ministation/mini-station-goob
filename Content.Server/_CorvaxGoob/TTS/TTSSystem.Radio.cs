using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Server.Chat.Systems;
using Content.Server.Radio;
using Content.Shared._CorvaxGoob.TTS;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._CorvaxGoob.TTS;

// ReSharper disable once InconsistentNaming
public sealed partial class TTSSystem
{
    private readonly Dictionary<RadioTtsRequestKey, RadioTtsRequestState> _radioTtsRequests = new();
    private readonly List<RadioTtsRequestKey> _radioTtsExpiredKeys = new();

    private void InitializeRadioTTS()
    {
        // Living players: radio arrives on the headset and is relayed to the wearer.
        SubscribeLocalEvent<TTSComponent, HeadsetRadioReceiveRelayEvent>(OnHeadsetRadioReceive);
        // Ghosts / intrinsic receivers (ActiveRadio on the mob itself): event is raised on the entity.
        SubscribeLocalEvent<TTSComponent, RadioReceiveEvent>(OnDirectRadioReceive);
    }

    private void CleanupRadioTTS()
    {
        _radioTtsRequests.Clear();
        _radioTtsExpiredKeys.Clear();
    }

    private void OnHeadsetRadioReceive(EntityUid uid, TTSComponent component, ref HeadsetRadioReceiveRelayEvent args)
    {
        _ = HandleRadioReceiveAsync(uid, args.RelayedEvent);
    }

    private void OnDirectRadioReceive(EntityUid uid, TTSComponent component, ref RadioReceiveEvent args)
    {
        _ = HandleRadioReceiveAsync(uid, args);
    }

    private async Task HandleRadioReceiveAsync(EntityUid uid, RadioReceiveEvent args)
    {
        if (!_isEnabled ||
            !args.Language.SpeechOverride.RequireSpeech ||
            !TryComp<ActorComponent>(uid, out var actor))
            return;

        if (IsInDirectSpeechRange(args.MessageSource, uid))
            return;

        if (!TryGetRadioVoice(args.MessageSource, out var speaker, out var pitch))
            return;

        var canUnderstand = _lang.CanUnderstand(uid, args.Language);
        var message = canUnderstand ? args.OriginalChatMsg.Message : args.LanguageObfuscatedChatMsg.Message;
        var radioText = GetRadioTtsText(message);
        if (string.IsNullOrWhiteSpace(radioText) || radioText.Length > MaxMessageChars)
            return;

        var currentTick = _timing.CurTick.Value;
        PruneRadioRequests(currentTick);

        var requestKey = new RadioTtsRequestKey(
            currentTick,
            args.MessageSource,
            args.Channel.ID,
            radioText,
            speaker,
            pitch);

        if (!_radioTtsRequests.TryGetValue(requestKey, out var requestState))
        {
            requestState = new RadioTtsRequestState(GenerateTTS(radioText, speaker), currentTick);
            _radioTtsRequests[requestKey] = requestState;
        }

        if (!requestState.DeliveredReceivers.Add(uid))
            return;

        var soundData = await requestState.SoundTask;
        if (soundData is null)
            return;

        RaiseNetworkEvent(new PlayTTSEvent(soundData, isWhisper: true, isRadio: true, pitch: pitch), actor.PlayerSession);
    }

    private bool TryGetRadioVoice(EntityUid source, out string speaker, out float? pitch)
    {
        speaker = string.Empty;
        pitch = null;

        if (!TryComp<TTSComponent>(source, out var tts))
            return false;

        var voiceId = tts.VoicePrototypeId;
        if (string.IsNullOrWhiteSpace(voiceId))
            return false;

        var voiceEv = new TransformSpeakerVoiceEvent(source, voiceId);
        RaiseLocalEvent(source, voiceEv);
        voiceId = voiceEv.VoiceId;

        if (!_prototypeManager.TryIndex<TTSVoicePrototype>(voiceId, out var protoVoice))
            return false;

        speaker = protoVoice.Speaker;
        pitch = tts.Pitch;
        return true;
    }

    private string GetRadioTtsText(string message)
    {
        try
        {
            return FormattedMessage.RemoveMarkupOrThrow(message);
        }
        catch
        {
            return message;
        }
    }

    private bool IsInDirectSpeechRange(EntityUid source, EntityUid receiver)
    {
        if (source == receiver)
            return true;

        var inPvs = false;
        foreach (var session in Filter.Pvs(source).Recipients)
        {
            if (session.AttachedEntity != receiver)
                continue;

            inPvs = true;
            break;
        }

        if (!inPvs)
            return false;

        var xformQuery = GetEntityQuery<TransformComponent>();
        if (!xformQuery.TryComp(source, out var sourceXform) ||
            !xformQuery.TryComp(receiver, out var receiverXform) ||
            sourceXform.MapID != receiverXform.MapID)
            return false;

        var sourcePos = _xforms.GetWorldPosition(sourceXform, xformQuery);
        var receiverPos = _xforms.GetWorldPosition(receiverXform, xformQuery);
        return (sourcePos - receiverPos).Length() <= ChatSystem.VoiceRange;
    }

    private void PruneRadioRequests(uint currentTick)
    {
        if (_radioTtsRequests.Count == 0)
            return;

        _radioTtsExpiredKeys.Clear();
        foreach (var (key, state) in _radioTtsRequests)
        {
            if (currentTick - state.CreatedTick <= 10)
                continue;

            _radioTtsExpiredKeys.Add(key);
        }

        foreach (var key in _radioTtsExpiredKeys)
        {
            _radioTtsRequests.Remove(key);
        }

        _radioTtsExpiredKeys.Clear();
    }

    private readonly record struct RadioTtsRequestKey(
        uint Tick,
        EntityUid Source,
        string Channel,
        string Message,
        string Speaker,
        float? Pitch);

    private sealed class RadioTtsRequestState(Task<byte[]?> soundTask, uint createdTick)
    {
        public Task<byte[]?> SoundTask { get; private set; } = soundTask;
        public uint CreatedTick { get; private set; } = createdTick;
        public HashSet<EntityUid> DeliveredReceivers { get; private set; } = new();
    }
}
