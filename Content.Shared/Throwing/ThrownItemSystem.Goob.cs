using Content.Shared.Projectiles;

namespace Content.Shared.Throwing;

public sealed partial class ThrownItemSystem
{
    private readonly List<(EntityUid Thrown, EntityUid Target)> _deferredCollisions = new();

    private readonly List<(EntityUid Uid, TimeSpan? ThrownTime)> _deferredSleepStops = new();

    // Goob - PhysicsSleepEvent is raised while the engine is still putting the body to sleep.
    // Stopping the throw right there lets content damage, gib or delete the entity, which wakes
    // the body again mid-sleep and makes the engine log "Found a corrupted physics awake state".
    // Stop the throw after the physics step instead.
    private void DeferSleepStop(EntityUid uid, ThrownItemComponent thrownItem)
    {
        _deferredSleepStops.Add((uid, thrownItem.ThrownTime));
    }

    private void ProcessDeferredSleepStops()
    {
        if (_deferredSleepStops.Count == 0)
            return;

        foreach (var (uid, thrownTime) in _deferredSleepStops)
        {
            // The entity may have been deleted or thrown again in the meantime.
            if (!TryComp(uid, out ThrownItemComponent? thrownItem) || thrownItem.ThrownTime != thrownTime)
                continue;

            StopThrow(uid, thrownItem);
        }

        _deferredSleepStops.Clear();
    }

    private bool TryDeferCollision(EntityUid thrown, EntityUid target)
    {
        if (!_netMan.IsClient || !_gameTiming.ApplyingState)
            return false;

        _deferredCollisions.Add((thrown, target));
        return true;
    }

    private void ProcessDeferredCollisions()
    {
        if (_deferredCollisions.Count == 0)
            return;

        var collisions = _deferredCollisions.ToArray();
        _deferredCollisions.Clear();
        foreach (var (thrown, target) in collisions)
        {
            if (TerminatingOrDeleted(thrown) || TerminatingOrDeleted(target) ||
                !TryComp<ThrownItemComponent>(thrown, out var component) ||
                TryComp<EmbeddableProjectileComponent>(thrown, out var embed) && embed.EmbeddedIntoUid != null)
                continue;

            ThrowCollideInteraction(component, thrown, target);
        }
    }
}
