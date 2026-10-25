// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Ninja.Weapons;

/// <summary>
/// Marker for a gun that shoots projectiles supplied by the ninja suit instead of regular ammo.
/// Every shot is paid for with charge from the suit battery, see <see cref="NinjaSuitAmmoProviderSystem"/>.
/// </summary>
[RegisterComponent]
public sealed partial class NinjaSuitAmmoProviderComponent : Component
{
    /// <summary>
    /// Entity prototype that is spawned instead of a cartridge.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Proto;

    /// <summary>
    /// Suit battery charge spent for a single shot.
    /// </summary>
    [DataField(required: true)]
    public float EnergyPerShoot;
}
