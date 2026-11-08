// SPDX-FileCopyrightText: 2026 Mini Station
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

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
