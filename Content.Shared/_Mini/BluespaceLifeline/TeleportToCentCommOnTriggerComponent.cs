// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameObjects;

namespace Content.Shared._Mini.BluespaceLifeline;

/// <summary>
/// On trigger, teleports the implant host to a CentComm warp instead of deleting them.
/// </summary>
[RegisterComponent]
public sealed partial class TeleportToCentCommOnTriggerComponent : Component;
