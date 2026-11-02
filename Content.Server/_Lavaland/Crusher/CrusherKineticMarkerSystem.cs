// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Lavaland.Weapons;
using Content.Server._Lavaland.Weapons.Ranged;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Lavaland.Crusher;

/// <summary>
/// Puts the kinetic marker built into a crusher back after a main upgrade is ejected,
/// because the upgrade removes the gun components it replaced.
/// </summary>
public sealed class CrusherKineticMarkerSystem : EntitySystem
{
    private static readonly EntProtoId MarkerPrototype = "BaseWeaponCrusherMarker";

    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Same event as the gun upgrades system, but on the upgrade's own marker component,
        // so the restore runs after that system removed the replaced components.
        SubscribeLocalEvent<KineticMarkerUpgradeComponent, EntGotRemovedFromContainerMessage>(OnMarkerUpgradeEjected,
            after: new[] { typeof(GunUpgradesSystem) });
    }

    private void OnMarkerUpgradeEjected(Entity<KineticMarkerUpgradeComponent> ent,
        ref EntGotRemovedFromContainerMessage args)
    {
        if (!HasComp<MegafaunaWeaponLooterComponent>(args.Container.Owner))
            return;

        EntityManager.AddComponents(args.Container.Owner, _proto.Index(MarkerPrototype), removeExisting: false);
    }
}
