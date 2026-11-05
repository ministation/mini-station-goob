// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: MIT

using Content.Shared.Network;
using Content.Shared.Roles;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Players.PlayTimeTracking;

/// <summary>
/// Sent server -> client with permanently unlocked antag roles (playtime bypass).
/// </summary>
public sealed class MsgAntagUnlocks : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.EntityEvent;

    public HashSet<ProtoId<AntagPrototype>> UnlockedAntags = new();

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        var count = buffer.ReadBoundedLength();
        UnlockedAntags = new HashSet<ProtoId<AntagPrototype>>(count);

        for (var i = 0; i < count; i++)
            UnlockedAntags.Add(buffer.ReadString());
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.WriteVariableInt32(UnlockedAntags.Count);

        foreach (var antag in UnlockedAntags)
            buffer.Write(antag.ToString());
    }
}
