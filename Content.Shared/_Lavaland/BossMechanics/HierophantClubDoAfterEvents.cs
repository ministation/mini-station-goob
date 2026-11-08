// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Lavaland.BossMechanics;

/// <summary>
/// Because the client has to replay the do-after.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class HierophantClubDeployDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// Because the client has to replay the do-after.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class HierophantClubTeleportDoAfterEvent : SimpleDoAfterEvent;
