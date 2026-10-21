// SPDX-License-Identifier: AGPL-3.0-or-later

using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Goobstation.Shared.VoiceChat;

/// <summary>
/// Mini: client → server notification that the player participates in voice chat.
/// Voice chat is always on for new clients, so this is normally true. The server uses it to
/// accept microphone audio; TTS works independently and is no longer disabled by voice chat.
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
