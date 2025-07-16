using Content.Server.Fax;
using Content.Server.Station.Systems;
using Content.Shared._CorvaxGoob.CCCVars;
using Content.Shared.Fax.Components;
using Content.Shared.GameTicking;
using Content.Shared.Paper;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CorvaxGoob.StationGoal
{
    /// <summary>
    ///     System to spawn paper with station goal.
    /// </summary>
    public sealed class StationGoalPaperSystem : EntitySystem
    {
        [Dependency] private readonly IPrototypeManager _proto = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly FaxSystem _fax = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly StationSystem _station = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;

        public override void Initialize()
        {
            SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        }

        private void OnRoundStarted(RoundStartedEvent ev)
        {
            if (!_cfg.GetCVar(CCCVars.StationGoal))
                return;

            var playerCount = _playerManager.PlayerCount;

            var query = EntityQueryEnumerator<StationGoalComponent>();
            while (query.MoveNext(out var uid, out var station))
            {
                var tempGoals = new List<ProtoId<StationGoalPrototype>>(station.Goals);
                StationGoalPrototype? selGoal = null;
                while (tempGoals.Count > 0)
                {
                    var goalId = _random.Pick(tempGoals);
                    var goalProto = _proto.Index(goalId);

                    if (playerCount > goalProto.MaxPlayers ||
                        playerCount < goalProto.MinPlayers)
                    {
                        tempGoals.Remove(goalId);
                        continue;
                    }

                    selGoal = goalProto;
                    break;
                }

                if (selGoal is null)
                    return;

                if (SendStationGoal(uid, selGoal))
                {
                    Log.Info($"Goal {selGoal.ID} has been sent to station {MetaData(uid).EntityName}");
                }
            }
        }

        public bool SendStationGoal(EntityUid ent, ProtoId<StationGoalPrototype> goal)
        {
            return SendStationGoal(ent, _proto.Index(goal));
        }

        /// <summary>
        ///     Send a station goal on selected station to all faxes which are authorized to receive it.
        /// </summary>
        /// <returns>True if at least one fax received paper</returns>
        public bool SendStationGoal(EntityUid ent, StationGoalPrototype goal)
        {
			var stampId = string.IsNullOrEmpty(goal.Stamp)
				? "RubberStampCentcom"
				: goal.Stamp;

			if (!_proto.HasIndex<EntityPrototype>(stampId))
			{
				Log.Info($"SendStationGoal: stamp prototype '{stampId}' не найден — fallback to RubberStampCentcom");
				stampId = "RubberStampCentcom";
			}

			var entProto = _proto.Index<EntityPrototype>(stampId);
			
			if (!entProto.Components.TryGetValue("Stamp", out var entry))
			{
				return false;
			}
			
			if (entry.Component is not StampComponent stampProto)
			{
				return false;
			}

			var stampedName = stampProto.StampedName;
			var stampedColor = stampProto.StampedColor;
			var stampState = stampProto.StampState;

			var printout = new FaxPrintout(
				Loc.GetString(goal.Text, ("station", MetaData(ent).EntityName)),
				Loc.GetString("station-goal-fax-paper-name"),
				null,
				null,
				stampState,
                [new() { StampedName = stampedName, StampedColor = stampedColor }]
            );

            var wasSent = false;
            var query = EntityQueryEnumerator<FaxMachineComponent>();
            while (query.MoveNext(out var faxUid, out var fax))
            {
                if (!fax.ReceiveAllStationGoals && !(fax.ReceiveStationGoal && _station.GetOwningStation(faxUid) == ent))
                    continue;

                _fax.Receive(faxUid, printout, null, fax);

                foreach (var spawnEnt in goal.Spawns)
                    SpawnAtPosition(spawnEnt, Transform(faxUid).Coordinates);

                wasSent |= fax.ReceiveStationGoal;
            }

            return wasSent;
        }
    }
}
