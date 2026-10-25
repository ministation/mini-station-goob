// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared._Mini.Ninja;

/// <summary>
/// Three ninja paths of the Spider Clan. Each tier of the Spider OS offers one module of every path.
/// </summary>
[Serializable, NetSerializable]
public enum NinjaPath : byte
{
    Ghost = 0,
    Snake = 1,
    Steel = 2,
}
