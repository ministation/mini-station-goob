// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Lavaland.Mobs;

/// <summary>
///     Marker for a Legion of any tier. Used on the server to drop the staff of storms only
///     when the last living legion of the fight dies, like TG's legion death().
/// </summary>
[RegisterComponent]
public sealed partial class LegionBossLootComponent : Component;
