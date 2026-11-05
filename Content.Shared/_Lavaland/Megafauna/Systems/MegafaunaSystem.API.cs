using System.Numerics;
using Content.Shared._Lavaland.Aggression;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Events;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Shared._Lavaland.Megafauna.Systems;

public sealed partial class MegafaunaSystem
{
    public void StartupMegafauna(Entity<MegafaunaAiComponent> ent)
    {
        RaiseLocalEvent(ent, new MegafaunaStartupEvent());
        ent.Comp.Active = true;
    }

    public void ShutdownMegafauna(Entity<MegafaunaAiComponent> ent)
    {
        RaiseLocalEvent(ent, new MegafaunaShutdownEvent());
        ent.Comp.Active = false;
    }

    public void KillMegafauna(Entity<MegafaunaAiComponent> ent)
    {
        RaiseLocalEvent(ent, new MegafaunaKilledEvent());
        ent.Comp.Active = false;
    }

    /// <summary>
    /// Helper method that constructs new <see cref="RequestPerformActionEvent"/> for megafauna AI to use an action.
    /// </summary>
    public RequestPerformActionEvent GetPerformEvent(EntityUid boss, EntityUid action)
    {
        var targetingComp = CompOrNull<MegafaunaAiTargetingComponent>(boss);

        if (targetingComp != null)
        {
            if (targetingComp.TargetEnt is { } targetEnt && TerminatingOrDeleted(targetEnt))
                targetingComp.TargetEnt = null;

            if (targetingComp.TargetCoords is { } targetCoords && TerminatingOrDeleted(targetCoords.EntityId))
                targetingComp.TargetCoords = null;
        }

        var netAction = GetNetEntity(action);

        // A plan can leave the targeting out (component gone, target died between selectors).
        // Passing null coordinates makes the action silently do nothing, so fall back to the boss
        // or its nearest aggressor: an attack in the wrong direction is still a working attack.
        NetEntity? netTarget = null;
        if (HasComp<EntityTargetActionComponent>(action))
        {
            var target = targetingComp?.TargetEnt;
            if (target is not { } valid || TerminatingOrDeleted(valid))
                target = FindFallbackTarget(boss);
            netTarget = GetNetEntity(target);
        }

        NetCoordinates? netCoords = null;
        if (HasComp<WorldTargetActionComponent>(action))
        {
            var coords = targetingComp?.TargetCoords;
            if (coords is not { } validCoords || !validCoords.IsValid(EntityManager))
                coords = Transform(FindFallbackTarget(boss)).Coordinates;
            netCoords = GetNetCoordinates(coords);
        }

        return new RequestPerformActionEvent(netAction, netTarget, netCoords);
    }

    /// <summary>
    /// Nearest living aggressor of the boss, or the boss itself when nobody is fighting it.
    /// Keeps the AI actions aimed at something valid instead of a null target.
    /// </summary>
    private EntityUid FindFallbackTarget(EntityUid boss)
    {
        if (!TryComp<AggressiveComponent>(boss, out var aggressive)
            || !TryComp<TransformComponent>(boss, out var bossXform))
            return boss;

        var bossPos = _xform.GetWorldPosition(bossXform);
        var best = boss;
        var bestDistance = float.MaxValue;

        foreach (var aggressor in aggressive.Aggressors)
        {
            if (TerminatingOrDeleted(aggressor)
                || !TryComp(aggressor, out TransformComponent? xform)
                || xform.MapID != bossXform.MapID)
                continue;

            var distance = (_xform.GetWorldPosition(xform) - bossPos).LengthSquared();
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = aggressor;
        }

        return best;
    }

    public void PickRandomPosition(MegafaunaCalculationBaseArgs args, float radius)
    {
        // TODO add an option to not pick any obstructed coordinates

        var uid = args.Entity;
        var mapId = Transform(uid).MapID;

        var randomVector = new Vector2(args.Random.NextFloat(-radius, radius), args.Random.NextFloat(-radius, radius));
        var position = _xform.GetWorldPosition(uid) + randomVector;
        var newMapCoords = new MapCoordinates(position, mapId);
        var coords = _xform.ToCoordinates(newMapCoords);

        var comp = EnsureComp<MegafaunaAiTargetingComponent>(args.Entity);
        comp.TargetEnt = null;
        comp.TargetCoords = coords;
    }
}
