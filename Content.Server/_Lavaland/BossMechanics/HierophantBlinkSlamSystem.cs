// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Events;
using Content.Shared._Lavaland.Megafauna.Systems;
using Content.Shared.Coordinates.Helpers;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// Handles <see cref="HierophantBlinkSlamComponent"/>: randomizes the blink destination
/// around the target and drops the slam spawner there after the teleport.
/// </summary>
public sealed class HierophantBlinkSlamSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MegafaunaBlinkSystem _blink = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HierophantBlinkSlamComponent, MegafaunaBlinkActionEvent>(OnBlinkAction);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<HierophantBlinkSlamComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            for (var i = comp.Pending.Count - 1; i >= 0; i--)
            {
                if (comp.Pending[i].Time > now)
                    continue;

                SpawnAtPosition(comp.Slam, comp.Pending[i].Coords);
                comp.Pending.RemoveAt(i);
            }
        }
    }

    private void OnBlinkAction(Entity<HierophantBlinkSlamComponent> ent, ref MegafaunaBlinkActionEvent args)
    {
        if (!TryComp<MegafaunaBlinkComponent>(ent, out var blink))
            return;

        var landing = args.Target.Offset(_random.NextVector2(ent.Comp.Radius)).SnapToGrid(EntityManager);

        // Both this system and MegafaunaBlinkSystem handle this event and their order is not
        // guaranteed, so write the destination twice: here for the case the blink already ran,
        // and into the event for the case it has not.
        args.Target = landing;
        _blink.Blink(ent.Owner, landing, blink.Delay, blink.Sound);

        ent.Comp.Pending.Add((_timing.CurTime + TimeSpan.FromSeconds(ent.Comp.SlamDelay), landing));
    }
}
