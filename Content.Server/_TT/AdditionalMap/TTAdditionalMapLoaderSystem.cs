using Content.Server.GameTicking;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

// Author: by TornadoTech
namespace Content.Server._TT.AdditionalMap;

public sealed class TTAdditionalMapLoaderSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LoadingMapsEvent>(OnGetMaps);
    }

    private void OnGetMaps(LoadingMapsEvent args)
    {
        var firstMap = args.Maps[0];
        if (!_prototype.TryIndex<TTAdditionalMapPrototype>(firstMap.ID, out var proto))
            return;

        foreach (var mapProtoId in proto.MapProtoIds)
        {
            if (!_prototype.TryIndex(mapProtoId, out var mapProto))
                continue;

            _gameTicker.LoadGameMap(mapProto,
                out _,
                options: new DeserializationOptions
                {
                    InitializeMaps = true,
                });
        }
    }
}
