// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

namespace Content.Shared._Mini.TypanWar;

public enum TypanWarPhase : byte
{
    Inactive = 0,
    Pending = 1,
    Active = 2,
    Ended = 3,
}

public enum TypanWarWinner : byte
{
    None = 0,
    Nanotrasen = 1,
    Typan = 2,
    Stalemate = 3,
}

public enum TypanWarSide : byte
{
    Nanotrasen = 0,
    Typan = 1,
}
