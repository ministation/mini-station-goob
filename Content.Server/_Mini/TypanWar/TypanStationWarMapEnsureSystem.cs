// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Server._TT.AdditionalMap;
using Content.Server._TT.StationHandleJob;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Maps;
using Content.Shared.Maps;
using Content.Shared.Station.Components;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Mini.TypanWar;

/// <summary>
/// Ensures Typan Station War always has both an NT and a Typan station, even when the selected
/// main map has no <see cref="AdditionalMapPrototype"/> entry (e.g. Dev).
/// </summary>
public sealed class TypanStationWarMapEnsureSystem : EntitySystem
{
    private const string WarPresetId = "TypanStationWar";
    private static readonly ProtoId<GameMapPrototype> TypanMapId = "Typan";
    private static readonly ProtoId<GameMapPrototype> AspidMapId = "Aspid";
    private static readonly ProtoId<GameMapPrototype> NtFallbackMapId = "Empty";

    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IGameMapManager _gameMapManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<LoadingMapsEvent>(OnLoadingMaps, after: [typeof(AdditionalMapLoaderSystem)]);
        // Maps may already be preloaded (LoadMaps early-return) before supplemental stations were added.
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
    }

    private void OnLoadingMaps(LoadingMapsEvent ev)
    {
        if (ev.Maps.Count == 0)
            return;

        TryEnsureSupplementalMaps(ev.Maps[0]);
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        TryEnsureSupplementalMaps();
    }

    /// <summary>
    /// Loads missing NT/Typan supplemental maps when the war preset is active.
    /// Safe to call multiple times — skips maps whose stations already exist.
    /// </summary>
    public void TryEnsureSupplementalMaps(GameMapPrototype? mainMap = null)
    {
        if (!IsWarPresetActive())
            return;

        mainMap ??= _gameMapManager.GetSelectedMap();
        if (mainMap == null)
            return;

        // typanpool.yml handles supplemental Typan for maps that declare additionalMap.
        if (_prototypes.HasIndex<AdditionalMapPrototype>(mainMap.ID))
            return;

        var isTypanMain = mainMap.ID == TypanMapId || mainMap.ID == AspidMapId;
        if (!isTypanMain && !HasTypanStation())
            LoadSupplemental(SelectTypanMapId());
        else if (isTypanMain && !HasNtStation())
            LoadSupplemental(NtFallbackMapId);
    }

    private ProtoId<GameMapPrototype> SelectTypanMapId()
    {
        return _random.Prob(0.5f) ? AspidMapId : TypanMapId;
    }

    private bool IsWarPresetActive()
    {
        var presetId = _ticker.Preset?.ID ?? _ticker.CurrentPreset?.ID;
        return presetId == WarPresetId;
    }

    private bool HasTypanStation()
    {
        var query = EntityQueryEnumerator<TTStationHandleJobComponent>();
        return query.MoveNext(out _, out _);
    }

    private bool HasNtStation()
    {
        var query = EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!HasComp<TTStationHandleJobComponent>(uid))
                return true;
        }

        return false;
    }

    private void LoadSupplemental(ProtoId<GameMapPrototype> mapId)
    {
        if (!_prototypes.TryIndex(mapId, out var map))
        {
            Log.Error($"Typan Station War: failed to load supplemental map '{mapId}' — prototype missing.");
            return;
        }

        Log.Info($"Typan Station War: loading supplemental map '{mapId}'.");
        _ticker.LoadGameMap(map, out _, options: new DeserializationOptions { InitializeMaps = true });
    }
}
