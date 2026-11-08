// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameObjects;

namespace Content.Shared._Lavaland.Necropolis;

/// <summary>
///     Damage to the holder forces closed necropolis gates on its map open
///     (TG megafauna legion on_damage_loss_changed): the boss cannot be walled off.
/// </summary>
[RegisterComponent]
public sealed partial class NecropolisGateOpenOnDamageComponent : Component;
