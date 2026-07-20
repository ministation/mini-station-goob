// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;  // goob - intermap transmitters
using Content.Goobstation.Shared.Communications; // goob - intermap transmitters
using Content.Goobstation.Shared.Loudspeaker.Events; // goob - loudspeakers
using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Server._EinsteinEngines.Language;
using Content.Server.Power.Components;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared._EinsteinEngines.Language;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Speech;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Replays;
using Robust.Shared.Utility;
using Content.Shared.Access.Systems; // Goobstation
using Content.Shared.Chat.RadioIconsEvents; // Goobstation
// Goobstation
using Content.Shared.Whitelist;
using Content.Server.Inventory;
using Content.Shared.Inventory;
using Content.Server.PDA;
using Content.Shared.PDA;
using Content.Shared.IdentityManagement;
using Content.Shared.Access.Components;
using System.Text.RegularExpressions;
using Content.Shared.StatusIcon; // Goobstation
using Content.Goobstation.Shared.Radio; // Goobstation

namespace Content.Server.Radio.EntitySystems;

/// <summary>
///     This system handles intrinsic radios and the general process of converting radio messages into chat messages.
/// </summary>
public sealed partial class RadioSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly IReplayRecordingManager _replay = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly RadioJobIconSystem _radioIconSystem = default!; // Goobstation - radio icons
    [Dependency] private readonly LanguageSystem _language = default!; // Einstein Engines - Language
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!; // Goobstation - Whitelisted radio channels


    // set used to prevent radio feedback loops.
    private readonly HashSet<string> _messages = new();
    private static readonly Regex CategoryRegex = new Regex(@"^(.*?)\s*\(([^)]+)\)\s*$", RegexOptions.Compiled);
    private EntityQuery<TelecomExemptComponent> _exemptQuery;

    private readonly Dictionary<string, string[]> _departments = new Dictionary<string, string[]>
    {
        { "fcdf03", ["командование", "кэп", "капитан", "глава персонала"] },
        { "d98b71", ["юридический отдел", "магистрат", "юрист", "агент внутренних дел"] },
        { "ff4242", ["служба безопасности", "бриг", "варден", "смотритель", "инструктор", "детектив", "пилот сб", "бригмед", "кадет"] },
        { "57b8f0", ["медицинский отдел", "главный врач", "ведущий врач", "химик", "врач", "парамед", "коронер", "психолог", "интерн"] },
        { "c68cfa", ["научный отдел", "рнд", "нио", "научный руководитель", "ведущий учёный", "учёный", "робоёб", "лаборант", "анома"] },
        { "f2ac26", ["инженерный отдел", "инженерный", "старший инженер", "ведущий инженер", "атмосферный техник", "атмос", "инженер", "инженер стажёр"] },
        { "a46106", ["отдел снабжения", "карго", "каргонцы", "ведущий утилизатор", "ведущий утиль", "утиль", "утилизатор", "грузчик"] },
        { "6ca729", ["сервисный отдел", "сервис", "менеджер", "шеф", "повар", "ботаник", "бармен", "боксер", "уборщик", "библиотекарь", "священик", "святой отец", "зоотехник", "репортёр", "музыкант"] },
        { "2ed2fd", ["искусственный интеллект", "юнит", "борг"] },
        { "fb77f3", ["клуня", "клоун"] },
        { "d0d0d0", ["мим"] }
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IntrinsicRadioReceiverComponent, RadioReceiveEvent>(OnIntrinsicReceive);
        SubscribeLocalEvent<IntrinsicRadioTransmitterComponent, EntitySpokeEvent>(OnIntrinsicSpeak);
        SubscribeLocalEvent<IntrinsicRadioReceiverComponent, RadioReceiveAttemptEvent>(OnIntrinsicReceiveAttempt); // Goobstation

        _exemptQuery = GetEntityQuery<TelecomExemptComponent>();
    }

    private void OnIntrinsicSpeak(EntityUid uid, IntrinsicRadioTransmitterComponent component, EntitySpokeEvent args)
    {
        if (args.Channel != null
            && component.Channels.Contains(args.Channel.ID)
            && _whitelist.IsWhitelistPassOrNull(args.Channel.SendWhitelist, uid)) // Goobstation - Whitelisted radio channels
        {
            SendRadioMessage(uid, args.Message, args.Channel, uid, args.Language); // Einstein Engines - Language
            args.Channel = null; // prevent duplicate messages from other listeners.
        }
    }

    private void OnIntrinsicReceive(EntityUid uid, IntrinsicRadioReceiverComponent component, ref RadioReceiveEvent args)
    {
        if (TryComp(uid, out ActorComponent? actor))
        {
            if (actor.PlayerSession.Status != SessionStatus.InGame || !actor.PlayerSession.Channel.IsConnected)
                return;

            // Einstein Engines - Languages begin
            var listener = component.Owner;
            var msg = args.OriginalChatMsg;

            if (listener != null && !_language.CanUnderstand(listener, args.Language.ID))
                msg = args.LanguageObfuscatedChatMsg;

            _netMan.ServerSendMessage(new MsgChatMessage { Message = msg }, actor.PlayerSession.Channel);
            // Einstein Engines - Languages end
        }
    }

    // Goobstation - Whitelisted radio channels
    private void OnIntrinsicReceiveAttempt(EntityUid uid, IntrinsicRadioReceiverComponent component, ref RadioReceiveAttemptEvent args)
    {
        args.Cancelled = _whitelist.IsWhitelistFail(args.Channel.ReceiveWhitelist, uid);
    }

    /// <summary>
    /// Send radio message to all active radio listeners
    /// </summary>
    public void SendRadioMessage(
        EntityUid messageSource,
        string message,
        ProtoId<RadioChannelPrototype> channel,
        EntityUid radioSource,
        LanguagePrototype? language = null,
        bool escapeMarkup = true)
    {
        SendRadioMessage(messageSource, message, _prototype.Index(channel), radioSource, escapeMarkup: escapeMarkup, language: language); // Einstein Engines - Language
    }

    /// <summary>
    /// Send radio message to all active radio listeners
    /// </summary>
    /// <param name="messageSource">Entity that spoke the message</param>
    /// <param name="radioSource">Entity that picked up the message and will send it, e.g. headset</param>
    public void SendRadioMessage(
        EntityUid messageSource,
        string message,
        RadioChannelPrototype channel,
        EntityUid radioSource,
        LanguagePrototype? language = null,
        bool escapeMarkup = true)
    {
        // Einstein Engines - Language begin
        if (language == null)
            language = _language.GetLanguage(messageSource);

        if (!language.SpeechOverride.AllowRadio)
            return;
        // Einstein Engines - Language end

        // TODO if radios ever garble / modify messages, feedback-prevention needs to be handled better than this.
        if (!_messages.Add(message))
            return;

        var evt = new TransformSpeakerNameEvent(messageSource, MetaData(messageSource).EntityName);
        RaiseLocalEvent(messageSource, evt);

        // Goob - Job icons
        ProtoId<JobIconPrototype> jobIcon = "JobIconNoId";
        string? jobName = null;
        if (_radioIconSystem.TryGetJobIcon(messageSource, out var resolvedIcon, out var resolvedName))
        {
            var iconEvent = new TransformSpeakerJobIconEvent(messageSource, resolvedIcon!.Value, resolvedName);
            RaiseLocalEvent(messageSource, iconEvent);

            jobIcon = iconEvent.JobIcon;
            jobName = iconEvent.JobName;
        }

        if (!_prototype.HasIndex<JobIconPrototype>(jobIcon))
            jobIcon = "JobIconNoId";

        var name = evt.VoiceName;
        name = FormattedMessage.EscapeText(name);

        SpeechVerbPrototype speech;
        if (evt.SpeechVerb != null && _prototype.Resolve(evt.SpeechVerb, out var evntProto))
            speech = evntProto;
        else
            speech = _chat.GetSpeechVerb(messageSource, message);

        // CorvaxGoob-Anonymous-Radio-Start
        if (channel.Anonymous)
        {
            name = AnonymizeName(name);
            speech.SpeechVerbStrings = ["chat-speech-verb-default"]; // shitcode :cat_gagaga:
            jobIcon = "JobIconNoId";
            jobName = Loc.GetString("generic-unknown");
        }
        // CorvaxGoob-Anonymous-Radio-End

        var content = escapeMarkup
            ? FormattedMessage.EscapeText(message)
            : message;

        var headsetColor = TryComp(radioSource, out HeadsetComponent? headset) ? headset.Color : channel.Color;

        var job = String.Empty;
        if (_inventory.HasSlot(messageSource, "id"))
        {
            job = Loc.GetString("chat-radio-source-unknown");

            if (_inventory.TryGetSlotEntity(messageSource, "id", out var idSlotEntity))
            {
                if (TryComp(idSlotEntity, out PdaComponent? pda))
                    idSlotEntity = pda.ContainedId;

                job = TryComp(idSlotEntity, out IdCardComponent? idCard) && !string.IsNullOrEmpty(idCard.LocalizedJobTitle)
                    ? _chat.SanitizeMessageCapital(idCard.LocalizedJobTitle)
                    : Loc.GetString("chat-radio-source-unknown");
            }

            job = $"\\[{job}\\] ";
        }

        content = Highlight(content);

        var wrappedMessage = WrapRadioMessage(messageSource, channel, name, content, language, jobIcon, jobName, headsetColor, job);

        // most radios are relayed to chat, so lets parse the chat message beforehand
        // var chat = new ChatMessage(
        //     ChatChannel.Radio,
        //     message,
        //     wrappedMessage,
        //     NetEntity.Invalid,
        //     null);
        // var chatMsg = new MsgChatMessage { Message = chat };
        // var ev = new RadioReceiveEvent(message, messageSource, channel, radioSource, chatMsg);
        // Goobstation - Chat Pings
        // Added GetNetEntity(messageSource), to source
        var msg = new ChatMessage(ChatChannel.Radio, content, wrappedMessage, GetNetEntity(messageSource), null);

        // Einstein Engines - Language begin
        var obfuscated = _language.ObfuscateSpeech(content, language);
        // Goobstation - Chat Pings
        // Added GetNetEntity(messageSource), to source
        var obfuscatedWrapped = WrapRadioMessage(messageSource, channel, name, obfuscated, language, jobIcon, jobName, headsetColor, job);
        var notUdsMsg = new ChatMessage(ChatChannel.Radio, obfuscated, obfuscatedWrapped, GetNetEntity(messageSource), null);
        var ev = new RadioReceiveEvent(messageSource, channel, msg, notUdsMsg, language, radioSource);
        // Einstein Engines - Language end

        var sendAttemptEv = new RadioSendAttemptEvent(channel, radioSource);
        RaiseLocalEvent(ref sendAttemptEv);
        RaiseLocalEvent(radioSource, ref sendAttemptEv);
        var canSend = !sendAttemptEv.Cancelled;

        var sourceMapId = Transform(radioSource).MapID;
        var hasActiveServer = HasActiveServer(sourceMapId, channel.ID);
        var sourceServerExempt = _exemptQuery.HasComp(radioSource);
        // Cache transmitter presence — querying every receiver was O(n*entities) lag.
        var sourceHasTransmitter = HasActiveTransmitter(sourceMapId);
        var transmitterByMap = new Dictionary<MapId, bool> { [sourceMapId] = sourceHasTransmitter };

        var radioQuery = EntityQueryEnumerator<ActiveRadioComponent, TransformComponent>();
        while (canSend && radioQuery.MoveNext(out var receiver, out var radio, out var transform))
        {
            if (!radio.ReceiveAllChannels)
            {
                if (!radio.Channels.Contains(channel.ID) || (TryComp<IntercomComponent>(receiver, out var intercom) &&
                                                             !intercom.SupportedChannels.Contains(channel.ID)))
                    continue;
            }

            if (!channel.LongRange && transform.MapID != sourceMapId && !radio.GlobalReceive)
            {
                if (!transmitterByMap.TryGetValue(transform.MapID, out var receiverHasTransmitter))
                {
                    receiverHasTransmitter = HasActiveTransmitter(transform.MapID);
                    transmitterByMap[transform.MapID] = receiverHasTransmitter;
                }

                if (!(receiverHasTransmitter && sourceHasTransmitter)) // goob - intermap transmitters
                    continue;
            }

            // don't need telecom server for long range channels or handheld radios and intercoms
            var needServer = !channel.LongRange && !sourceServerExempt;
            if (needServer && !hasActiveServer)
                continue;

            // check if message can be sent to specific receiver
            var attemptEv = new RadioReceiveAttemptEvent(channel, radioSource, receiver);
            RaiseLocalEvent(ref attemptEv);
            RaiseLocalEvent(receiver, ref attemptEv);
            if (attemptEv.Cancelled)
                continue;

            // send the message
            RaiseLocalEvent(receiver, ref ev);
        }

        if (name != Name(messageSource))
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Radio message from {ToPrettyString(messageSource):user} as {name} on {channel.LocalizedName}: {message}");
        else
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Radio message from {ToPrettyString(messageSource):user} on {channel.LocalizedName}: {message}");

        _replay.RecordServerMessage(msg); // Einstein Engines - Language
        _messages.Remove(message);
    }

    // CorvaxGoob-Anonymous-Radio-Start

    /// <summary>
    /// Replaces string with garbage symbols and adding/removing characters from it with random chance
    /// </summary>
    /// <param name="name">String that will be anonymized</param>
    /// <returns>String consisting of garbage characters</returns>
    private string AnonymizeName(string name)
    {
        var garbageChars = "1234567890!@#$%^&*";
        var newName = string.Empty;

        for (var i = 1; i < name.Length + 1; ++i)
        {
            newName += garbageChars[_random.Next(garbageChars.Length)];
        }

        // Adding/removing characters from string to make identifying by length harder.
        var randomNumber = _random.Next(0, 2);
        switch (randomNumber)
        {
            case 1:
            {
                for (var i = 0; i < _random.Next(1, 5); ++i)
                {
                    if (_random.Next(0, 1) != 1)
                        continue;
                    newName += garbageChars[_random.Next(garbageChars.Length)];
                }

                break;
            }
            case 2:
            {
                for (var i = 0; i < _random.Next(1, 5); ++i)
                {
                    if (_random.Next(0, 1) != 1)
                        continue;
                    if (newName.Length - 1 < 2)
                        break;

                    newName.Remove(i);
                }

                break;
            }
        }

        return newName;
    }

    // CorvaxGoob-Anonymous-Radio-End

    // Einstein Engines - Language begin
    private string WrapRadioMessage(
        EntityUid source,
        RadioChannelPrototype channel,
        string name,
        string message,
        LanguagePrototype language,
        ProtoId<JobIconPrototype>? jobIcon, // Goob edit
        string? jobName, // Gaby Radio icons
        Color headsetColor,
        string job)
    {
        // TODO: code duplication with ChatSystem.WrapMessage
        var speech = _chat.GetSpeechVerb(source, message);
        var languageColor = channel.Color;

        // Goobstation - Bolded Language Overrides begin
        var wrapId = speech.Bold ? "chat-radio-message-wrap-bold" : "chat-radio-message-wrap";
        // Radio names keep headset color; bolded-language wrap uses channel color for the whole line.
        // Goobstation end

        if (language.SpeechOverride.Color is { } colorOverride)
            languageColor = Color.InterpolateBetween(Color.White, colorOverride, colorOverride.A); // Changed first param to Color.White so it shows color correctly.

        var languageDisplay = language.IsVisibleLanguage
            ? Loc.GetString("chat-manager-language-prefix", ("language", language.ChatName))
            : "";

        // goob start - loudspeakers

        int? loudSpeakFont = null;

        var getLoudspeakerEv = new GetLoudspeakerEvent();
        RaiseLocalEvent(source, ref getLoudspeakerEv);

        if (getLoudspeakerEv.Loudspeakers != null)
            foreach (var loudspeaker in getLoudspeakerEv.Loudspeakers)
            {
                var loudSpeakerEv = new GetLoudspeakerDataEvent();
                RaiseLocalEvent(loudspeaker, ref loudSpeakerEv);

                if (loudSpeakerEv.IsActive && loudSpeakerEv.AffectRadio)
                {
                    loudSpeakFont = loudSpeakerEv.FontSize;
                    break;
                }
            }

        var nameString = name;

        return Loc.GetString(wrapId,
            ("channel-color", channel.Color),
            ("color", channel.Color),
            // Mini Station start
            ("jobIcon", (object?)jobIcon ?? ""),
            ("jobName", jobName ?? ""),
            // Mini Station end
            ("languageColor", languageColor),
            ("fontType", language.SpeechOverride.FontId ?? speech.FontId),
            ("fontSize", loudSpeakFont ?? language.SpeechOverride.FontSize ?? speech.FontSize), // goob edit - "loudSpeakFont"
            ("boldFontType", language.SpeechOverride.BoldFontId ?? language.SpeechOverride.FontId ?? speech.FontId), // Goob Edit - Custom Bold Fonts
            ("verb", Loc.GetString(_random.Pick(speech.SpeechVerbStrings))),
            ("channel", $"\\[{channel.LocalizedName}\\]"),
            ("name", nameString), // goob
            ("message", message),
            ("headset-color", headsetColor),
            ("job", job),
            ("language", languageDisplay));
    }
    // Einstein Engines - Language end

    /// <inheritdoc cref="TelecomServerComponent"/>
    private bool HasActiveServer(MapId mapId, string channelId)
    {
        var servers = EntityQuery<TelecomServerComponent, EncryptionKeyHolderComponent, ApcPowerReceiverComponent, TransformComponent>();
        foreach (var (_, keys, power, transform) in servers)
        {
            if (transform.MapID == mapId &&
                power.Powered &&
                keys.Channels.Contains(channelId))
            {
                return true;
            }
        }
        return false;
    }

    private string Highlight(string msg)
    {

        foreach (var department in _departments)
        {
            string color = department.Key;
            foreach (string word in department.Value)
            {
                string redex_word = RedexWord(word);

                Regex regex = new Regex($@"\w*{redex_word}\w*", RegexOptions.IgnoreCase);
                MatchCollection matches = regex.Matches(msg);

                foreach (Match match in matches)
                {
                    msg = msg.Replace(match.Value, $"[color=#{color}]{match.Value}[/color]");
                }
            }
        }
        return msg;
    }

    private string RedexWord(string word)
    {
        string redex_word = "";
        foreach (char letter in word)
        {
            string add_letter = letter.ToString();
            if (letter == 'л')
                add_letter = "[лв]";
            if (letter == 'р')
                add_letter = "[рв]";
            if (letter == 'ы')
                add_letter = "[иы]";
            redex_word += add_letter + "+";
        }

        return redex_word.Remove(redex_word.Length - 1);
    }

    // goob start - intermap transmitters
    /// <inheritdoc cref="TelecomServerComponent"/>
    private bool HasActiveTransmitter(MapId mapId)
    {
        return EntityQuery<TelecomTransmitterComponent, ApcPowerReceiverComponent, TransformComponent>()
            .Any(server => server.Item3.MapID == mapId && server.Item2.Powered);
    }
    // goob end
}
