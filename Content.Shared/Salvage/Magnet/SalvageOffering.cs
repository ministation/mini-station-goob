// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.Salvage.Magnet;

/// <summary>
/// Asteroid offered for the magnet.
/// </summary>
public record struct SalvageOffering : ISalvageMagnetOffering
{
    public SalvageMapPrototype SalvageMap;

    uint ISalvageMagnetOffering.Cost => 0; // Mini: wrecks are pulled for free, the mining point price was removed.
}