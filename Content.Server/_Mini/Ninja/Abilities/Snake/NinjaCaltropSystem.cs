// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Numerics;
using Content.Server._Mini.Ninja.Abilities.Ghost;
using Content.Shared._Mini.Ninja.Abilities.Snake;
using Content.Shared._Mini.Ninja.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Station;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;
using Content.Shared.Trigger;

namespace Content.Server._Mini.Ninja.Abilities.Snake;

/// <summary>
/// Snake tier 4: places a single permanent energy caltrop behind the ninja.
/// No more than a few traps can be active on one station, and every third trap puts the ability on cooldown.
/// </summary>
public sealed partial class NinjaCaltropSystem : EntitySystem
{
    private const string CaltropActionProto = "NinjaCaltropAction";

    [Dependency] private readonly NinjaSuitPowerSystem _suitPower = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly NinjaGhostSmokeSystem _ghostSmoke = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaCaltropComponent, NinjaCaltropAbilityEvent>(OnPlaceCaltrop);
        SubscribeLocalEvent<NinjaCaltropTrapComponent, TriggerEvent>(OnTrapTriggered);
    }

    private void OnPlaceCaltrop(Entity<NinjaCaltropComponent> ent, ref NinjaCaltropAbilityEvent args)
    {
        if (args.Handled)
            return;

        if (!_suitPower.TryGetActiveSuit(args.Performer, out _, out _))
            return;

        if (Transform(ent.Owner).ParentUid != args.Performer)
            return;

        args.Handled = true;

        var comp = ent.Comp;
        var now = _timing.CurTime;

        if (CountStationTraps(args.Performer) >= comp.MaxTraps)
        {
            _popup.PopupEntity(Loc.GetString("ninja-caltrop-limit"), args.Performer, args.Performer,
                PopupType.MediumCaution);
            return;
        }

        if (!TryGetPlacement(args.Performer, comp, out var position))
        {
            _popup.PopupEntity(Loc.GetString("ninja-caltrop-no-room"), args.Performer, args.Performer,
                PopupType.MediumCaution);
            return;
        }

        if (!_suitPower.TryUseCharge(args.Performer, comp.EnergyCost))
            return;

        Spawn(comp.CaltropProto, position);

        // Synergy with the smoke module of the ghost path: the cloud hides the traps.
        _ghostSmoke.TryAutoSmoke(ent.Owner);

        comp.PlacedInBatch++;

        if (comp.PlacedInBatch < comp.MaxTraps || comp.RechargeEnds > now)
            return;

        // The third trap starts the recharge. The action cooldown is applied in Update: the action system
        // overwrites the cooldown with the base use delay right after this event is handled.
        comp.RechargeEnds = now + comp.RechargeDelay;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<NinjaCaltropComponent>();

        while (query.MoveNext(out var suit, out var comp))
        {
            if (comp.PlacedInBatch == 0)
                continue;

            // A batch only starts its recharge after the third trap; until then RechargeEnds is zero.
            if (comp.RechargeEnds != TimeSpan.Zero && now >= comp.RechargeEnds)
            {
                // The recharge is over: the next batch starts from a clean count.
                comp.PlacedInBatch = 0;
                comp.RechargeEnds = TimeSpan.Zero;
                continue;
            }

            if (comp.PlacedInBatch < comp.MaxTraps)
                continue;

            if (FindCaltropAction(suit) is { } action)
                ApplyRechargeCooldown(action, comp.RechargeEnds);
        }
    }

    private void ApplyRechargeCooldown(EntityUid action, TimeSpan end)
    {
        if (!TryComp<ActionComponent>(action, out var actionComp))
            return;

        if (actionComp.Cooldown is { } cooldown && cooldown.End >= end)
            return;

        _actions.SetCooldown(action, _timing.CurTime, end);
    }

    private void OnTrapTriggered(Entity<NinjaCaltropTrapComponent> ent, ref TriggerEvent args)
    {
        // The trap burns out after the first trigger, the station limit frees up.
        QueueDel(ent.Owner);
    }

    /// <summary>
    /// Number of active traps on the station of the wearer, or on the same grid when the wearer is not on a station.
    /// </summary>
    private int CountStationTraps(EntityUid wearer)
    {
        var station = _station.GetOwningStation(wearer);
        var grid = Transform(wearer).GridUid;
        var count = 0;

        var query = EntityQueryEnumerator<NinjaCaltropTrapComponent, TransformComponent>();
        while (query.MoveNext(out var trap, out _, out var xform))
        {
            if (station != null)
            {
                if (_station.GetOwningStation(trap, xform) == station)
                    count++;

                continue;
            }

            if (grid != null && xform.GridUid == grid)
                count++;
        }

        return count;
    }

    private bool TryGetPlacement(EntityUid wearer, NinjaCaltropComponent comp, out EntityCoordinates position)
    {
        var xform = Transform(wearer);
        var backwards = -xform.LocalRotation.ToWorldVec();

        var behind = xform.Coordinates.Offset(backwards * comp.SpawnDistance).SnapToGrid();
        if (IsTileFree(behind))
        {
            position = behind;
            return true;
        }

        var ahead = xform.Coordinates.Offset(-backwards * comp.SpawnDistance).SnapToGrid();
        if (IsTileFree(ahead))
        {
            position = ahead;
            return true;
        }

        position = default;
        return false;
    }

    private bool IsTileFree(EntityCoordinates coords)
    {
        if (!_turf.TryGetTileRef(coords, out var tileRef))
            return false;

        var entities = new HashSet<EntityUid>();
        _turf.GetEntitiesInTile(coords, entities);

        foreach (var uid in entities)
        {
            if (!TryComp<PhysicsComponent>(uid, out var physics))
                continue;

            if (physics.BodyType != BodyType.Static || !physics.Hard)
                continue;

            if ((physics.CollisionLayer & (int) CollisionGroup.Impassable) == 0)
                continue;

            return false;
        }

        return true;
    }

    private EntityUid? FindCaltropAction(EntityUid suit)
    {
        if (!TryComp<ActionsContainerComponent>(suit, out var container))
            return null;

        foreach (var action in container.Container.ContainedEntities)
        {
            if (MetaData(action).EntityPrototype?.ID == CaltropActionProto)
                return action;
        }

        return null;
    }
}
