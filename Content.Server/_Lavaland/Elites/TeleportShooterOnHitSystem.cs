// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared._Lavaland.Elites.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Robust.Shared.Network;

namespace Content.Server._Lavaland.Elites;

/// <summary>
/// Herald teleshot: teleports the shooter to the shot's impact point.
/// </summary>
public sealed class TeleportShooterOnHitSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TeleportShooterOnHitComponent, ProjectileHitEvent>(OnHit);
    }

    private void OnHit(Entity<TeleportShooterOnHitComponent> ent, ref ProjectileHitEvent args)
    {
        if (_net.IsClient || args.Shooter is not { } shooter || TerminatingOrDeleted(shooter))
            return;

        // Only mob hits: a wall hit would drop the shooter inside the wall.
        if (!HasComp<MobStateComponent>(args.Target))
            return;

        _xform.SetCoordinates(shooter, Transform(ent).Coordinates);
    }
}
