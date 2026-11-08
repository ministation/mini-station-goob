// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;

namespace Content.Shared._Lavaland.Megafauna.Events;

/// <summary>
/// TG hierophant club beacon action: deploys the beacon, or channels a group
/// teleport to it once the beacon is placed.
/// </summary>
public sealed partial class HierophantClubBeaconActionEvent : InstantActionEvent;
