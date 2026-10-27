// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Numerics;
using Content.Server.KillTracking;
using Content.Server.Parallax;
using Content.Server._Mini.Katorga.Components;
using Content.Shared._Mini.Katorga;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Mobs.Components;
using Content.Shared.Parallax.Biomes;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Mini.Katorga;

/// <summary>
///     Популяция фауны на каторге: засевает и поддерживает мобов вокруг лагерей.
///     Спавн только на уже сгенерированных тайлах биома (не в космосе, не в скале,
///     не рядом с игроками), так что по мере исследования астероида фауна расползается.
///     Награда за убийство начисляется в KatorgaSystem.OnKillReported.
/// </summary>
public sealed class KatorgaFaunaSystem : EntitySystem
{
    private static readonly CollisionGroup SpawnBlockerMask =
        CollisionGroup.Impassable |
        CollisionGroup.HighImpassable |
        CollisionGroup.MidImpassable |
        CollisionGroup.LowImpassable;

    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private readonly Dictionary<EntProtoId, int> _counts = new();
    private readonly HashSet<EntityUid> _tileEntities = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<KatorgaFaunaPopulationComponent>();
        while (query.MoveNext(out var mapUid, out var population))
        {
            if (now < population.NextSpawnTime ||
                !_prototype.TryIndex(population.Spawner, out var spawner) ||
                population.Bases.Count == 0)
            {
                continue;
            }

            var total = CountLiveFauna();
            if (total >= spawner.HardCap)
            {
                population.NextSpawnTime = now + spawner.SpawnInterval;
                continue;
            }

            var isInitial = population.InitialRemaining > 0;
            var cap = isInitial ? spawner.HardCap : spawner.SoftCap;
            var batchLimit = isInitial
                ? Math.Min(spawner.InitialBatchSize, population.InitialRemaining)
                : _random.Next(spawner.SpawnBatchMin, spawner.SpawnBatchMax + 1);
            var batch = Math.Min(batchLimit, Math.Max(0, cap - total));

            var spawned = SpawnBatch(mapUid, population, spawner, batch);
            if (isInitial)
                population.InitialRemaining -= spawned;

            population.NextSpawnTime = now + (isInitial && spawned > 0
                ? TimeSpan.FromSeconds(10)
                : spawner.SpawnInterval);
        }
    }

    private int CountLiveFauna()
    {
        var total = 0;
        _counts.Clear();
        var query = EntityQueryEnumerator<KatorgaFaunaComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var fauna, out var mobState))
        {
            if (TerminatingOrDeleted(uid))
                continue;

            total++;
            _counts[fauna.Prototype] = _counts.GetValueOrDefault(fauna.Prototype) + 1;
        }

        return total;
    }

    private int SpawnBatch(
        EntityUid mapUid,
        KatorgaFaunaPopulationComponent population,
        KatorgaFaunaSpawnerPrototype spawner,
        int requested)
    {
        var spawned = 0;
        for (var i = 0; i < requested; i++)
        {
            if (!TryPickEntry(spawner, out var entry) ||
                !TryFindSpawnPosition(mapUid, population, spawner, out var coordinates))
            {
                continue;
            }

            var uid = Spawn(entry.Prototype, coordinates);
            var fauna = EnsureComp<KatorgaFaunaComponent>(uid);
            fauna.Prototype = entry.Prototype;
            fauna.RewardMinutes = entry.RewardMinutes;
            EnsureComp<KillTrackerComponent>(uid);
            spawned++;
            _counts[entry.Prototype] = _counts.GetValueOrDefault(entry.Prototype) + 1;
        }

        return spawned;
    }

    private bool TryPickEntry(KatorgaFaunaSpawnerPrototype spawner, out KatorgaFaunaSpawnEntry entry)
    {
        entry = default!;
        var totalWeight = 0;
        foreach (var candidate in spawner.Spawns)
        {
            if (candidate.Weight > 0 && _counts.GetValueOrDefault(candidate.Prototype) < candidate.MaxCount)
                totalWeight += candidate.Weight;
        }

        if (totalWeight <= 0)
            return false;

        var roll = _random.Next(totalWeight);
        foreach (var candidate in spawner.Spawns)
        {
            if (candidate.Weight <= 0 || _counts.GetValueOrDefault(candidate.Prototype) >= candidate.MaxCount)
                continue;

            roll -= candidate.Weight;
            if (roll < 0)
            {
                entry = candidate;
                return true;
            }
        }

        return false;
    }

    private bool TryFindSpawnPosition(
        EntityUid mapUid,
        KatorgaFaunaPopulationComponent population,
        KatorgaFaunaSpawnerPrototype spawner,
        out EntityCoordinates coordinates)
    {
        coordinates = EntityCoordinates.Invalid;

        if (!TryComp<MapGridComponent>(mapUid, out var grid) ||
            !TryComp<BiomeComponent>(mapUid, out var biome))
        {
            return false;
        }

        var baseEntity = _random.Pick(population.Bases);
        if (!Exists(baseEntity))
            return false;

        var center = Transform(baseEntity).WorldPosition;
        var mapId = Transform(mapUid).MapID;

        for (var attempt = 0; attempt < 25; attempt++)
        {
            var direction = _random.NextAngle().ToVec();
            var distance = _random.NextFloat(spawner.MinBaseDistance, spawner.MaxBaseDistance);
            var point = center + direction * distance;
            var indices = _map.WorldToTile(mapUid, grid, point);

            // Тайл должен быть уже сгенерирован (биом растёт вокруг игроков и баз):
            // несгенерированные позиции пропускаем и пробуем снова в следующий заход.
            if (!_map.TryGetTileRef(mapUid, grid, indices, out var tileRef) ||
                tileRef.Tile.IsEmpty ||
                _turf.IsSpace(tileRef.Tile))
            {
                continue;
            }

            if (_turf.IsTileBlocked(mapUid, indices, SpawnBlockerMask, grid) ||
                _biome.TryGetEntity(indices, biome, grid, out _) ||
                HasMobInTile(tileRef) ||
                HasPlayerNearby(mapId, point, spawner.MinPlayerDistance))
            {
                continue;
            }

            coordinates = _map.GridTileToLocal(mapUid, grid, indices);
            return true;
        }

        return false;
    }

    private bool HasMobInTile(TileRef tileRef)
    {
        _tileEntities.Clear();
        _lookup.GetEntitiesInTile(tileRef, _tileEntities, LookupFlags.Dynamic | LookupFlags.Static);
        foreach (var uid in _tileEntities)
        {
            if (HasComp<MobStateComponent>(uid))
                return true;
        }

        return false;
    }

    private bool HasPlayerNearby(MapId mapId, Vector2 point, float range)
    {
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } entity ||
                !TryComp<TransformComponent>(entity, out var xform) ||
                xform.MapID != mapId)
            {
                continue;
            }

            if (Vector2.DistanceSquared(xform.WorldPosition, point) < range * range)
                return true;
        }

        return false;
    }
}
