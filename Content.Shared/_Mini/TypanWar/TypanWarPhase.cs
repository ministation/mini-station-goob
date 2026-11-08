// SPDX-FileCopyrightText: 2026 Egorik1
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

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
