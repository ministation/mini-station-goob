// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Goobstation.Server.BloodBrother;

/// <summary>
/// Completes when the team has more than one member.
/// This mirrors TG's "convert brother" objective, which is mostly there to
/// teach players that they can recruit more people.
/// </summary>
[RegisterComponent]
public sealed partial class BloodBrotherConvertConditionComponent : Component;
