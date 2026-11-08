// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

﻿using System.Collections.Generic;
using Content.Server.Pinpointer;
using Content.Server.Station.Components;
using Content.Shared.Ninja.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Warps;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Server.RPSX.DarkForces.Ratvar.Righteous.Progress.Events;

namespace Content.Server.RPSX.DarkForces.Ratvar.Righteous.Progress.Objectives.Summon;

public sealed class RatvarSummonObjectiveSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RatvarSummonObjectiveComponent, ObjectiveAssignedEvent>(OnAssigned);
        SubscribeLocalEvent<RatvarSummonObjectiveComponent, ObjectiveAfterAssignEvent>(OnAfterAssigned);
        SubscribeLocalEvent<RatvarSummonObjectiveComponent, ObjectiveGetProgressEvent>(OnGetProgress);

        SubscribeLocalEvent<RatvarSpawnedEvent>(OnRatvarSpawned);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<RatvarSummonObjectiveComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var component, out var meta))
        {
            if (component.Target is not { } target || TerminatingOrDeleted(target))
            {
                if (component.Target != null)
                    component.Target = null;

                continue;
            }

            if (component.UpdateCoordinatesTime > time)
                continue;

            UpdateSummonObjectiveText(uid, component, meta);
            component.UpdateCoordinatesTime = time + component.UpdateCoordinatesPeriod;
        }
    }

    private void OnRatvarSpawned(ref RatvarSpawnedEvent ev)
    {
        var query = EntityQueryEnumerator<RatvarSummonObjectiveComponent>();
        while (query.MoveNext(out _, out var component))
        {
            component.IsCompleted = true;
        }
    }

    private void OnGetProgress(EntityUid uid, RatvarSummonObjectiveComponent component,
        ref ObjectiveGetProgressEvent args)
    {
        args.Progress = component.IsCompleted ? 1f : 0f;
    }

    private void OnAfterAssigned(EntityUid uid, RatvarSummonObjectiveComponent component,
        ref ObjectiveAfterAssignEvent args)
    {
        if (component.Target == null)
            return;

        component.UpdateCoordinatesTime = _timing.CurTime;
        UpdateSummonObjectiveText(uid, component, MetaData(uid));
    }

    private void OnAssigned(EntityUid uid, RatvarSummonObjectiveComponent component,
        ref ObjectiveAssignedEvent args)
    {
        TryAssignTarget(uid, component);
    }

    public void TryAssignTarget(EntityUid uid, RatvarSummonObjectiveComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.Target != null)
            return;

        var warps = new List<EntityUid>();
        var query = EntityQueryEnumerator<BombingTargetComponent, WarpPointComponent>();
        while (query.MoveNext(out var warpUid, out _, out _))
        {
            warps.Add(warpUid);
        }

        if (warps.Count > 0)
        {
            component.Target = _random.Pick(warps);
            return;
        }

        warps.Clear();
        var queryWarps = EntityQueryEnumerator<WarpPointComponent>();
        while (queryWarps.MoveNext(out var warpUid, out _))
        {
            if (!HasComp<BecomesStationComponent>(Transform(warpUid).GridUid))
                continue;

            warps.Add(warpUid);
        }

        if (warps.Count > 0)
            component.Target = _random.Pick(warps);
    }

    private void UpdateSummonObjectiveText(EntityUid uid, RatvarSummonObjectiveComponent component, MetaDataComponent meta)
    {
        if (component.Target is not { } target || TerminatingOrDeleted(target))
        {
            component.Target = null;
            return;
        }

        var location = GetSummonLocationName(target);
        _metaData.SetEntityName(uid, Loc.GetString("objective-title-RatvarSummonObjective"), meta);
        _metaData.SetEntityDescription(uid,
            Loc.GetString("ratvar-summon-objective-location", ("location", location)),
            meta);
    }

    private string GetSummonLocationName(EntityUid target)
    {
        if (TerminatingOrDeleted(target) || !TryComp<TransformComponent>(target, out var xform))
            return Loc.GetString("nav-beacon-pos-no-beacons");

        var coordinates = _transform.GetMapCoordinates(target, xform);
        var beaconName = FormattedMessage.RemoveMarkupPermissive(_navMap.GetNearestBeaconString(coordinates, onlyName: true));
        if (!string.IsNullOrWhiteSpace(beaconName) &&
            beaconName != Loc.GetString("nav-beacon-pos-no-beacons"))
        {
            return beaconName;
        }

        if (TryComp<WarpPointComponent>(target, out var warp) && !string.IsNullOrWhiteSpace(warp.Location))
            return warp.Location;

        return Loc.GetString("nav-beacon-pos-no-beacons");
    }
}
