// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.IO;
using Content.Shared.Network;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Mapping;

public sealed class MappingMapDataMessage : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;
    public override NetDeliveryMethod DeliveryMethod => NetDeliveryMethod.ReliableUnordered;

    public ZStdCompressionContext Context = default!;
    public string Yml = default!;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        MsgSize = buffer.LengthBytes;

        var uncompressedLength = buffer.ReadVariableInt32();
        var compressedLength = buffer.ReadBoundedLength();

        // Клиент присылает обе длины сам: потолок по сжатым данным не даёт подделанному
        // пакету заставить сервер выделить поток на гигабайты до проверки прав.
        if (uncompressedLength <= 0 || uncompressedLength > compressedLength * 100)
            throw new InvalidOperationException($"Invalid map payload: {uncompressedLength} of {compressedLength}");
        var stream = new MemoryStream(compressedLength);
        buffer.ReadAlignedMemory(stream, compressedLength);
        using var decompress = new ZStdDecompressStream(stream);
        using var decompressed = new MemoryStream(uncompressedLength);

        decompress.CopyTo(decompressed, uncompressedLength);
        decompressed.Position = 0;
        serializer.DeserializeDirect(decompressed, out Yml);
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        var stream = new MemoryStream();
        serializer.SerializeDirect(stream, Yml);
        buffer.WriteVariableInt32((int) stream.Length);

        stream.Position = 0;
        var buf = new byte[ZStd.CompressBound((int) stream.Length)];
        var length = Context.Compress2(buf, stream.AsSpan());

        buffer.WriteVariableInt32(length);
        buffer.Write(buf.AsSpan(0, length));
    }
}