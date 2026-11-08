// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameObjects;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
///     Marks a legion that swallowed a body through infest; the body is released
///     when the legion dies.
/// </summary>
[RegisterComponent]
public sealed partial class LegionShellComponent : Component
{
    public EntityUid? StoredBody;
}
