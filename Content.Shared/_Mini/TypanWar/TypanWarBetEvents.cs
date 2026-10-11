// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared._Mini.TypanWar;

[Serializable, NetSerializable]
public sealed class TypanWarBetStateRequestEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class TypanWarBetRequestEvent(TypanWarSide side, int amount) : EntityEventArgs
{
    public TypanWarSide Side { get; } = side;
    public int Amount { get; } = amount;
}

/// <summary>
/// Personal bet state. Bets of other players stay hidden until the war ends.
/// </summary>
[Serializable, NetSerializable]
public sealed class TypanWarBetStateEvent(
    bool bettingOpen,
    TypanWarSide? mySide,
    int myAmount,
    int[] amounts) : EntityEventArgs
{
    public bool BettingOpen { get; } = bettingOpen;
    public TypanWarSide? MySide { get; } = mySide;
    public int MyAmount { get; } = myAmount;
    public int[] Amounts { get; } = amounts;

    public static TypanWarBetStateEvent Closed() => new(false, null, 0, []);
}
