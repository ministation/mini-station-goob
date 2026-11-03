// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Lavaland.Ashwalker;

/// <summary>
/// Appearance keys of the tribal bonfire.
/// </summary>
[Serializable, NetSerializable]
public enum TribeBonfireVisuals : byte
{
    Lit,
}

/// <summary>
/// Appearance keys of the head pike.
/// </summary>
[Serializable, NetSerializable]
public enum HeadPikeVisuals : byte
{
    HasHead,
}
