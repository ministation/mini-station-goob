// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Server.GameTicking;
using Content.Server.Storage.EntitySystems;
using Content.Shared._Mini.TypanWar;
using Content.Shared.GameTicking.Components;
using Content.Shared.Storage.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Mini.TypanWar;

/// <summary>
/// Rolls each loot-table entry independently and inserts successful spawns into the crate.
/// Probabilities scale up as the war round progresses.
/// </summary>
public sealed class TypanWarSurplusBundleSystem : EntitySystem
{
    private const float RoundLootQualityBonus = 0.75f;

    [Dependency] private readonly EntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
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

        var roundProgress = TryGetWarRoundProgress(out _);
        var coords = Transform(uid).Coordinates;
        var spawned = 0;

        foreach (var entry in table.Entries)
        {
            if (!_random.Prob(GetEffectiveProbability(entry, roundProgress)))
                continue;

            var countMin = Math.Max(1, entry.CountMin);
            var countMax = Math.Max(countMin, entry.CountMax);
            var count = _random.Next(countMin, countMax + 1);

            for (var i = 0; i < count; i++)
            {
                var item = Spawn(entry.Item, coords);
                if (_entityStorage.Insert(item, uid))
                    spawned++;
                else
                    Del(item);
            }
        }

        if (spawned != 0 || string.IsNullOrEmpty(component.FallbackItem.Id))
            return;

        var fallback = Spawn(component.FallbackItem, coords);
        if (!_entityStorage.Insert(fallback, uid))
            Del(fallback);
    }

    private static float GetEffectiveProbability(TypanWarSurplusLootEntry entry, float roundProgress)
    {
        if (entry.LateRoundProbability is { } late)
            return MathHelper.Lerp(entry.Probability, late, roundProgress);

        return Math.Min(1f, entry.Probability * (1f + roundProgress * RoundLootQualityBonus));
    }

    private float TryGetWarRoundProgress(out TypanStationWarRuleComponent? rule)
    {
        rule = null;
        var query = EntityQueryEnumerator<TypanStationWarRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var comp, out var gameRule))
        {
            if (!_ticker.IsGameRuleActive(uid, gameRule) || comp.Phase != TypanWarPhase.Active)
                continue;

            rule = comp;
            if (comp.WarStartTime == null || comp.WarDurationSeconds <= 0f)
                return 0f;

            var elapsed = (_timing.CurTime - comp.WarStartTime.Value).TotalSeconds;
            return (float) Math.Clamp(elapsed / comp.WarDurationSeconds, 0, 1);
        }

        return 0f;
    }
}
