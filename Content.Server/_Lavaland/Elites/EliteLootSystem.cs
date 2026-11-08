// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Goobstation.Maths.FixedPoint;
using Content.Server._Lavaland.Mobs;
using Content.Shared._Lavaland.Elites.Components;
using Content.Shared._Lavaland.Megafauna.Events;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Projectiles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Lavaland.Elites;

/// <summary>
/// Unique elite trophies: the herald cloak answers projectile hits with a ring of bolts,
/// and the tumor shard revives a dead elite as a passive, crew friendly mob.
/// </summary>
public sealed class EliteLootSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HeraldCloakComponent, InventoryRelayedEvent<ProjectileReflectAttemptEvent>>(OnWearerHit);
        SubscribeLocalEvent<LavalandEliteComponent, InteractUsingEvent>(OnShardUsed);
    }

    private void OnWearerHit(Entity<HeraldCloakComponent> ent, ref InventoryRelayedEvent<ProjectileReflectAttemptEvent> args)
    {
        if (_net.IsClient || args.Args.Cancelled || !_random.Prob(ent.Comp.Chance))
            return;

        // A full ring of bolts around the wearer, like the herald's own omni shot.
        // Action events are plain broadcasts, so the shot system only hears them when they are raised as such.
        RaiseLocalEvent(args.Owner, new MegafaunaShotActionEvent
        {
            Performer = args.Owner,
            Target = Transform(args.Owner).Coordinates,
            Projectile = ent.Comp.Projectile,
            Count = ent.Comp.Count,
            Speed = ent.Comp.Speed,
            Pattern = MegafaunaShotPattern.Ring,
        }, broadcast: true);
    }

    private void OnShardUsed(Entity<LavalandEliteComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || _net.IsClient)
            return;

        if (!TryComp<TumorShardComponent>(args.Used, out var shard)
            || !_mobState.IsDead(ent)
            || !TryComp<DamageableComponent>(ent, out var damageable))
            return;

        // TG brings the elite back hurt, at a fraction of its health.
        var reviveDamage = FixedPoint2.Zero;
        if (_mobThreshold.TryGetThresholdForState(ent, MobState.Dead, out var deadThreshold) && deadThreshold is { } threshold)
            reviveDamage = threshold * shard.HealthFraction;

        var damage = new DamageSpecifier();
        if (reviveDamage > FixedPoint2.Zero)
            damage = new DamageSpecifier(_proto.Index<DamageTypePrototype>("Blunt"), reviveDamage);

        // The mob has to allow revives for the damage change to wake it up.
        _mobThreshold.SetAllowRevives(ent, true);
        _damageable.SetDamage((ent.Owner, damageable), damage);
        _mobThreshold.SetAllowRevives(ent, false);

        if (_mobState.IsDead(ent))
            _mobState.ChangeMobState(ent, MobState.Alive);

        // Passive and crew friendly: the revived elite fights the Necropolis, not the station.
        _faction.ClearFactions(ent.Owner);
        _faction.AddFaction(ent.Owner, shard.Faction.Id);

        // No loot on the second death, otherwise shards could farm elite trophies.
        RemComp<SpawnLootOnDeathComponent>(ent);

        QueueDel(args.Used);
        args.Handled = true;
    }
}
