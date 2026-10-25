// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.Ninja.Systems;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Network;

namespace Content.Shared._Mini.Ninja.Weapons;

/// <summary>
/// Feeds a gun with projectiles spawned on the fly and paid for with the ninja suit battery charge.
/// Replaces the regular ammo providers of the gun, so no magazine or cartridge is needed.
/// </summary>
public sealed class NinjaSuitAmmoProviderSystem : EntitySystem
{
    [Dependency] private readonly NinjaSuitPowerSystem _suitPower = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaSuitAmmoProviderComponent, TakeAmmoEvent>(OnTakeAmmo);
        SubscribeLocalEvent<NinjaSuitAmmoProviderComponent, GetAmmoCountEvent>(OnAmmoCount);
    }

    private void OnTakeAmmo(Entity<NinjaSuitAmmoProviderComponent> ent, ref TakeAmmoEvent args)
    {
        if (args.User is not { } user)
            return;

        for (var i = 0; i < args.Shots; i++)
        {
            // The burst stops as soon as the suit cannot pay for the next projectile.
            if (!_suitPower.HasCharge(user, ent.Comp.EnergyPerShoot))
                break;

            // Only the server owns the battery state, the client just predicts the projectile.
            if (_net.IsServer)
                _suitPower.TryUseCharge(user, ent.Comp.EnergyPerShoot);

            var ammo = PredictedSpawnAtPosition(ent.Comp.Proto, args.Coordinates);
            args.Ammo.Add((ammo, _gun.EnsureShootable(ammo)));
        }
    }

    private void OnAmmoCount(Entity<NinjaSuitAmmoProviderComponent> ent, ref GetAmmoCountEvent args)
    {
        // There is no magazine to count, the suit battery is the only limitation.
        args.Capacity = 1;
        args.Count = 1;
    }
}
