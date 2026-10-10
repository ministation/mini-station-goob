// SPDX-License-Identifier: AGPL-3.0-or-later

using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Goobstation.Shared.VoiceChat;

/// <summary>
/// Mini: client → server notification that the player enabled/disabled voice chat.
/// Opted-in players transmit microphone audio and are excluded from TTS generation
/// (voice chat or TTS — one or the other).
/// </summary>
public sealed class MsgVoiceChatOptIn : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public bool OptedIn;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        OptedIn = buffer.ReadBoolean();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(OptedIn);
    }
}
