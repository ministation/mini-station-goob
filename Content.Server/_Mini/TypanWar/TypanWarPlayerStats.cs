// SPDX-FileCopyrightText: 2026 Egorik1
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

namespace Content.Server._Mini.TypanWar;

/// <summary>Per-player war contribution used for MVP pick, rewards and round-end text.</summary>
public sealed class TypanWarPlayerStats
{
    public string Name = string.Empty;

    public int Captures;

    public int Kills;

    public int Score => Captures + Kills;
}
