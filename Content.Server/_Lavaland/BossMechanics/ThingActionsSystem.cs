// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using System.Threading;
using Timer = Robust.Shared.Timing.Timer;
using Content.Shared._Lavaland.BossMechanics;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// Attack implementations for The Thing: spikes (decimate and tendrils), charges, the shriek
/// and the acid shower. Everything is telegraphed before it lands, like on TG.
/// </summary>
public sealed class ThingActionsSystem : EntitySystem
{
    private const string SpikeProto = "ThingTendrilSpike";
    private const string TelegraphProto = "ThingTelegraph";
    private const string TelegraphShortProto = "ThingTelegraphShort";
    private const string TelegraphLargeProto = "ThingTelegraphLarge";

    private static readonly SoundPathSpecifier ChargeSound = new("/Audio/Weapons/bladeslice.ogg");
    private static readonly SoundPathSpecifier ShriekSound = new("/Audio/_EinsteinEngines/Shadowling/screech.ogg");
    private static readonly SoundPathSpecifier SpikeSound = new("/Audio/Animals/Blob/blobattack.ogg");

    private static readonly ProtoId<TagPrototype> WallTag = "Wall";

    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedJitteringSystem _jittering = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThingBossComponent, ThingAttackActionEvent>(OnAttack);
    }

    private void OnAttack(Entity<ThingBossComponent> ent, ref ThingAttackActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        switch (args.Attack)
        {
            case ThingAttackKind.Decimate:
                Decimate(ent);
                break;
            case ThingAttackKind.Charge:
                Charge(ent, args.Target);
                break;
            case ThingAttackKind.Shriek:
                Shriek(ent);
                break;
            case ThingAttackKind.CardinalTendrils:
                CardinalTendrils(ent);
                break;
            case ThingAttackKind.BigTendrils:
                BigTendrils(ent, args.Target);
                break;
            case ThingAttackKind.AcidSpit:
                AcidSpit(ent);
                break;
        }
    }

    // Decimate: a box of spikes around the boss itself.

    private void Decimate(Entity<ThingBossComponent> ent)
    {
        var coords = Transform(ent).Coordinates;
        SpawnTelegraphBox(coords, 2, TelegraphProto, 1.5f);
        var uid = ent.Owner;

        Timer.Spawn(TimeSpan.FromSeconds(1.5f), () =>
        {
            if (TerminatingOrDeleted(uid))
                return;

            _audio.PlayPvs(SpikeSound, coords);
            SpawnSpikeBox(coords, 2, uid);
        }, CancellationToken.None);
    }

    // Charge: lunge at the target, twice in the last phase.

    private void Charge(Entity<ThingBossComponent> ent, EntityCoordinates target)
    {
        DoCharge(ent.Owner, target, ent.Comp.Phase >= ent.Comp.MaxPhases ? 2 : 1);
    }

    private void DoCharge(EntityUid boss, EntityCoordinates target, int remaining)
    {
        Spawn(TelegraphShortProto, target);

        Timer.Spawn(TimeSpan.FromSeconds(0.5f), () =>
        {
            if (TerminatingOrDeleted(boss))
                return;

            PerformCharge(boss, target);

            if (remaining <= 1
                || !TryComp<MegafaunaAiTargetingComponent>(boss, out var targeting)
                || targeting.TargetCoords is not { } next)
                return;

            // The second lunge follows the target, like the TG double charge in the last phase.
            DoCharge(boss, next, remaining - 1);
        }, CancellationToken.None);
    }

    private void PerformCharge(EntityUid boss, EntityCoordinates target)
    {
        _xform.SetCoordinates(boss, target);
        _audio.PlayPvs(ChargeSound, boss);

        var damage = new DamageSpecifier { DamageDict = { { "Slash", 35f } } };
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(target, 0.6f))
        {
            if (mob.Owner == boss)
                continue;

            _damageable.TryChangeDamage(mob.Owner, damage, origin: boss);
            _stun.TryKnockdown(mob.Owner, TimeSpan.FromSeconds(0.5), force: true);
            _popup.PopupEntity(Loc.GetString("thing-boss-lunge"), mob.Owner, mob.Owner);
        }
    }

    // Shriek: everyone close by gets jittery and drops what they are holding.

    private void Shriek(Entity<ThingBossComponent> ent)
    {
        var coords = Transform(ent).Coordinates;
        SpawnTelegraphBox(coords, 2, TelegraphProto, 1.2f);
        var uid = ent.Owner;

        Timer.Spawn(TimeSpan.FromSeconds(1.2f), () =>
        {
            if (TerminatingOrDeleted(uid))
                return;

            _audio.PlayPvs(ShriekSound, uid);
            _popup.PopupEntity(Loc.GetString("thing-boss-shriek"), uid);

            foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(uid).Coordinates, 2f))
            {
                if (mob.Owner == uid)
                    continue;

                _jittering.DoJitter(mob.Owner, TimeSpan.FromSeconds(5), true);

                if (!TryComp<HandsComponent>(mob, out var hands))
                    continue;

                foreach (var hand in _hands.EnumerateHands((mob.Owner, hands)))
                    _hands.TryDrop((mob.Owner, hands), hand);
            }
        }, CancellationToken.None);
    }

    // Cardinal tendrils: four lines of spikes, stopped by walls.

    private void CardinalTendrils(Entity<ThingBossComponent> ent)
    {
        var origin = Transform(ent).Coordinates;
        var tiles = new List<EntityCoordinates>();

        foreach (var direction in new[] { Vector2.UnitY, Vector2.UnitX, -Vector2.UnitY, -Vector2.UnitX })
        {
            for (var i = 1; i <= 9; i++)
            {
                var tile = origin.Offset(direction * i);
                if (IsBlocked(tile))
                    break;

                tiles.Add(tile);
            }
        }

        foreach (var tile in tiles)
            Spawn(TelegraphProto, tile);

        var uid = ent.Owner;
        Timer.Spawn(TimeSpan.FromSeconds(1.5f), () =>
        {
            if (TerminatingOrDeleted(uid))
                return;

            _audio.PlayPvs(SpikeSound, Transform(uid).Coordinates);
            foreach (var tile in tiles)
                SpawnSpike(tile, uid);
        }, CancellationToken.None);
    }

    private bool IsBlocked(EntityCoordinates coords)
    {
        foreach (var anchored in _lookup.GetEntitiesInRange<TransformComponent>(coords, 0.1f))
        {
            if (_tags.HasTag(anchored.Owner, WallTag))
                return true;
        }

        return false;
    }

    // Big tendrils: a box of spikes around the target, bigger in the last phase.

    private void BigTendrils(Entity<ThingBossComponent> ent, EntityCoordinates target)
    {
        var big = ent.Comp.Phase >= ent.Comp.MaxPhases;
        var delay = big ? 2f : 1f;
        var radius = big ? 2 : 1;

        Spawn(big ? TelegraphLargeProto : TelegraphProto, target);
        var uid = ent.Owner;

        Timer.Spawn(TimeSpan.FromSeconds(delay), () =>
        {
            if (TerminatingOrDeleted(uid))
                return;

            _audio.PlayPvs(SpikeSound, target);
            SpawnSpikeBox(target, radius, uid);
        }, CancellationToken.None);
    }

    // Acid spit: acid globs fall around the boss.

    private void AcidSpit(Entity<ThingBossComponent> ent)
    {
        var origin = Transform(ent).Coordinates;
        _popup.PopupEntity(Loc.GetString("thing-boss-acid"), ent);

        var count = _random.Next(2, 5);
        for (var i = 0; i < count; i++)
        {
            var offset = new Vector2(_random.NextFloat(-4f, 4f), _random.NextFloat(-4f, 4f));
            Spawn("ThingAcidGlob", origin.Offset(offset));
        }
    }

    // Helpers.

    private void SpawnTelegraphBox(EntityCoordinates center, int radius, string proto, float lifetime)
    {
        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                var telegraph = Spawn(proto, center.Offset(new Vector2(x, y)));
                EnsureComp<TimedDespawnComponent>(telegraph).Lifetime = lifetime;
            }
        }
    }

    private void SpawnSpikeBox(EntityCoordinates center, int radius, EntityUid boss)
    {
        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
                SpawnSpike(center.Offset(new Vector2(x, y)), boss);
        }
    }

    private void SpawnSpike(EntityCoordinates coords, EntityUid boss)
    {
        foreach (var spike in _lookup.GetEntitiesInRange<ThingSpikeComponent>(coords, 0.1f))
        {
            if (!TerminatingOrDeleted(spike.Owner))
                return;
        }

        Spawn(SpikeProto, coords);

        // The spike itself is just a sprite with a collider, the hit happens right here.
        var damage = new DamageSpecifier { DamageDict = { { "Slash", 40f } } };
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 0.4f))
        {
            if (mob.Owner == boss || HasComp<MegafaunaAiComponent>(mob.Owner))
                continue;

            _damageable.TryChangeDamage(mob.Owner, damage, origin: boss);
            _popup.PopupEntity(Loc.GetString("thing-spike-hit"), mob.Owner, mob.Owner);
        }
    }
}