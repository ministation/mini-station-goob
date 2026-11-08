// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared._Lavaland.Aggression;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Selectors;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Lavaland.Megafauna.Systems;

public sealed partial class MegafaunaSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private EntityQuery<AggressiveComponent> _aggressiveQuery;

    protected override string SawmillName => "megafauna";

    public override void Initialize()
    {
        base.Initialize();

        InitializeHandle();

        _aggressiveQuery = GetEntityQuery<AggressiveComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // We are not ready to predict this thing... Probably never are.
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<MegafaunaAiComponent>();
        while (query.MoveNext(out var uid, out var ai))
        {
            if (!ai.Active)
                continue;

            // A player (or ghost takeover) controls this boss: AI steps aside.
            if (HasComp<ActorComponent>(uid))
                continue;

            // TODO when there's more than just hierophant make this a CPU job or make it parallel idk whatever is faster
            var selectors = new Dictionary<TimeSpan, MegafaunaSelector>(ai.Schedule);
            foreach (var (time, action) in selectors)
            {
                if (time > _timing.CurTime)
                    continue;

                var args = new MegafaunaCalculationBaseArgs(uid, EntityManager, _protoMan, Log, _random);
                var actionTime = action.Invoke(args);
                ai.Schedule.Remove(time);

                // Add next action to this thread. Two selectors due on the same tick can compute
                // the same next time; nudge one by a tick so both branches keep attacking instead
                // of one being dropped (or Add throwing) and the boss going quiet.
                actionTime = Math.Max(Math.Abs(actionTime), 0.02f);
                var delayTime = ai.ActionDelaySelector.Get(args);
                var nextAction = _timing.CurTime + TimeSpan.FromSeconds(actionTime + delayTime);
                while (!ai.Schedule.TryAdd(nextAction, ai.Selector))
                {
                    nextAction += TimeSpan.FromMilliseconds(1);
                }
            }
        }
    }
}
