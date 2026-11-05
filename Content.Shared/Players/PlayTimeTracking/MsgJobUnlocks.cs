// SPDX-FileCopyrightText: 2022 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-License-Identifier: MIT

using Content.Shared.Network;
using Content.Shared.Roles;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Players.PlayTimeTracking;

/// <summary>
/// Sent server -> client with permanently unlocked job roles (playtime bypass).
/// </summary>
public sealed class MsgJobUnlocks : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.EntityEvent;

    public HashSet<ProtoId<JobPrototype>> UnlockedJobs = new();

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        var count = buffer.ReadBoundedLength();
        UnlockedJobs = new HashSet<ProtoId<JobPrototype>>(count);

        for (var i = 0; i < count; i++)
            UnlockedJobs.Add(buffer.ReadString());
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.WriteVariableInt32(UnlockedJobs.Count);

        foreach (var job in UnlockedJobs)
            buffer.Write(job.ToString());
    }
}
