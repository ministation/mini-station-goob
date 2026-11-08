// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameObjects;

namespace Content.Shared._Lavaland.Mobs;

/// <summary>
///     Legions with this component infest unconscious humans they hit in melee
///     (TG megafauna legion AttackingTarget): the body is swallowed into a new
///     lesser legion and released when that legion dies.
/// </summary>
[RegisterComponent]
public sealed partial class LegionInfestComponent : Component;
