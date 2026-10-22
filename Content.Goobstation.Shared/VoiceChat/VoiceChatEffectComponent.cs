namespace Content.Goobstation.Shared.VoiceChat;

public enum VoiceEffect : byte
{
    None,
    Robot,
    Alien,
    Deep,
    High,
    Radio,
    Muffled,
    Abductor,
    Whisper,

    // Mini: extra voice mods that can be picked on the voice chat web page.
    Echo,
    Cave,
    Cathedral,
    Ghost,
    Demon,
    Telephone,
    Megaphone,
    Bitcrush,
    Underwater,
    Vibrato,
    OldRadio,
}

[RegisterComponent]
public sealed partial class VoiceChatEffectComponent : Component
{
    [DataField]
    public VoiceEffect Effect = VoiceEffect.None;
}
