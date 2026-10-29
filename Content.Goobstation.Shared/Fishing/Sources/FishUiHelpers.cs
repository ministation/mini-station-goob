// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

namespace Content.Goobstation.Shared.Fishing.Sources;

/// <summary>
/// Shared formatting helpers for the fish catalog and the fish analyzer windows.
/// </summary>
public static class FishUiHelpers
{
    public static int KelvinToCelsius(float kelvin)
    {
        return (int) MathF.Round(kelvin - 273.15f);
    }

    public static string GetFluidName(AquariumFluid fluid)
    {
        return Loc.GetString(fluid switch
        {
            AquariumFluid.Anadromous => "fishing-fluid-anadromous",
            AquariumFluid.Saltwater => "fishing-fluid-saltwater",
            AquariumFluid.Freshwater => "fishing-fluid-freshwater",
            AquariumFluid.SulphWater => "fishing-fluid-sulphuric",
            AquariumFluid.Air => "fishing-fluid-air",
            _ => "fishing-fluid-any",
        });
    }
}
