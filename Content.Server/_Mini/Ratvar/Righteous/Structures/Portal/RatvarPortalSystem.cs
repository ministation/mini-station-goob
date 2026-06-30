using System;
using Content.Server.RPSX.DarkForces.Ratvar.Righteous.Progress.Events;
using Content.Shared.Destructible;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Server.RPSX.DarkForces.Ratvar.Righteous.Structures.Portal;

public sealed class RatvarPortalSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    private static readonly TimeSpan DefaultSpawnDelay = TimeSpan.FromMinutes(3);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RatvarPortalComponent, DestructionEventArgs>(OnDestroy);
    }

    public void BeginSpawnCountdown(EntityUid uid, TimeSpan? delay = null)
    {
        var component = Comp<RatvarPortalComponent>(uid);
        component.RatvarSpawnTick = _gameTiming.CurTime + (delay ?? DefaultSpawnDelay);
        var ev = new RatvarSpawnStartedEvent(uid);
        RaiseLocalEvent(ref ev);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var curTime = _gameTiming.CurTime;
        var query = EntityQueryEnumerator<RatvarPortalComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.RatvarSpawnTick == TimeSpan.Zero || component.RatvarSpawnTick > curTime)
                continue;

            var ratvar = Spawn("MobRatvarSpawn", Transform(uid).Coordinates);
            var ev = new RatvarSpawnedEvent(ratvar);
            RaiseLocalEvent(ref ev);
            QueueDel(uid);
        }
    }

    private void OnDestroy(EntityUid uid, RatvarPortalComponent component, DestructionEventArgs args)
    {
        var ev = new RatvarSpawnCanceledEvent();
        RaiseLocalEvent(ref ev);
    }
}
