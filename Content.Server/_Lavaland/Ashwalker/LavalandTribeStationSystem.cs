// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Server.GameTicking.Events;
using Content.Server._Lavaland.Procedural.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Lavaland.Ashwalker;

/// <summary>
/// Creates the ash walker tribe station on the Lavaland planet at round start, so the
/// AshWalker job has native job slots, a crew manifest entry and spawn points at the den.
/// The station lives for one round: stations are deleted at round end and the planet map is
/// flushed with the rest of the world, so the next round starts from scratch.
/// </summary>
public sealed class LavalandTribeStationSystem : EntitySystem
{
    private static readonly EntProtoId TribeStation = "LavalandTribeStation";

    [Dependency] private readonly LavalandSystem _lavaland = default!;
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Late enough that the planet exists (staged lobby preload), early enough that
        // SpawnPlayers sees the station and its job slots.
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
    }

    private void OnRoundStarting(RoundStartingEvent args)
    {
        EnsureTribeStation();
    }

    /// <summary>
    /// Ensures the Lavaland planet grid belongs to the tribe station.
    /// Returns false when there is no Lavaland planet in this round.
    /// </summary>
    public bool EnsureTribeStation()
    {
        var lavalands = _lavaland.GetLavalands();
        if (lavalands.Count == 0)
            return false;

        var planet = lavalands[0];

        // Already set up: either this system ran twice or the grid is reused.
        if (TryComp<StationMemberComponent>(planet.Owner, out var member)
            && !TerminatingOrDeleted(member.Station)
            && HasComp<StationDataComponent>(member.Station))
            return true;

        var station = Spawn(TribeStation, MapCoordinates.Nullspace);
        _station.AddGridToStation(station, planet.Owner);

        Log.Info($"Created ash walker tribe station {ToPrettyString(station)} on Lavaland grid {ToPrettyString(planet.Owner)}.");
        return true;
    }
}
