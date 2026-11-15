// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Roles.Components;

namespace Content.Goobstation.Shared.BloodBrother;

/// <summary>
/// Mind role component for blood brothers.
/// Used by objective role requirements to tell brother objectives apart from traitor ones.
/// </summary>
[RegisterComponent]
public sealed partial class BloodBrotherRoleComponent : BaseMindRoleComponent;
