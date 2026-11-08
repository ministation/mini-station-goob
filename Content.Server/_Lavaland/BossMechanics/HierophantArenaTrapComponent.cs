// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Lavaland.Aggression;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Systems;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// TG parity: the hierophant periodically re-creates its arena around the target
/// wherever they fled to. Walls last ten seconds and a blast marks the whole ring.
/// </summary>
[RegisterComponent]
public sealed partial class HierophantArenaTrapComponent : Component
{
    /// <summary>
    /// The trap ring radius. TG uses a ring of range 11 around the target.
    /// </summary>
    [DataField]
    public int TrapRadius = 11;

    /// <summary>
    /// Time between traps. TG arena_cooldown_time is 20 seconds.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(20);

    /// <summary>
    /// How long the trap walls stand. TG wall duration is 10 seconds.
    /// </summary>
    [DataField]
    public TimeSpan WallDuration = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The hierophant blinks to the trapped target from at least this far away.
    /// </summary>
    [DataField]
    public int BlinkDistance = 11;

    [DataField]
    public EntProtoId WallProto = "WallHierophantArenaTrap";

    [DataField]
    public EntProtoId SquareProto = "LavalandHierophantSquare";

    [ViewVariables]
    public TimeSpan NextTrap;

    [ViewVariables]
    public TimeSpan NextCheck;
}

public sealed class HierophantArenaTrapSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly MegafaunaBlinkSystem _blink = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<HierophantArenaTrapComponent, AggressiveComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var trap, out var aggressive, out var xform))
        {
            if (now < trap.NextCheck)
                continue;

            trap.NextCheck = now + TimeSpan.FromSeconds(0.5);
            if (now < trap.NextTrap)
                continue;

            var target = ClosestLivingAggressor(uid, aggressive, xform);
            if (target == null)
                continue;

            Trap(uid, trap, target.Value, now);
        }
    }

    private EntityUid? ClosestLivingAggressor(EntityUid boss, AggressiveComponent aggressive, TransformComponent xform)
    {
        EntityUid? closest = null;
        var closestDist = float.MaxValue;
        foreach (var aggressor in aggressive.Aggressors)
        {
            if (!_mobState.IsAlive(aggressor))
                continue;

            var dist = (Transform(aggressor).Coordinates.Position - xform.Coordinates.Position).LengthSquared();
            if (dist < closestDist)
            {
                closest = aggressor;
                closestDist = dist;
            }
        }

        return closest;
    }

    private void Trap(
        EntityUid boss,
        HierophantArenaTrapComponent trap,
        EntityUid target,
        TimeSpan now)
    {
        trap.NextTrap = now + trap.Cooldown;

        var bossPos = _xform.GetMapCoordinates(boss);
        var targetPos = _xform.GetMapCoordinates(target);
        var targetCoords = Transform(target).Coordinates;
        if (_xform.GetGrid(targetCoords) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var targetTile = _map.LocalToTile(gridUid, grid, targetCoords);
        var radius = trap.TrapRadius;

        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                if (Math.Max(Math.Abs(x), Math.Abs(y)) != radius)
                    continue;

                var ringTile = targetTile + new Vector2i(x, y);
                var tileCoords = _map.GridTileToLocal(gridUid, grid, ringTile);

                // TG leaves existing hierophant walls alone; skip tiles that already hold one.
                var blockedByWall = false;
                foreach (var ent in _lookup.GetEntitiesIntersecting(tileCoords))
                {
                    var protoId = Prototype(ent)?.ID;
                    if (protoId != null && protoId == trap.WallProto.Id)
                        blockedByWall = true;
                }

                if (!blockedByWall)
                {
                    Spawn(trap.WallProto, tileCoords);
                    Spawn(trap.SquareProto, tileCoords);
                }
            }
        }

        // TG: "hey you're out of range I need to get closer to you!"
        var bossCoords = Transform(boss).Coordinates;
        var cheb = Math.Max(
            Math.Abs(bossPos.X - targetPos.X),
            Math.Abs(bossPos.Y - targetPos.Y));
        if (cheb >= trap.BlinkDistance && TryComp<MegafaunaBlinkComponent>(boss, out var blink))
        {
            _blink.Blink(boss, targetCoords, blink.Delay, blink.Sound);
        }
    }
}
