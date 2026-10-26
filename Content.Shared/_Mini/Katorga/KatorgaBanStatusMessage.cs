// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Mini.Katorga;

/// <summary>
///     Сервер сообщает клиенту остаток отрабатываемого временного бана, чтобы тот показал
///     срок в лобби вместо кнопки персонализации. Передаётся относительный остаток:
///     клиент считает его своими часами и не зависит от расхождения времени с сервером.
///     Нулевой остаток означает, что бана нет (или срок уже отработан).
/// </summary>
public sealed class KatorgaBanStatusMessage : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.EntityEvent;

    public TimeSpan Remaining { get; set; }

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Remaining = buffer.ReadTimeSpan();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Remaining);
    }
}
