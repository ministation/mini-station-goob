using Content.Server.Administration.Managers;
using Content.Server.GameTicking;
using Content.Server.Shuttles.Systems;
using Content.Server.Spawners.Components;
using Content.Server.Spawners.EntitySystems;
using Content.Server.Station.Components;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Station.Components;
using Content.Shared.Warps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Linq;

namespace Content.Server._TT.StationHandleJob;

public sealed class TTStationHandleJobSystem : EntitySystem
{
    [Dependency] private readonly IBanManager _banManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;

    private readonly Dictionary<ProtoId<JobPrototype>, EntityUid> _jobStations = new();
    private readonly HashSet<EntityUid> _handledStations = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TTStationHandleJobComponent, ComponentStartup>(OnHandleJobStartup);
        SubscribeLocalEvent<TTStationHandleJobComponent, ComponentShutdown>(OnHandleJobShutdown);
        SubscribeLocalEvent<PlayerSpawningEvent>(OnPlayerSpawning, before: [typeof(ArrivalsSystem), typeof(ContainerSpawnPointSystem), typeof(SpawnPointSystem)]);
    }

    private void OnHandleJobStartup(EntityUid uid, TTStationHandleJobComponent component, ComponentStartup args)
    {
        _handledStations.Add(uid);
        RegisterStationJobs(uid, component);
    }

    private void OnHandleJobShutdown(EntityUid uid, TTStationHandleJobComponent component, ComponentShutdown args)
    {
        _handledStations.Remove(uid);
        UnregisterStationJobs(component);
    }

    private void RegisterStationJobs(EntityUid station, TTStationHandleJobComponent component)
    {
        foreach (var job in component.Jobs)
            _jobStations[job] = station;
    }

    private void UnregisterStationJobs(TTStationHandleJobComponent component)
    {
        foreach (var job in component.Jobs)
            _jobStations.Remove(job);
    }

    /// <summary>
    /// Returns true if the job belongs to a TTStationHandleJob station (e.g. Typan roles).
    /// </summary>
    public bool IsHandledJob(ProtoId<JobPrototype> job) => TryGetHandledStation(job, out _);

    public bool MindHasHandledJob(EntityUid mindId)
    {
        return _jobs.MindTryGetJobId(mindId, out var jobId)
               && jobId != null
               && IsHandledJob(jobId.Value);
    }

    public bool TryGetHandledStation(ProtoId<JobPrototype> job, out EntityUid station)
    {
        EnsureCacheBuilt();
        return _jobStations.TryGetValue(job, out station);
    }

    /// <summary>
    /// Ensures dedicated job stations (e.g. Typan) participate in roundstart job assignment.
    /// </summary>
    public void EnsureHandledStationsIncluded(List<EntityUid> stations)
    {
        EnsureCacheBuilt();

        foreach (var station in _handledStations)
        {
            if (!HasComp<StationJobsComponent>(station) || !HasComp<StationSpawningComponent>(station))
                continue;

            if (!stations.Contains(station))
                stations.Add(station);
        }
    }

    /// <summary>
    /// Assigns Typan (and other handled) jobs that the stock roundstart allocator skips when
    /// multiple stations split the player budget by slot count.
    /// </summary>
    public void AssignRoundstartHandledJobs(
        ref Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs,
        IReadOnlyDictionary<NetUserId, HumanoidCharacterProfile> profiles,
        IEnumerable<NetUserId> readyPlayers)
    {
        EnsureCacheBuilt();

        var usedSlots = new Dictionary<(EntityUid Station, ProtoId<JobPrototype> Job), int>();
        foreach (var (_, (job, station)) in assignedJobs)
        {
            if (job is not { } assignedJob || station == EntityUid.Invalid)
                continue;

            var key = (station, assignedJob);
            usedSlots[key] = usedSlots.GetValueOrDefault(key) + 1;
        }

        foreach (var userId in readyPlayers)
        {
            if (assignedJobs.ContainsKey(userId))
                continue;

            if (!profiles.TryGetValue(userId, out var profile))
                continue;

            if (SelectRoundstartHandledJob(userId, profile, usedSlots) is not { } pick)
                continue;

            assignedJobs[userId] = (pick.Job, pick.Station);
            var key = (pick.Station, pick.Job);
            usedSlots[key] = usedSlots.GetValueOrDefault(key) + 1;
        }
    }

    /// <summary>
    /// Ensures handled jobs are assigned to their dedicated station at roundstart,
    /// and that non-handled jobs are not assigned to handled-only stations.
    /// </summary>
    public void FixJobStationAssignments(ref Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs)
    {
        EnsureCacheBuilt();

        var defaultStation = FindDefaultStation(assignedJobs, _handledStations);

        foreach (var userId in assignedJobs.Keys.ToList())
        {
            var (job, station) = assignedJobs[userId];
            if (job == null)
                continue;

            if (TryGetHandledStation(job.Value, out var requiredStation))
            {
                if (station != requiredStation)
                    assignedJobs[userId] = (job, requiredStation);
                continue;
            }

            if (station == EntityUid.Invalid || !_handledStations.Contains(station))
                continue;

            if (defaultStation is { } fallback)
                assignedJobs[userId] = (job, fallback);
        }
    }

    private void EnsureCacheBuilt()
    {
        if (_jobStations.Count > 0 && _handledStations.Count > 0)
            return;

        _jobStations.Clear();
        _handledStations.Clear();

        var query = EntityQueryEnumerator<TTStationHandleJobComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            _handledStations.Add(uid);
            RegisterStationJobs(uid, component);
        }
    }

    private void OnPlayerSpawning(PlayerSpawningEvent ev)
    {
        EnsureCacheBuilt();

        if (ev.SpawnResult is not null)
        {
            Log.Error("The spawn result has already been received");
            return;
        }

        if (ev.Job is not { } job)
        {
            Log.Debug("The job does not exist");
            return;
        }

        if (!TryGetHandledStation(job, out var handledStation))
        {
            var requestedStation = ev.Station;
            var requestedIsHandledStation = requestedStation is { } requestedStationUid &&
                _handledStations.Contains(requestedStationUid);

            if (requestedIsHandledStation)
            {
                AbortSpawn(ev,
                    $"Blocked spawn for job {job} on station {GetStationName(requestedStation)}: this station only accepts TTStationHandleJob roles.",
                    Loc.GetString("game-ticker-player-job-spawn-invalid-station"));
            }

            return;
        }

        if (ev.Station is { } req && req != handledStation)
        {
            Log.Warning(
                $"Redirecting spawn for job {job} from {GetStationName(ev.Station)} to {GetStationName(handledStation)}.");
        }

        var possiblePositions = CollectSpawnLocations(handledStation, job);

        if (possiblePositions.Count == 0)
        {
            AbortSpawn(ev,
                $"No spawn points found for role {job} on station {GetStationName(handledStation)}.",
                Loc.GetString("game-ticker-player-job-spawn-no-spawn-point"));
            return;
        }

        var spawnLoc = _random.Pick(possiblePositions);
        ev.SpawnResult = _stationSpawning.SpawnPlayerMob(
            spawnLoc,
            job,
            ev.HumanoidCharacterProfile,
            handledStation);
    }

    private List<EntityCoordinates> CollectSpawnLocations(EntityUid handledStation, ProtoId<JobPrototype> job)
    {
        // Prefer job-specific Job spawners, then LateJoin (null job_id = any job, like stock SpawnPointSystem).
        var possiblePositions = CollectSpawnLocations(handledStation, job, SpawnPointType.Job);

        if (possiblePositions.Count == 0)
            possiblePositions = CollectSpawnLocations(handledStation, job, SpawnPointType.LateJoin);

        // Any compatible spawner on the station (covers Unset / mixed map data).
        if (possiblePositions.Count == 0)
            possiblePositions = CollectCompatibleSpawnLocations(handledStation, job);

        // Last resort: stand on a station grid so AbortSpawn does not wipe the round.
        if (possiblePositions.Count == 0)
            possiblePositions = CollectGridFallbackLocations(handledStation);

        return possiblePositions;
    }

    private List<EntityCoordinates> CollectSpawnLocations(EntityUid handledStation, ProtoId<JobPrototype> job, SpawnPointType spawnType)
    {
        var possiblePositions = new List<EntityCoordinates>();

        var query = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var spawnPoint, out var xform))
        {
            if (!IsSpawnOnStation(uid, xform, handledStation))
                continue;

            // Null Job means any role may use this point (LateJoin CentComm, etc.).
            if (spawnPoint.Job != null && spawnPoint.Job != job)
                continue;

            if (spawnPoint.SpawnType != spawnType)
                continue;

            possiblePositions.Add(xform.Coordinates);
        }

        return possiblePositions;
    }

    private List<EntityCoordinates> CollectCompatibleSpawnLocations(EntityUid handledStation, ProtoId<JobPrototype> job)
    {
        var possiblePositions = new List<EntityCoordinates>();

        var query = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var spawnPoint, out var xform))
        {
            if (spawnPoint.SpawnType is SpawnPointType.Observer)
                continue;

            if (!IsSpawnOnStation(uid, xform, handledStation))
                continue;

            if (spawnPoint.Job != null && spawnPoint.Job != job)
                continue;

            possiblePositions.Add(xform.Coordinates);
        }

        return possiblePositions;
    }

    private List<EntityCoordinates> CollectGridFallbackLocations(EntityUid handledStation)
    {
        var possiblePositions = new List<EntityCoordinates>();

        if (!TryComp<StationDataComponent>(handledStation, out var data) || data.Grids.Count == 0)
            return possiblePositions;

        // Prefer named warp points on the station (CentComm map has "CentCom") — never use grid (0,0), that is space.
        var warps = EntityQueryEnumerator<WarpPointComponent, TransformComponent>();
        while (warps.MoveNext(out _, out var warp, out var xform))
        {
            if (!IsTransformOnStationGrids(xform, data))
                continue;

            if (warp.Location != null &&
                warp.Location.Contains("CentCom", StringComparison.OrdinalIgnoreCase))
                possiblePositions.Add(xform.Coordinates);
        }

        if (possiblePositions.Count == 0)
        {
            warps = EntityQueryEnumerator<WarpPointComponent, TransformComponent>();
            while (warps.MoveNext(out _, out _, out var xform))
            {
                if (!IsTransformOnStationGrids(xform, data))
                    continue;

                possiblePositions.Add(xform.Coordinates);
            }
        }

        if (possiblePositions.Count > 0)
        {
            Log.Warning(
                $"No job spawners on {GetStationName(handledStation)}; falling back to warp point(s).");
            return possiblePositions;
        }

        // Last resort: local offset into the grid AABB (origin is often empty space around CentComm).
        foreach (var grid in data.Grids)
        {
            if (!Exists(grid) || !TryComp<MapGridComponent>(grid, out var gridComp))
                continue;

            var local = gridComp.LocalAABB.Center;
            possiblePositions.Add(new EntityCoordinates(grid, local));
            Log.Warning(
                $"No warps on {GetStationName(handledStation)}; falling back to grid AABB center {local} on {ToPrettyString(grid)}.");
            break;
        }

        return possiblePositions;
    }

    private bool IsSpawnOnStation(EntityUid spawnEnt, TransformComponent xform, EntityUid station)
    {
        if (_station.GetOwningStation(spawnEnt, xform) == station)
            return true;

        if (!TryComp<StationDataComponent>(station, out var data))
            return false;

        return IsTransformOnStationGrids(xform, data);
    }

    private bool IsTransformOnStationGrids(TransformComponent xform, StationDataComponent data)
    {
        if (xform.GridUid is { } gridUid)
        {
            foreach (var grid in data.Grids)
            {
                if (grid == gridUid)
                    return true;
            }
        }

        // Entity still parented to the grid but GridUid not updated yet.
        foreach (var grid in data.Grids)
        {
            if (grid == xform.ParentUid)
                return true;
        }

        // CentComm is a dedicated map: accept anything on the same map as a station grid.
        if (xform.MapUid is not { } mapUid)
            return false;

        foreach (var grid in data.Grids)
        {
            if (!TryComp(grid, out TransformComponent? gridXform))
                continue;

            if (gridXform.MapUid == mapUid)
                return true;
        }

        return false;
    }

    private readonly record struct HandledJobPick(ProtoId<JobPrototype> Job, EntityUid Station);

    private HandledJobPick? SelectRoundstartHandledJob(
        NetUserId userId,
        HumanoidCharacterProfile profile,
        Dictionary<(EntityUid Station, ProtoId<JobPrototype> Job), int> usedSlots)
    {
        var roleBans = _banManager.GetJobBans(userId);
        var candidates = new List<ProtoId<JobPrototype>>();

        foreach (var (jobIdStr, priority) in profile.JobPriorities)
        {
            if (priority == JobPriority.Never)
                continue;

            var jobId = new ProtoId<JobPrototype>(jobIdStr);
            if (!IsHandledJob(jobId))
                continue;

            candidates.Add(jobId);
        }

        if (candidates.Count == 0)
            return null;

        var ev = new StationJobsGetCandidatesEvent(userId, candidates);
        RaiseLocalEvent(ref ev);

        HandledJobPick? best = null;
        var bestPriority = JobPriority.Never;

        foreach (var jobId in ev.Jobs)
        {
            if (roleBans != null && roleBans.Contains(jobId))
                continue;

            if (!profile.JobPriorities.TryGetValue(jobId, out var priority) || priority == JobPriority.Never)
                continue;

            if (priority <= bestPriority)
                continue;

            if (!TryGetHandledStation(jobId, out var station))
                continue;

            if (!HasOpenRoundstartSlot(station, jobId, usedSlots))
                continue;

            best = new HandledJobPick(jobId, station);
            bestPriority = priority;
        }

        return best;
    }

    private bool HasOpenRoundstartSlot(
        EntityUid station,
        ProtoId<JobPrototype> jobId,
        Dictionary<(EntityUid Station, ProtoId<JobPrototype> Job), int> usedSlots)
    {
        if (!TryComp<StationJobsComponent>(station, out var stationJobs))
            return false;

        var slots = _stationJobs.GetRoundStartJobs(station, stationJobs);
        if (!slots.TryGetValue(jobId, out var count))
            return false;

        if (count == null)
            return true;

        var used = usedSlots.GetValueOrDefault((station, jobId));
        return used < count;
    }

    private EntityUid? FindDefaultStation(
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs,
        HashSet<EntityUid> handledStations)
    {
        foreach (var (_, (_, station)) in assignedJobs)
        {
            if (station == EntityUid.Invalid || handledStations.Contains(station))
                continue;

            return station;
        }

        var query = EntityQueryEnumerator<StationJobsComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (handledStations.Contains(uid))
                continue;

            return uid;
        }

        return null;
    }

    private void AbortSpawn(PlayerSpawningEvent ev, string reason, string failureMessage)
    {
        ev.PreventFallback = true;
        ev.FailureMessage = failureMessage;
        Log.Warning(reason);
    }

    private string GetStationName(EntityUid? station)
    {
        return station is { } stationUid
            ? Name(stationUid)
            : "<null>";
    }
}
