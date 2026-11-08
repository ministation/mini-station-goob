// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

namespace Content.Shared._Lavaland.Mobs;

/// <summary>
///     Marker for a Legion of any tier. Used on the server to drop the staff of storms only
///     when the last living legion of the fight dies, like TG's legion death().
/// </summary>
[RegisterComponent]
public sealed partial class LegionBossLootComponent : Component;
