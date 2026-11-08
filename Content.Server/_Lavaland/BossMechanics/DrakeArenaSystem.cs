// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Events;
using Robust.Shared.Player;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
///     Ash drake's lava arena (TG lava_swoop enraged swoop): fire walls encase the
///     target, waves of lava flood the interior leaving safe tiles a step away from
///     each player, and players who escape the arena enrage the drake — it heals,
///     speeds up and answers with a mass fire burst.
/// </summary>
public sealed class DrakeArenaSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<DrakeArenaComponent, MegafaunaLavaArenaActionEvent>(OnArenaAction);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DrakeArenaComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out _))
        {
            // Escape punishment: restore speed, then answer with a mass fire burst.
            if (comp.PunishPending)
            {
                if (now >= comp.SpeedRestoreAt)
                    RestoreSpeed((uid, comp));

                if (now >= comp.PunishAt)
                {
                    comp.PunishPending = false;

                    // TG fires 5 seconds later at wherever the target ran to.
                    var pos = comp.PunishTarget is { } target && !TerminatingOrDeleted(target)
                        ? _xform.GetMapCoordinates(target)
                        : _xform.ToMapCoordinates(comp.Center);
                    if (pos.MapId != MapId.Nullspace)
                        Spawn(comp.PunishBurstId, pos);
                }
            }

            if (!comp.Active || now < comp.NextWaveAt)
                continue;

            if (comp.WavesLeft > 0)
            {
                RunWave((uid, comp));
                comp.WavesLeft--;
                comp.NextWaveAt = now + TimeSpan.FromSeconds(comp.WaveDelay);
            }
            else
            {
                FinishArena((uid, comp));
            }
        }
    }

    private void OnArenaAction(Entity<DrakeArenaComponent> ent, ref MegafaunaLavaArenaActionEvent args)
    {
        if (ent.Comp.Active)
            return;

        StartArena(ent, args.Target);
    }

    private void StartArena(Entity<DrakeArenaComponent> ent, EntityCoordinates target)
    {
        var comp = ent.Comp;
        var gridUid = target.GetGridUid(EntityManager);
        if (gridUid == null || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        comp.Active = true;
        comp.WavesLeft = comp.Waves;
        comp.NextWaveAt = _timing.CurTime + TimeSpan.FromSeconds(comp.StartDelay);
        comp.Center = target;
        comp.Victims.Clear();
        comp.SafeTiles.Clear();

        // TG walls are plain entities on turfs: a dense ring around the arena center.
        var center = _map.TileIndicesFor(gridUid.Value, grid, target);
        for (var dx = -comp.WallRing; dx <= comp.WallRing; dx++)
        {
            for (var dy = -comp.WallRing; dy <= comp.WallRing; dy++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != comp.WallRing)
                    continue;

                var tile = new Vector2i(center.X + dx, center.Y + dy);
                if (!_map.TryGetTileRef(gridUid.Value, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
                    continue;

                Spawn(comp.WallId, _map.GridTileToLocal(gridUid.Value, grid, tile));
            }
        }

        _popup.PopupEntity(Loc.GetString("drake-arena-start", ("boss", ent.Owner)), ent.Owner, PopupType.MediumCaution);
    }

    private void RunWave(Entity<DrakeArenaComponent> ent)
    {
        var comp = ent.Comp;
        var gridUid = comp.Center.GetGridUid(EntityManager);
        if (gridUid == null || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var center = _map.TileIndicesFor(gridUid.Value, grid, comp.Center);

        // Player-controlled victims inside the arena: they are the ones worth punishing.
        var interiorMobs = new List<EntityUid>();
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(comp.Center, comp.InteriorRange + 0.5f))
        {
            if (!HasComp<ActorComponent>(mob.Owner))
                continue;

            interiorMobs.Add(mob.Owner);
            if (!comp.Victims.Contains(mob.Owner))
                comp.Victims.Add(mob.Owner);
        }

        PickSafeTiles(comp, gridUid.Value, grid, center, interiorMobs);

        var flooded = 0;
        for (var dx = -comp.InteriorRange; dx <= comp.InteriorRange; dx++)
        {
            for (var dy = -comp.InteriorRange; dy <= comp.InteriorRange; dy++)
            {
                var tile = new Vector2i(center.X + dx, center.Y + dy);
                if (!_map.TryGetTileRef(gridUid.Value, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
                    continue;

                // TG leaves one safe tile per player, a step away from them: you have to
                // move to survive the wave instead of standing still.
                if (comp.SafeTiles.Contains(tile))
                    continue;

                Spawn(comp.PoolId, _map.GridTileToLocal(gridUid.Value, grid, tile));
                flooded++;
            }
        }

        // TG ends the arena early when nobody cliented is inside anymore.
        if (flooded == 0 && interiorMobs.Count == 0)
            comp.WavesLeft = 0;
    }

    private void PickSafeTiles(
        DrakeArenaComponent comp,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i center,
        List<EntityUid> interiorMobs)
    {
        comp.SafeTiles.Clear();

        var claimed = new HashSet<Vector2i>();
        foreach (var mob in interiorMobs)
        {
            var mobTile = _map.LocalToTile(gridUid, grid, Comp<TransformComponent>(mob).Coordinates);

            var candidates = new List<Vector2i>();
            for (var dx = -comp.InteriorRange; dx <= comp.InteriorRange; dx++)
            {
                for (var dy = -comp.InteriorRange; dy <= comp.InteriorRange; dy++)
                {
                    var tile = new Vector2i(center.X + dx, center.Y + dy);
                    var chebyshev = Math.Max(Math.Abs(tile.X - mobTile.X), Math.Abs(tile.Y - mobTile.Y));
                    if (chebyshev < 2 || claimed.Contains(tile))
                        continue;

                    candidates.Add(tile);
                }
            }

            // No free tile a step away: the victim's own tile is the mercy.
            var safe = candidates.Count == 0 ? mobTile : _random.Pick(candidates);
            claimed.Add(safe);
            comp.SafeTiles.Add(safe);
        }
    }

    private void FinishArena(Entity<DrakeArenaComponent> ent)
    {
        var comp = ent.Comp;
        comp.Active = false;

        // Tracked victims that are still alive, controlled and outside the arena escaped
        // through the fire: the drake punishes that (TG COMSIG_LAVA_ARENA_FAILED).
        var center = _xform.ToMapCoordinates(comp.Center);
        foreach (var victim in comp.Victims)
        {
            if (TerminatingOrDeleted(victim)
                || _mobState.IsDead(victim)
                || !TryComp<ActorComponent>(victim, out var actor)
                || actor.PlayerSession?.AttachedEntity != victim)
            {
                continue;
            }

            var pos = _xform.GetMapCoordinates(victim);
            if ((pos.Position - center.Position).Length() <= comp.InteriorRange + 1)
                continue;

            Punish(ent, victim);
            break; // One punishment per arena, like TG's single enraged follow-up.
        }

        comp.Victims.Clear();
    }

    private void Punish(Entity<DrakeArenaComponent> ent, EntityUid escapee)
    {
        var comp = ent.Comp;
        comp.PunishPending = true;
        comp.PunishAt = _timing.CurTime + TimeSpan.FromSeconds(comp.PunishDuration);
        comp.SpeedRestoreAt = comp.PunishAt;
        comp.PunishTarget = escapee;

        if (comp.PunishHeal > FixedPoint2.Zero)
        {
            var heal = new DamageSpecifier();
            heal.DamageDict["Blunt"] = -comp.PunishHeal;
            _damageable.TryChangeDamage(ent.Owner, heal, true);
        }

        if (TryComp<MovementSpeedModifierComponent>(ent, out var speed))
        {
            comp.BaseWalkSpeed = speed.BaseWalkSpeed;
            comp.BaseSprintSpeed = speed.BaseSprintSpeed;
            _speed.ChangeBaseSpeed(ent.Owner,
                speed.BaseWalkSpeed * comp.PunishSpeedMultiplier,
                speed.BaseSprintSpeed * comp.PunishSpeedMultiplier,
                speed.Acceleration);
        }

        _popup.PopupEntity(Loc.GetString("drake-arena-escape", ("boss", ent.Owner)), ent.Owner, PopupType.LargeCaution);
    }

    private void RestoreSpeed(Entity<DrakeArenaComponent> ent)
    {
        if (!TryComp<MovementSpeedModifierComponent>(ent, out var speed))
            return;

        _speed.ChangeBaseSpeed(ent.Owner, ent.Comp.BaseWalkSpeed, ent.Comp.BaseSprintSpeed, speed.Acceleration);
    }
}
