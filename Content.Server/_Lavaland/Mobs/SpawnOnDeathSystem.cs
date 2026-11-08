// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Server._Lavaland.Weapons.CainAbel;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityTable;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Whitelist;

// ReSharper disable EnforceForeachStatementBraces
// ReSharper disable EnforceIfStatementBraces
namespace Content.Server._Lavaland.Mobs;

public sealed class SpawnOnDeathSystem : EntitySystem
{
    [Dependency] private readonly CainAbelEffectsSystem _cainAbel = default!;
    [Dependency] private readonly EntityTableSystem _entityTable = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpawnLootOnDeathComponent, AttackedEvent>(OnDropAttacked);
        SubscribeLocalEvent<SpawnLootOnDeathComponent, BeforeDamageChangedEvent>(OnDropDamaged);
        SubscribeLocalEvent<ProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<SpawnLootOnDeathComponent, MobStateChangedEvent>(OnDropKilled);
    }

    private void OnDropAttacked(EntityUid uid, SpawnLootOnDeathComponent comp, ref AttackedEvent args)
    {
        comp.DoSpecialLoot = _whitelist.IsWhitelistPassOrNull(comp.SpecialWeaponWhitelist, args.Used);
    }

    // Single handler of ProjectileComponent/ProjectileHitEvent in this fork: the dagger swing parry
    // also lives here, because the event bus allows one handler per component and event pair.
    private void OnProjectileHit(Entity<ProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (_cainAbel.TryParry(ent, ref args))
            return;

        // Projectile damage only reports the shooter as its origin, so the weapon comes from the projectile itself.
        if (!args.Damage.AnyPositive() || !TryComp<SpawnLootOnDeathComponent>(args.Target, out var comp))
            return;

        comp.DoSpecialLoot = ent.Comp.Weapon is { } weapon
            && _whitelist.IsWhitelistPassOrNull(comp.SpecialWeaponWhitelist, weapon);
    }

    // Fire, explosions and other environmental damage have no weapon origin and must not keep
    // the trophy roll of an earlier crusher hit alive.
    private void OnDropDamaged(EntityUid uid, SpawnLootOnDeathComponent comp, ref BeforeDamageChangedEvent args)
    {
        if (!args.Damage.AnyPositive())
            return;

        if (args.Origin is not { } origin)
        {
            comp.DoSpecialLoot = false;
            return;
        }

        // Melee and projectile damage report the attacker instead of the weapon, both already
        // recorded the weapon in their own event right before this one.
        if (HasComp<MobStateComponent>(origin))
            return;

        comp.DoSpecialLoot = _whitelist.IsWhitelistPassOrNull(comp.SpecialWeaponWhitelist, origin);
    }

    private void OnDropKilled(EntityUid uid, SpawnLootOnDeathComponent comp, ref MobStateChangedEvent args)
    {
        if (!_mobState.IsDead(uid))
            return;

        var coords = Transform(uid).Coordinates;

        if (comp.DeleteOnDeath)
            QueueDel(uid);

        var droppedSpecial = false;
        if (comp.DoSpecialLoot && comp.SpecialTable != null)
        {
            var specialLoot = _entityTable.GetSpawns(comp.SpecialTable);
            foreach (var item in specialLoot)
                Spawn(item, coords);

            droppedSpecial = true;
        }

        if (comp.Table == null)
            return;

        var loot = _entityTable.GetSpawns(comp.Table);
        if (droppedSpecial)
        {
            if (comp.DropBoth)
                foreach (var item in loot)
                    Spawn(item, coords);
        }
        else
            foreach (var item in loot)
                Spawn(item, coords);
    }
}
