// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// TG goldgrub girth: while conscious it repels mining projectiles (plasma cutter
/// bolts and kinetic shots) instead of taking the hit.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandProjectileBlockerComponent : Component
{
    /// <summary>
    /// Damage types that get blocked. An empty set blocks every projectile.
    /// </summary>
    [DataField]
    public HashSet<string> BlockedDamageTypes = new()
    {
        "Heat",
        "Piercing",
    };

    [DataField]
    public bool OnlyWhenConscious = true;
}

public sealed class LavalandProjectileBlockerSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandProjectileBlockerComponent, ProjectileReflectAttemptEvent>(OnProjectileAttempt);
    }

    private void OnProjectileAttempt(Entity<LavalandProjectileBlockerComponent> ent, ref ProjectileReflectAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (ent.Comp.OnlyWhenConscious && !_mobState.IsAlive(ent.Owner))
            return;

        if (ent.Comp.BlockedDamageTypes.Count > 0)
        {
            var blocked = false;
            foreach (var (type, _) in args.Component.Damage.DamageDict)
            {
                if (!ent.Comp.BlockedDamageTypes.Contains(type))
                    continue;

                blocked = true;
                break;
            }

            if (!blocked)
                return;
        }

        args.Cancelled = true;
        QueueDel(args.ProjUid);
    }
}
