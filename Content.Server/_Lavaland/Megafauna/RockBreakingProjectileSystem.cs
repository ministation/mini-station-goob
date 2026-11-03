// SPDX-License-Identifier: AGPL-3.0-or-later

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
