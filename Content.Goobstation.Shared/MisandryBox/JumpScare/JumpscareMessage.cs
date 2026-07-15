// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Goobstation.Shared.MisandryBox.JumpScare;

public sealed class JumpscareMessage : NetMessage
{
    public string ImagePath = "";

    public override MsgGroups MsgGroup => MsgGroups.String;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        ImagePath = buffer.ReadString();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(ImagePath);
    }
}
