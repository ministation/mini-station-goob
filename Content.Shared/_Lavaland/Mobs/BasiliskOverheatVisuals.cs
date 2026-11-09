// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.Serialization;

namespace Content.Shared._Lavaland.Mobs;

/// <summary>
/// Appearance keys for the overheat state of a basilisk.
/// </summary>
[Serializable, NetSerializable]
public enum BasiliskOverheatVisuals : byte
{
    Overheated,
}
