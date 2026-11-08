// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

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
