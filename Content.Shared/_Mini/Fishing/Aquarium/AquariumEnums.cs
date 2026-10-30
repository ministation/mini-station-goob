// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

namespace Content.Shared._Mini.Fishing.Aquarium;

/// <summary>
/// How the aquarium treats its fluid and temperature.
/// </summary>
public enum AquariumMode : byte
{
    /// <summary>Everything is set by hand, nothing drifts or is adjusted automatically.</summary>
    Manual,

    /// <summary>The tank picks the fluid and the temperature that keeps most fish alive.</summary>
    Assisted,

    /// <summary>Fish neither grow nor breed nor starve, the tank drifts to room temperature.</summary>
    Stasis,
}

/// <summary>
/// Permanent improvements installed into an aquarium by an upgrade kit.
/// </summary>
[Flags]
public enum AquariumUpgradeFlags : byte
{
    None = 0,

    /// <summary>Electrogenesis fish zap the tank surroundings, the tank gets the bioelectric sprite.</summary>
    BioelectricGenerator = 1 << 0,

    /// <summary>Much bigger fish capacity, for the bluespace tank kit.</summary>
    BluespaceCapacity = 1 << 1,
}
