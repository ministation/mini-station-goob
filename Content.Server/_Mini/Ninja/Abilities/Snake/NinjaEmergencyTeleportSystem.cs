// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Numerics;
using Content.Server._Mini.Ninja.Abilities.Ghost;
using Content.Shared._Mini.Ninja.Abilities.Snake;
using Content.Shared._Mini.Ninja.Systems;
using Content.Shared.Maps;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;

namespace Content.Server._Mini.Ninja.Abilities.Snake;

/// <summary>
/// Snake tier 3: teleports the ninja to a random safe position in front of them.
/// </summary>
public sealed class NinjaEmergencyTeleportSystem : EntitySystem
{
    [Dependency] private readonly NinjaSuitPowerSystem _suitPower = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly NinjaGhostSmokeSystem _ghostSmoke = default!;

    private static readonly Vector2 CheckExtents = new(0.475f, 0.475f);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaEmergencyTeleportComponent, NinjaEmergencyTeleportEvent>(OnTeleport);
    }

    private void OnTeleport(Entity<NinjaEmergencyTeleportComponent> ent, ref NinjaEmergencyTeleportEvent args)
    {
        if (args.Handled)
            return;

        if (!_suitPower.TryGetActiveSuit(args.Performer, out _, out _))
            return;

        if (Transform(ent.Owner).ParentUid != args.Performer)
            return;

        // Do not teleport together with whatever the ninja is pulling or is pulled by.
        if (TryComp<PullableComponent>(args.Performer, out var pullable) && _pulling.IsPulled(args.Performer, pullable))
            _pulling.TryStopPull(args.Performer, pullable);

        if (TryComp<PullerComponent>(args.Performer, out var puller) &&
            TryComp<PullableComponent>(puller.Pulling, out var pulled))
        {
            _pulling.TryStopPull(puller.Pulling.Value, pulled);
        }

        if (SelectRandomTeleportPosition(args.Performer, ent.Comp.TeleportRadius) is not { } target)
            return;

        if (!_suitPower.TryUseCharge(args.Performer, ent.Comp.EnergyCost))
            return;

        // Synergy with the smoke module of the ghost path: the cloud covers the departure point.
        _ghostSmoke.TryAutoSmoke(ent.Owner);

        _audio.PlayPvs(ent.Comp.TeleportSound, args.Performer);
        _transform.SetCoordinates(args.Performer, target);
        args.Handled = true;
    }

    private EntityCoordinates? SelectRandomTeleportPosition(EntityUid uid, Vector2 radius, int tries = 80)
    {
        if (!TryComp<PhysicsComponent>(uid, out var physics))
            return null;

        var xform = Transform(uid);
        var origin = xform.Coordinates;
        var forward = xform.LocalRotation.ToWorldVec().Normalized();
        var side = new Vector2(-forward.Y, forward.X);
        var minDistance = MathF.Max(1f, radius.X);
        var maxDistance = MathF.Max(minDistance, radius.Y);
        var collisionMask = (CollisionGroup) physics.CollisionMask;

        return TryPick(minDistance, maxDistance)
               ?? (minDistance > 1f ? TryPick(1f, minDistance) : null);

        EntityCoordinates? TryPick(float min, float max)
        {
            for (var i = 0; i < tries; i++)
            {
                var distance = (max - min) * MathF.Sqrt(_random.NextFloat()) + min;
                var lateral = _random.NextFloat(-distance / 2f, distance / 2f);
                var candidate = origin.Offset(forward * distance + side * lateral);

                if (IsBlocked(candidate, collisionMask))
                    continue;

                if (_turf.TryGetTileRef(candidate, out var tileRef) && !tileRef.Value.Tile.IsEmpty)
                    return _turf.GetTileCenter(tileRef.Value);

                return candidate;
            }

            return null;
        }
    }

    private bool IsBlocked(EntityCoordinates candidate, CollisionGroup collisionMask)
    {
        if (_turf.TryGetTileRef(candidate, out var tileRef) && !tileRef.Value.Tile.IsEmpty)
            return _turf.IsTileBlocked(tileRef.Value, collisionMask);

        // Empty tile means space. Allow it only if nothing solid occupies the landing spot.
        var mapCoords = _transform.ToMapCoordinates(candidate);
        if (mapCoords.MapId == MapId.Nullspace)
            return true;

        var box = new Box2(mapCoords.Position - CheckExtents, mapCoords.Position + CheckExtents);

        foreach (var ent in _lookup.GetEntitiesIntersecting(mapCoords.MapId, box, LookupFlags.Dynamic | LookupFlags.Static))
        {
            if (HasComp<MapGridComponent>(ent))
                continue;

            if (!TryComp<FixturesComponent>(ent, out var fixtures))
                continue;

            var (pos, rot) = _transform.GetWorldPositionRotation(ent);
            var worldXform = new Transform(pos, (float) rot.Theta);

            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (!fixture.Hard)
                    continue;

                if ((fixture.CollisionLayer & (int) collisionMask) == 0)
                    continue;

                for (var i = 0; i < fixture.Shape.ChildCount; i++)
                {
                    if (fixture.Shape.ComputeAABB(worldXform, i).Intersects(box))
                        return true;
                }
            }
        }

        return false;
    }
}