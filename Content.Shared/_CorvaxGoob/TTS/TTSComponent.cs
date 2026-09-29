using Robust.Shared.GameStates;

namespace Content.Shared._CorvaxGoob.TTS;

/// <summary>
/// Apply TTS for entity chat say messages
/// </summary>
[RegisterComponent, NetworkedComponent]
// ReSharper disable once InconsistentNaming
public sealed partial class TTSComponent : Component
{
    /// <summary>
    /// Prototype of used voice for TTS.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("voice")]
    public string? VoicePrototypeId { get; set; } = "Eugene";

    /// <summary>
    /// Pitch of played TTS sound.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("pitch")]
    public float Pitch { get; set; } = 1;
}
