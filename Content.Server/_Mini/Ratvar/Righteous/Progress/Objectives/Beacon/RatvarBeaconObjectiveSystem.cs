// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

﻿using System.Linq;
using Content.Server.RPSX.DarkForces.Ratvar.Righteous.Progress.Objectives.Convert;
using Content.Server.RPSX.DarkForces.Ratvar.Righteous.Structures.Beacon;
using Content.Shared.Objectives.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Random;

namespace Content.Server.RPSX.DarkForces.Ratvar.Righteous.Progress.Objectives.Beacon;

public sealed class RatvarBeaconObjectiveSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RatvarBeaconObjectiveComponent, ObjectiveAssignedEvent>(OnAssigned);
        SubscribeLocalEvent<RatvarBeaconObjectiveComponent, ObjectiveAfterAssignEvent>(OnAfterAssigned);
        SubscribeLocalEvent<RatvarBeaconObjectiveComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, RatvarBeaconObjectiveComponent component, ref ObjectiveGetProgressEvent args)
    {
        var query = EntityQuery<RatvarBeaconComponent>();
        var progress = query.Count() / component.RequiredCount;
        if (progress >= 1f)
        {
            progress = 1f;
        }

        args.Progress = progress;
    }

    private void OnAfterAssigned(EntityUid uid, RatvarBeaconObjectiveComponent component,
        ref ObjectiveAfterAssignEvent args)
    {
        _metaData.SetEntityName(uid,
            $"Необходимо накопить мощь, чтобы выпустить Ратвара из тюрьмы. Постройте {component.RequiredCount} маяков");
    }

    private void OnAssigned(EntityUid uid, RatvarBeaconObjectiveComponent component, ref ObjectiveAssignedEvent args)
    {
        component.RequiredCount = _robustRandom.Next(4, 7);
    }
}
