// SPDX-FileCopyrightText: 2026 Mini Station
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

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
