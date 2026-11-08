// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Lavaland.BossMechanics;

/// <summary>
/// Finishes the overload of a molecular accelerator in The Thing arena. Lives in shared
/// because the client has to replay the do-after.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ThingAcceleratorOverloadDoAfterEvent : SimpleDoAfterEvent;
