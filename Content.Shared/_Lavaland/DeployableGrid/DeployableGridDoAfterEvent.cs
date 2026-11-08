// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Lavaland.DeployableGrid;

[Serializable, NetSerializable]
public sealed partial class DeployableGridDoAfterEvent : SimpleDoAfterEvent;
