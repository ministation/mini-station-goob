// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Lavaland.ItemUpgrades;
using Content.Shared._Lavaland.ItemUpgrades.Components;
using Content.Shared._Lavaland.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Server._Lavaland.Crusher;

/// <summary>
/// TG parity for the raptor feather crusher trophy: every shot fired from a crusher
/// with that trophy installed passes through the shooter's allies.
/// </summary>
public sealed class PassThroughAlliesTrophySystem : EntitySystem
{
    [Dependency] private readonly ItemUpgradesSystem _upgrades = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunComponent, GunShotEvent>(OnGunShot);
    }

    private void OnGunShot(Entity<GunComponent> gun, ref GunShotEvent args)
    {
        if (!TryComp<ItemUpgradeableComponent>(gun, out var upgradeable))
            return;

        var passThrough = false;
        foreach (var upgrade in _upgrades.GetCurrentUpgrades((gun.Owner, upgradeable)))
        {
            if (TryComp<FaunaCrusherTrophyComponent>(upgrade.Owner, out var trophy) && trophy.PassThroughAllies)
            {
                passThrough = true;
                break;
            }
        }

        if (!passThrough)
            return;

        foreach (var (uid, _) in args.Ammo)
        {
            if (uid is { } projectile)
                EnsureComp<ProjectileIgnoreAlliesComponent>(projectile);
        }
    }
}
