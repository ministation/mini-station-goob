// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameObjects;

namespace Content.Shared._Mini.BluespaceLifeline;

/// <summary>
/// On trigger, teleports the implant host to a CentComm warp instead of deleting them.
/// </summary>
[RegisterComponent]
public sealed partial class TeleportToCentCommOnTriggerComponent : Component;
