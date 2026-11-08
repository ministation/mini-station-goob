// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.Projectiles;

namespace Content.Server._Lavaland.Megafauna;

public sealed class RockBreakingProjectileSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RockBreakingProjectileComponent, ProjectileHitEvent>(OnHit);
    }

    private void OnHit(Entity<RockBreakingProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        var id = MetaData(args.Target).EntityPrototype?.ID;
        // shortcut: rock is matched by prototype id, switch to a tag if rock ids stop using the WallRock prefix
        if (id != null && id.StartsWith("WallRock", StringComparison.Ordinal))
            QueueDel(args.Target);
    }
}
