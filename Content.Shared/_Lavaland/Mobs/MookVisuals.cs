// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Lavaland.Mobs;

/// <summary>
/// Appearance keys for mook combat poses (TG mook_warmup/mook_leap states).
/// </summary>
[Serializable, NetSerializable]
public enum MookVisuals : byte
{
    Leaping,
}
