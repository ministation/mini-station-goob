// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

namespace Content.Server._Mini.TypanWar;

/// <summary>Per-player war contribution used for MVP pick, rewards and round-end text.</summary>
public sealed class TypanWarPlayerStats
{
    public string Name = string.Empty;

    public int Captures;

    public int Kills;

    public int Score => Captures + Kills;
}
