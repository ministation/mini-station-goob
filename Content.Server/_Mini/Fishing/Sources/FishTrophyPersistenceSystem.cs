// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Text.Json;
using Content.Shared._Mini.Fishing.Components;
using Robust.Shared.ContentPack;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Mini.Fishing.Sources;

/// <summary>
/// Mini Station addition: tg has no trophy persistence, so mounted fish are written to a JSON
/// file in user data and restored on freshly spawned mounts after a server restart.
/// </summary>
public sealed partial class FishTrophyPersistenceSystem : EntitySystem
{
    private const int MaxRecords = 100;

    // Not every empty mount gets a trophy back; most stay empty.
    private const float RestoreChance = 0.35f;

    private static readonly ResPath StoragePath = new("/fish_trophies.json");

    [Dependency] private IResourceManager _res = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    private readonly List<TrophyRecord> _records = new();
    private readonly HashSet<EntityUid> _restoring = new();

    public override void Initialize()
    {
        base.Initialize();

        Load();

        SubscribeLocalEvent<FishMountComponent, MapInitEvent>(OnMountMapInit);
        SubscribeLocalEvent<FishMountComponent, EntInsertedIntoContainerMessage>(OnMountInserted);
    }

    private void OnMountInserted(Entity<FishMountComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.Container || _restoring.Contains(ent.Owner))
            return;

        if (!TryComp<FishComponent>(args.Entity, out var fish))
            return;

        var meta = MetaData(args.Entity);
        if (meta.EntityPrototype is null)
            return;

        _records.Add(new TrophyRecord
        {
            Proto = meta.EntityPrototype.ID,
            Size = fish.Size,
            Weight = fish.Weight,
            Catcher = fish.Catcher,
            CatchMinutes = fish.CatchDate is { } date ? (int) date.TotalMinutes : -1,
            SavedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        });

        while (_records.Count > MaxRecords)
        {
            _records.RemoveAt(0);
        }

        Save();
    }

    private void OnMountMapInit(Entity<FishMountComponent> ent, ref MapInitEvent args)
    {
        if (_records.Count == 0 || _random.NextFloat() > RestoreChance)
            return;

        if (!_container.TryGetContainer(ent.Owner, ent.Comp.Container, out var container) ||
            container.ContainedEntities.Count > 0)
            return;

        var record = _records[_random.Next(_records.Count)];
        if (!_proto.HasIndex<EntityPrototype>(record.Proto))
            return;

        var fish = Spawn(new EntProtoId(record.Proto), Transform(ent.Owner).Coordinates);

        if (TryComp<FishComponent>(fish, out var comp))
        {
            comp.Size = record.Size;
            comp.Weight = record.Weight;
            comp.Catcher = record.Catcher;
            comp.CatchDate = record.CatchMinutes >= 0 ? TimeSpan.FromMinutes(record.CatchMinutes) : null;
            comp.Alive = false;
            comp.Initialized = true;
            Dirty(fish, comp);
        }

        _restoring.Add(ent.Owner);
        _container.Insert(fish, container);
        _restoring.Remove(ent.Owner);
    }

    private void Load()
    {
        try
        {
            if (!_res.UserData.TryReadAllText(StoragePath, out var text))
                return;

            var loaded = JsonSerializer.Deserialize<List<TrophyRecord>>(text);
            if (loaded != null)
                _records.AddRange(loaded);
        }
        catch (Exception e)
        {
            Log.Warning($"Could not read fish trophy storage: {e.Message}");
        }
    }

    private void Save()
    {
        try
        {
            using var writer = _res.UserData.OpenWriteText(StoragePath);
            writer.Write(JsonSerializer.Serialize(_records));
        }
        catch (Exception e)
        {
            Log.Warning($"Could not save fish trophy storage: {e.Message}");
        }
    }

    public sealed class TrophyRecord
    {
        public string Proto { get; set; } = string.Empty;
        public float Size { get; set; }
        public float Weight { get; set; }
        public string? Catcher { get; set; }
        public int CatchMinutes { get; set; } = -1;
        public long SavedUnix { get; set; }
    }
}