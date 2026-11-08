// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using Content.Shared._Lavaland.ItemUpgrades;
using Content.Shared._Lavaland.ItemUpgrades.Components;
using Content.Shared._Lavaland.TileChaser;
using Content.Shared._Lavaland.Weapons.Lava;
using Content.Shared._Lavaland.Weapons.Marker;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Crusher;

/// <summary>
/// Applies the effects of crusher trophies inserted into a crusher weapon.
/// </summary>
public sealed class CrusherTrophySystem : EntitySystem
{
    private static readonly ProtoId<NpcFactionPrototype> FriendlyFaction = "NanoTrasen";

    private static readonly Vector2[] PatchDirections =
    {
        Vector2.Zero,
        new(1f, 0f),
        new(-1f, 0f),
        new(0f, 1f),
        new(0f, -1f),
    };

    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ItemUpgradesSystem _upgrades = default!;
    [Dependency] private readonly NpcFactionSystem _factions = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CrusherTrophyComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<ItemUpgradeableComponent, UseInHandEvent>(OnCrusherUseInHand);
        SubscribeLocalEvent<MobStateComponent, ApplyMarkerBonusEvent>(OnMarkerDetonated);
        SubscribeLocalEvent<CrusherTrophyDamageReductionComponent, DamageModifyEvent>(OnDamageModify);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CrusherTrophyDamageReductionComponent>();
        while (query.MoveNext(out var uid, out var reduction))
        {
            if (_timing.CurTime >= reduction.EndTime)
                RemCompDeferred(uid, reduction);
        }
    }

    private void OnMeleeHit(Entity<CrusherTrophyComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0 || ent.Comp.MeleeHeal <= 0f)
            return;

        if (TerminatingOrDeleted(args.User) || !HasComp<DamageableComponent>(args.User))
            return;

        _damageable.HealEvenly(args.User, -ent.Comp.MeleeHeal);
    }

    private void OnMarkerDetonated(Entity<MobStateComponent> target, ref ApplyMarkerBonusEvent args)
    {
        if (!TryComp<ItemUpgradeableComponent>(args.Weapon, out var upgradeable))
            return;

        foreach (var upgrade in _upgrades.GetCurrentUpgrades((args.Weapon, upgradeable)))
        {
            if (TryComp<CrusherTrophyComponent>(upgrade.Owner, out var trophy))
            {
                Detonate(trophy, args.User, target.Owner);
                continue;
            }

            // The ice block talisman is a trophy too, it freezes the marked target instead of blasting it.
            if (TryComp<IceBlockTalismanComponent>(upgrade.Owner, out var talisman))
                Freeze(talisman, target.Owner);
        }
    }

    private void Freeze(IceBlockTalismanComponent talisman, EntityUid target)
    {
        if (TerminatingOrDeleted(target))
            return;

        // TG only blocks movement, the stun also stops attacks and is the closest this fork has.
        _stun.TryAddStunDuration(target, talisman.Duration);
        Spawn(talisman.Cube, Transform(target).Coordinates);
        _popup.PopupEntity(Loc.GetString("frost-miner-ice-block"), target, target);
    }

    private void Detonate(CrusherTrophyComponent trophy, EntityUid user, EntityUid target)
    {
        if (TerminatingOrDeleted(user))
            return;

        var userXform = Transform(user);

        if (trophy.DetonationHeal > 0f && HasComp<DamageableComponent>(user))
            _damageable.HealEvenly(user, -trophy.DetonationHeal);

        if (trophy.DetonationHeatDamage > 0f)
        {
            var radius = MathF.Max(0.5f, trophy.DetonationPushRadius);
            foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(userXform.Coordinates, radius))
            {
                if (mob.Owner == user
                    || TerminatingOrDeleted(mob.Owner)
                    || _factions.IsEntityFriendly(user, mob.Owner))
                {
                    continue;
                }

                _damageable.TryChangeDamage(
                    mob.Owner,
                    new DamageSpecifier { DamageDict = { { "Heat", trophy.DetonationHeatDamage } } },
                    origin: user);
                PushAway(user, mob.Owner);
            }
        }

        if (trophy.DetonationDamageReductionDuration > TimeSpan.Zero)
        {
            var reduction = EnsureComp<CrusherTrophyDamageReductionComponent>(user);
            reduction.EndTime = _timing.CurTime + trophy.DetonationDamageReductionDuration;
        }

        if (trophy.DetonationChaser is { } chaserProto)
        {
            var chaser = Spawn(chaserProto, userXform.Coordinates);
            if (TryComp<TileChaserComponent>(chaser, out var tileChaser))
                tileChaser.Target = target;
        }

        if (trophy.DetonationSpawn is not { } spawnProto
            || !_random.Prob(Math.Clamp(trophy.DetonationSpawnChance, 0f, 1f)))
        {
            return;
        }

        var coords = trophy.DetonationSpawnAtTarget && !TerminatingOrDeleted(target)
            ? Transform(target).Coordinates
            : userXform.Coordinates;

        if (!trophy.DetonationSpawnPatch)
        {
            MakeLoyal(Spawn(spawnProto, coords));
            return;
        }

        foreach (var offset in PatchDirections)
            Spawn(spawnProto, coords.Offset(offset));
    }

    private void PushAway(EntityUid user, EntityUid target)
    {
        if (!TryComp<PhysicsComponent>(target, out var body) || body.BodyType != BodyType.Dynamic)
            return;

        var diff = Transform(target).WorldPosition - Transform(user).WorldPosition;
        if (diff.LengthSquared() < 0.001f)
            return;

        _physics.SetLinearVelocity(target, Vector2.Normalize(diff) * 4f, body: body);
    }

    // TG parity: the trophy is used through the crusher, not while held on its own.
    private void OnCrusherUseInHand(Entity<ItemUpgradeableComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || TerminatingOrDeleted(args.User))
            return;

        foreach (var upgrade in _upgrades.GetCurrentUpgrades(ent))
        {
            if (TryComp<CrusherTrophyComponent>(upgrade.Owner, out var trophy)
                && UseTrophy((upgrade.Owner, trophy), args.User))
            {
                args.Handled = true;
                return;
            }
        }
    }

    private bool UseTrophy(Entity<CrusherTrophyComponent> ent, EntityUid user)
    {
        var comp = ent.Comp;
        if (comp.LavaImmunityDuration <= TimeSpan.Zero && comp.UseSpawn == null)
            return false;

        if (_timing.CurTime < comp.NextUse)
        {
            _popup.PopupEntity(Loc.GetString("crusher-trophy-cooldown"), user, user);
            return false;
        }

        if (comp.LavaImmunityDuration > TimeSpan.Zero)
        {
            if (HasComp<LavaImmunityComponent>(user))
            {
                _popup.PopupEntity(Loc.GetString("crusher-trophy-lava-immune-already"), user, user);
                return false;
            }

            EnsureComp<LavaImmunityComponent>(user);
            Timer.Spawn(comp.LavaImmunityDuration, () =>
            {
                if (!TerminatingOrDeleted(user))
                    RemComp<LavaImmunityComponent>(user);
            });
            comp.NextUse = _timing.CurTime + comp.LavaImmunityCooldown;
            _popup.PopupEntity(Loc.GetString("crusher-trophy-tongue-squeeze"), user, user);
            return true;
        }

        if (comp.UseSpawn is not { } spawnProto)
            return false;

        MakeLoyal(Spawn(spawnProto, Transform(user).Coordinates));
        comp.NextUse = _timing.CurTime + comp.UseSpawnCooldown;
        _popup.PopupEntity(Loc.GetString("crusher-trophy-spine-shake"), user, user);
        return true;
    }

    private void OnDamageModify(Entity<CrusherTrophyDamageReductionComponent> ent, ref DamageModifyEvent args)
    {
        foreach (var key in args.Damage.DamageDict.Keys)
            args.Damage.DamageDict[key] *= ent.Comp.Multiplier;
    }

    private void MakeLoyal(EntityUid minion)
    {
        // shortcut: trophies mark their summons friendly to crew by faction, proper minion AI is out of scope.
        if (!HasComp<NpcFactionMemberComponent>(minion))
            return;

        _factions.ClearFactions(minion);
        _factions.AddFaction(minion, FriendlyFaction);
    }
}
