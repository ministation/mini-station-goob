// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Lavaland.Mobs;

/// <summary>
/// Finishes affixing a saddle to a tamed lavaland mob. Lives in shared because the client
/// has to replay the do-after.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class LavalandSaddleDoAfterEvent : SimpleDoAfterEvent;
