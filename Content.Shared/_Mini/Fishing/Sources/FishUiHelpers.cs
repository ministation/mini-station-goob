// SPDX-License-Identifier: LicenseRef-MiniStation-1.0
// Mechanics reference: tgstation/tgstation (code/modules/fishing).

namespace Content.Shared._Mini.Fishing.Sources;

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
