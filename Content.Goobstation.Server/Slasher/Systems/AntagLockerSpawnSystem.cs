using Content.Goobstation.Server.Antag;
using Content.Goobstation.Server.Slasher.Components;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Systems;
using Content.Server.Storage.EntitySystems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Goobstation.Server.Slasher.Systems;

/// <summary>
/// Handles placing the antag ghost-role spawner inside a random station locker.
/// Works with AntagSelection.
///
/// Mini: location must be chosen in Added (before token auto-join),
/// not deferred to ActiveTick — otherwise Slasher/Myasnik spawns in nullspace.
/// </summary>
public sealed partial class AntagLockerSpawnSystem : GameRuleSystem<AntagLockerSpawnComponent>
{
    private static readonly ProtoId<TagPrototype> MaintenanceClosetTag = "MaintenanceCloset";

    [Dependency] private AntagBetterRandomSpawnSystem _betterSpawn = default!;
    [Dependency] private EntityStorageSystem _entityStorage = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StationSystem _stationSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AntagLockerSpawnComponent, AntagSelectLocationEvent>(OnSelectLocation);
        SubscribeLocalEvent<AntagLockerSpawnComponent, AfterAntagEntitySelectedEvent>(OnAntagSelected);
    }

    /// <summary>
    /// Pick locker / fallback coords as soon as the rule is added so token auto-join
    /// (Timer.Spawn(0) after ghost-role register) already has a valid spawn location.
    /// </summary>
    protected override void Added(EntityUid uid, AntagLockerSpawnComponent comp, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, comp, gameRule, args);
        EnsureLocation(comp);
    }

    private void OnSelectLocation(Entity<AntagLockerSpawnComponent> ent, ref AntagSelectLocationEvent args)
    {
        // Location must apply for both ghost-spawner setup and player takeover.
        // Token auto-join previously raced ActiveTick and left bodies in nullspace.
        EnsureLocation(ent.Comp);

        if (ent.Comp.ChosenLocker is { } locker && Exists(locker))
            args.Coordinates.Add(_transform.GetMapCoordinates(locker));
        else if (ent.Comp.FallbackCoords is { } fallback)
            args.Coordinates.Add(_transform.ToMapCoordinates(fallback));
    }

    private void OnAntagSelected(Entity<AntagLockerSpawnComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (args.Session == null)
            return;

        EnsureLocation(ent.Comp);

        // Fallback mode: no locker was found, player is teleported via OnSelectLocation.
        if (ent.Comp.ChosenLocker is not { } locker || !Exists(locker))
            return;

        if (!TryComp<EntityStorageComponent>(locker, out var storage))
            return;

        _entityStorage.Insert(args.EntityUid, locker, storage);
    }

    protected override void ActiveTick(EntityUid uid, AntagLockerSpawnComponent comp, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, comp, gameRule, frameTime);

        if (comp.Placed)
            return;

        EnsureLocation(comp);

        EntityUid? spawnerEnt = null;
        var spawnerQuery = EntityQueryEnumerator<GhostRoleAntagSpawnerComponent>();
        while (spawnerQuery.MoveNext(out var spawner, out var spawnerComp))
        {
            if (spawnerComp.Rule != uid)
                continue;

            spawnerEnt = spawner;
            break;
        }

        // Spawner may already be gone after instant token takeover — location was set in Added.
        if (spawnerEnt == null)
            return;

        if (comp.ChosenLocker is { } locker && Exists(locker)
            && TryComp<EntityStorageComponent>(locker, out var storageComp))
        {
            if (_entityStorage.Insert(spawnerEnt.Value, locker, storageComp))
            {
                comp.Placed = true;
                return;
            }

            // Locker full (e.g. player already inserted) — fall back to locker coordinates.
            _transform.SetMapCoordinates(spawnerEnt.Value, _transform.GetMapCoordinates(locker));
            comp.Placed = true;
            return;
        }

        if (comp.FallbackCoords is { } coords)
        {
            _transform.SetMapCoordinates(spawnerEnt.Value, _transform.ToMapCoordinates(coords));
            comp.Placed = true;
        }
    }

    private void EnsureLocation(AntagLockerSpawnComponent comp)
    {
        if (comp.ChosenLocker is { } existingLocker && Exists(existingLocker))
            return;

        if (comp.FallbackCoords != null)
            return;

        if (!TryGetRandomStation(out var station))
        {
            if (_betterSpawn.TryFindSafeRandomLocation(out var earlyFallback))
                comp.FallbackCoords = earlyFallback;
            return;
        }

        // Mini: only pick the locker here, the target may not exist yet (token auto-join).
        if (TryFindLocker(station.Value, comp.MaintenanceOnly, out var locker, out _))
        {
            comp.ChosenLocker = locker;
            return;
        }

        // No valid locker found; fall back to TryFindSafeRandomLocation.
        if (_betterSpawn.TryFindSafeRandomLocation(out var safeCoords))
            comp.FallbackCoords = safeCoords;
    }

    private bool TryFindLocker(
        EntityUid station,
        bool maintenanceOnly,
        out EntityUid locker,
        out EntityStorageComponent storageComp)
    {
        locker = default;
        storageComp = default!;

        var validLockers = new List<(EntityUid, EntityStorageComponent)>();
        var query = EntityQueryEnumerator<EntityStorageComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out var storage, out var xform))
        {
            if (_stationSystem.GetOwningStation(ent, xform) != station
                || storage.Open
                || storage.Contents.ContainedEntities.Count >= storage.Capacity)
                continue;

            if (maintenanceOnly && !_tag.HasTag(ent, MaintenanceClosetTag))
                continue;

            validLockers.Add((ent, storage));
        }

        if (validLockers.Count == 0)
            return false;

        (locker, storageComp) = RobustRandom.Pick(validLockers);
        return true;
    }

    private void PlaceInStation(
        EntityUid station,
        EntityUid target,
        bool maintenanceOnly,
        out EntityUid? locker,
        out EntityCoordinates? fallback)
    {
        locker = null;
        fallback = null;

        if (TryFindLocker(station, maintenanceOnly, out var picked, out var storageComp)
            && _entityStorage.Insert(target, picked, storageComp))
        {
            locker = picked;
            return;
        }

        // No locker: fall back to a safe random location.
        if (_betterSpawn.TryFindSafeRandomLocation(out var safeCoords))
        {
            _transform.SetMapCoordinates(target, _transform.ToMapCoordinates(safeCoords));
            fallback = safeCoords;
        }
    }

    /// <summary>
    /// Relocates the target to a random maintenance locker.
    /// </summary>
    public bool TryRelocateToLocker(EntityUid target, bool maintenanceOnly = true)
    {
        if (!TryGetRandomStation(out var station))
            return false;

        PlaceInStation(station.Value, target, maintenanceOnly, out var locker, out var fallback);
        return locker != null || fallback != null;
    }
}
