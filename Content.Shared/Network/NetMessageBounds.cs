// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Lidgren.Network;

namespace Content.Shared.Network;

/// <summary>
///     Mini: проверка длин, которые клиент присылает в сетевом сообщении.
/// </summary>
public static class NetMessageBounds
{
    /// <summary>
    ///     Читает длину, пришедшую от клиента, и требует, чтобы она влезала в сам пакет:
    ///     каждый элемент или байт занимает в буфере минимум один байт.
    ///     Lidgren в Release на нехватке байт не бросает исключение (ReadString подрезает позицию
    ///     и возвращает пустую строку), поэтому без такой проверки цикл в ReadFromBuffer можно
    ///     растянуть на миллиарды итераций и уронить сервер по памяти.
    ///     Исключение отсюда NetManager ловит и отключает клиента с "Failed to deserialize packet".
    /// </summary>
    public static int ReadBoundedLength(this NetIncomingMessage buffer)
    {
        return CheckBounded(buffer, buffer.ReadVariableInt32());
    }

    /// <summary>
    ///     То же для длины, записанной фиксированным int32.
    /// </summary>
    public static int ReadBoundedLength32(this NetIncomingMessage buffer)
    {
        return CheckBounded(buffer, buffer.ReadInt32());
    }

    private static int CheckBounded(NetIncomingMessage buffer, int length)
    {
        if (length < 0 || length > buffer.LengthBytes)
            throw new InvalidOperationException($"Invalid net message length: {length}");

        return length;
    }
}
