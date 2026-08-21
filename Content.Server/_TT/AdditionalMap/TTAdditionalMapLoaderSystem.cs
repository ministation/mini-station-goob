using Content.Server.GameTicking;
using Content.Shared.Maps;
using Robust.Server.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

// Author: by TornadoTech
namespace Content.Server._TT.AdditionalMap;

public sealed class AdditionalMapLoaderSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LoadingMapsEvent>(OnGetMaps);
    }

    private void OnGetMaps(LoadingMapsEvent args)
    {
        if (args.Maps.Count == 0)
            return;

        var firstMap = args.Maps[0];
        if (!_prototype.TryIndex<AdditionalMapPrototype>(firstMap.ID, out var proto))
            return;

        var playerCount = _playerManager.PlayerCount;
        var eligible = new List<GameMapPrototype>();

        foreach (var mapProtoId in proto.MapProtoIds)
        {
            if (!_prototype.TryIndex(mapProtoId, out var mapProto))
                continue;

            // Skip supplemental maps outside their min/maxPlayers range.
            if (mapProto.MinPlayers > playerCount || mapProto.MaxPlayers < playerCount)
                continue;

            // Already queued (e.g. duplicate pool entry).
            if (args.Maps.Contains(mapProto))
                continue;

            eligible.Add(mapProto);
        }

        if (eligible.Count == 0)
            return;

        // Pick one at random (e.g. Typan vs Aspid in typanpool).
        // Append for staged preload — do not LoadGameMap here (avoids multi-second lobby freeze).
        var chosen = _random.Pick(eligible);
        args.Maps.Add(chosen);
    }
}
