// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server._Lavaland.Ashwalker;
using Content.Server.Body.Systems;
using Content.Server.Destructible;
using Content.Shared._Lavaland.Ashwalker;
using Content.Shared._Lavaland.EntityShapes;
using Content.Shared._Lavaland.EntityShapes.Shapes;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Necropolis;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.Forensics.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Necropolis;

/// <summary>
/// Ashwalker nest from TG lavaland: consumes nearby corpses to grow meat,
/// heals itself and hatches ashwalker eggs (ghost roles) from the accumulated meat.
/// </summary>
public sealed class AshwalkerNestSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AshwalkerTribeSystem _tribe = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    private const string Egg = "LavalandAshwalkerEgg";
    // MobAshwalkerPlayable: no ghost role components, so re-attaching the salvaged mind
    // does not fire the ghost role takeover handler and stack duplicate job roles.
    private const string Ashwalker = "MobAshwalkerPlayable";
    private const string SalvagePending = "ashwalker-nest-salvage-pending";
    private const string SalvageReturn = "ashwalker-nest-salvage-return";

    /// <summary>
    /// TG: each sacrifice has a 40% chance to let its deliverer hatch from an egg again.
    /// </summary>
    private const float RespawnUnlockChance = 0.4f;

    /// <summary>
    /// Radius in which the tribe witnesses a sacrifice, matching TG's view(5).
    /// </summary>
    private const float SacrificeRadius = 5f;

    /// <summary>
    /// A walker this close to a corpse without matching fingerprints still counts as its deliverer.
    /// </summary>
    private const float DelivererFallbackRadius = 3f;

    /// <summary>
    /// TG drops a live anomaly core when the nest dies; the inert versions are the sane SS14 stand-in.
    /// </summary>
    private static readonly string[] AnomalyCores =
    {
        "AnomalyCorePyroclasticInert",
        "AnomalyCoreGravityInert",
        "AnomalyCoreRockInert",
        "AnomalyCoreIceInert",
        "AnomalyCoreBluespaceInert",
        "AnomalyCoreElectricityInert",
    };

    /// <summary>
    /// Dead ash walkers waiting for the soul salvage, with the time they are reborn.
    /// While pending they are not eaten as ordinary corpses.
    /// </summary>
    private readonly Dictionary<EntityUid, (TimeSpan At, EntityUid Nest)> _pendingSalvage = new();

    private TimeSpan _nextScan = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AshwalkerNestComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AshwalkerNestComponent, DestructionEventArgs>(OnDestroyed);
    }

    private void OnMapInit(Entity<AshwalkerNestComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Meat = ent.Comp.InitialMeat;
        _tribe.EnsureTribe(ent.Owner);
    }

    private void OnDestroyed(Entity<AshwalkerNestComponent> ent, ref DestructionEventArgs args)
    {
        _tribe.NotifyNestDestroyed(ent.Owner);
        var coords = Transform(ent.Owner).Coordinates;

        _popup.PopupCoordinates(Loc.GetString("ashwalker-nest-destroyed"), coords);

        // TG drops a random anomaly core. Spawn the loot outside the 5x5 that the chasm eats.
        Spawn(_random.Pick(AnomalyCores), coords.Offset(new Vector2(3f, 0f)));
        Spawn("MaterialBones1", coords.Offset(new Vector2(3f, 0.5f)));
        Spawn("MaterialBones1", coords.Offset(new Vector2(3f, -0.5f)));

        // TG: RANGE_TURFS(2, src) turns into a lavaland chasm.
        EntityManager.System<EntityShapeSystem>().SpawnEntityShape(
            new NestedEntityShape { Id = "BoxFilled", Size = 5 },
            coords,
            "FloorChasmOpeningSpawner",
            out _);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        RespawnSalvaged(now);

        if (now < _nextScan)
            return;
        _nextScan = now + TimeSpan.FromSeconds(2);

        var query = EntityQueryEnumerator<AshwalkerNestComponent>();
        while (query.MoveNext(out var uid, out var nest))
        {
            var nestXform = Transform(uid);

            // TG consumes everything unconscious or dead in a 3x3 area around the nest.
            foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(nestXform.Coordinates, nest.ConsumeRadius))
            {
                if (mob.Comp.CurrentState is not (MobState.Dead or MobState.Critical))
                    continue;

                // TG ignores mobs standing on the nest tile itself.
                var mobXform = Transform(mob.Owner);
                if ((_transform.GetWorldPosition(mobXform) - _transform.GetWorldPosition(nestXform)).LengthSquared() < 0.25f)
                    continue;

                if (_pendingSalvage.ContainsKey(mob.Owner))
                    continue;

                // A dead tribe member whose player is still attached is returned to the nest instead of eaten.
                if (mob.Comp.CurrentState == MobState.Dead
                    && HasComp<AshwalkerTribeMemberComponent>(mob.Owner)
                    && _mind.TryGetMind(mob.Owner, out _, out var mind)
                    && mind.UserId != null)
                {
                    _pendingSalvage[mob.Owner] = (now + TimeSpan.FromSeconds(nest.SalvageDelay), uid);
                    _popup.PopupEntity(Loc.GetString(SalvagePending), mob.Owner, mob.Owner);
                    continue;
                }

                nest.Meat += HasComp<MegafaunaAiComponent>(mob.Owner) ? nest.MeatPerMegafauna : nest.MeatPerCorpse;
                _tribe.RegisterSacrifice(uid);
                RewardDeliverer(uid, mob.Owner);
                CelebrateSacrifice(uid);

                // TG gibs with remains instead of silently deleting the body.
                _body.GibBody(mob.Owner, true);
                QueueDel(mob.Owner);

                HealNest(uid, nest);
            }

            // TG has no egg cap and hatches everything it can afford at once.
            while (nest.Meat >= nest.MeatPerEgg && (nest.MaxEggs <= 0 || nest.Eggs < nest.MaxEggs))
            {
                nest.Meat -= nest.MeatPerEgg;
                nest.Eggs++;
                _tribe.RegisterEgg(uid);
                var egg = Spawn(Egg, nestXform.Coordinates.Offset(_random.NextVector2Box(-1.5f, -1.5f, 1.5f, 1.5f)));
                AddComp<AshwalkerEggComponent>(egg).Nest = uid;
            }
        }
    }

    private void RespawnSalvaged(TimeSpan now)
    {
        if (_pendingSalvage.Count == 0)
            return;

        List<EntityUid>? finished = null;
        foreach (var (corpse, pending) in _pendingSalvage)
        {
            if (now < pending.At)
                continue;

            (finished ??= new()).Add(corpse);

            if (TerminatingOrDeleted(corpse))
                continue;

            // The player might have ghosted or disconnected while the body was in the nest.
            if (!_mind.TryGetMind(corpse, out var mindId, out _))
            {
                QueueDel(corpse);
                continue;
            }

            var nestCoords = TryComp<TransformComponent>(pending.Nest, out var nestXform)
                ? nestXform.Coordinates
                : Transform(corpse).Coordinates;
            var reborn = Spawn(Ashwalker, nestCoords.Offset(_random.NextVector2Box(-1f, -1f, 1f, 1f)));
            _metaData.SetEntityName(reborn, Name(corpse));
            _mind.TransferTo(mindId, reborn);
            _popup.PopupEntity(Loc.GetString(SalvageReturn), reborn, reborn);
            QueueDel(corpse);
        }

        if (finished == null)
            return;

        foreach (var corpse in finished)
            _pendingSalvage.Remove(corpse);
    }

    /// <summary>
    /// TG: a sacrificed corpse was usually dragged in by one of the tribe, and there is a 40%
    /// chance that the Necropolis repays the walker by clearing their one-spawn-per-ckey limit.
    /// </summary>
    private void RewardDeliverer(EntityUid nest, EntityUid corpse)
    {
        if (!TryComp<AshwalkerTribeComponent>(nest, out var tribe) || tribe.SpawnedCkeys.Count == 0)
            return;

        if (FindDeliverer(corpse) is not { } deliverer
            || !_mind.TryGetMind(deliverer, out _, out var mind)
            || mind.UserId is not { } ckey
            || !tribe.SpawnedCkeys.Contains(ckey)
            || !_random.Prob(RespawnUnlockChance))
            return;

        tribe.SpawnedCkeys.Remove(ckey);
        _popup.PopupEntity(Loc.GetString("ashwalker-sacrifice-respawn"), deliverer, deliverer);
    }

    /// <summary>
    /// The walker who brought the corpse in. TG reads the corpse's last fingerprints; the fork has
    /// no last-touch marker, so prefer a walker whose fingerprint is on the corpse, else the closest.
    /// shortcut: fingerprint evidence can be missing (gloves, throwing), then the nearest walker wins.
    /// </summary>
    private EntityUid? FindDeliverer(EntityUid corpse)
    {
        var corpseXform = Transform(corpse);
        var corpsePosition = corpseXform.WorldPosition;
        HashSet<string>? fingerprints = TryComp<ForensicsComponent>(corpse, out var forensics)
            ? forensics.Fingerprints
            : null;

        EntityUid? printed = null;
        EntityUid? nearest = null;
        var printedDistance = float.MaxValue;
        var nearestDistance = DelivererFallbackRadius * DelivererFallbackRadius;

        foreach (var member in _lookup.GetEntitiesInRange<AshwalkerTribeMemberComponent>(corpseXform.Coordinates, SacrificeRadius))
        {
            var distance = (Transform(member.Owner).WorldPosition - corpsePosition).LengthSquared();

            if (distance < printedDistance
                && fingerprints is { Count: > 0 }
                && TryComp<FingerprintComponent>(member.Owner, out var fingerprint)
                && fingerprint.Fingerprint is { } print
                && fingerprints.Contains(print))
            {
                printed = member.Owner;
                printedDistance = distance;
            }

            if (distance < nearestDistance)
            {
                nearest = member.Owner;
                nearestDistance = distance;
            }
        }

        return printed ?? nearest;
    }

    /// <summary>
    /// TG gives every walker in view a mood buff for each sacrifice. The fork has no mood system,
    /// so the same flavor line is shown as a popup instead.
    /// </summary>
    private void CelebrateSacrifice(EntityUid nest)
    {
        var message = Loc.GetString("ashwalker-sacrifice-good");
        foreach (var member in _lookup.GetEntitiesInRange<AshwalkerTribeMemberComponent>(Transform(nest).Coordinates, SacrificeRadius))
            _popup.PopupEntity(message, member.Owner, member.Owner);
    }

    private void HealNest(EntityUid uid, AshwalkerNestComponent nest)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return;

        var total = (float) damageable.TotalDamage;
        var max = GetMaxHealth(uid);
        if (total <= 0f || max <= 0f)
            return;

        var heal = max * nest.HealPerCorpse;
        if (heal > total)
            heal = total;

        _damageable.TryChangeDamage(uid, damageable.Damage * (-heal / total), true);
    }

    private float GetMaxHealth(EntityUid uid)
    {
        if (!TryComp<DestructibleComponent>(uid, out var destructible))
            return 0f;

        var max = FixedPoint2.Zero;
        foreach (var threshold in destructible.Thresholds)
        {
            if (threshold.Trigger is DamageTrigger trigger && trigger.Damage > max)
                max = trigger.Damage;
        }

        return (float) max;
    }
}
