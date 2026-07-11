// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Server.Storage.EntitySystems;
using Content.Shared._Mini.TypanWar;
using Content.Shared.Storage.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Mini.TypanWar;

/// <summary>
/// Rolls each loot-table entry independently and inserts successful spawns into the crate.
/// </summary>
public sealed class TypanWarSurplusBundleSystem : EntitySystem
{
    [Dependency] private readonly EntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TypanWarSurplusBundleComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, TypanWarSurplusBundleComponent component, MapInitEvent args)
    {
        if (!_proto.TryIndex(component.LootTable, out var table))
        {
            Log.Error($"Typan war surplus crate {uid} references unknown loot table {component.LootTable}.");
            return;
        }

        if (!TryComp<EntityStorageComponent>(uid, out _))
        {
            Log.Warning($"Typan war surplus crate {uid} has no entity storage.");
            return;
        }

        var coords = Transform(uid).Coordinates;
        var spawned = 0;

        foreach (var entry in table.Entries)
        {
            if (!_random.Prob(entry.Probability))
                continue;

            var item = Spawn(entry.Item, coords);
            if (_entityStorage.Insert(item, uid))
                spawned++;
            else
                Del(item);
        }

        if (spawned != 0 || string.IsNullOrEmpty(component.FallbackItem.Id))
            return;

        var fallback = Spawn(component.FallbackItem, coords);
        if (!_entityStorage.Insert(fallback, uid))
            Del(fallback);
    }
}
