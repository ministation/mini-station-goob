// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Linq;
using Content.Goobstation.Shared.Fishing.Components;
using Content.Goobstation.Shared.Fishing.Events;
using Content.Goobstation.Shared.Fishing.Prototypes;
using Content.Shared.EntityTable;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Goobstation.Shared.Fishing.Systems;

/// <summary>
/// Resolves which fish source a cast lands on, calculates the difficulty of a catch
/// and rolls the actual loot.
/// </summary>
public sealed partial class FishingSourceSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private EntityTableSystem _tables = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private ITileDefinitionManager _tileDefs = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<string, ProtoId<FishingSourcePrototype>> _tileSources = new();

    public override void Initialize()
    {
        base.Initialize();
        _proto.PrototypesReloaded += OnPrototypesReloaded;
        RebuildTileCache();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _proto.PrototypesReloaded -= OnPrototypesReloaded;
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        RebuildTileCache();
    }

    private void RebuildTileCache()
    {
        _tileSources.Clear();

        foreach (var source in _proto.EnumeratePrototypes<FishingSourcePrototype>())
        {
            foreach (var tile in source.Tiles)
            {
                _tileSources[tile.Id] = source.ID;
            }
        }
    }

    /// <summary>
    /// Finds the fish source at the given coordinates. A directly clicked entity takes priority.
    /// </summary>
    public bool TryGetSource(EntityCoordinates coords, EntityUid? target, out ProtoId<FishingSourcePrototype> source)
    {
        source = default;

        if (target is { } targetUid && TryComp<FishingSourceComponent>(targetUid, out var targetSource))
        {
            source = targetSource.Source;
            return true;
        }

        foreach (var ent in _lookup.GetEntitiesIntersecting(coords, LookupFlags.StaticSundries))
        {
            if (!TryComp<FishingSourceComponent>(ent, out var spot))
                continue;

            source = spot.Source;
            return true;
        }

        if (_turf.GetTileRef(coords) is not { } tileRef)
            return false;

        var tile = _tileDefs[tileRef.Tile.TypeId];
        return _tileSources.TryGetValue(tile.ID, out source);
    }

    public bool TryGetSource(EntityCoordinates coords, out ProtoId<FishingSourcePrototype> source)
    {
        return TryGetSource(coords, null, out source);
    }

    public float GetWaitTime(ProtoId<FishingSourcePrototype> sourceId)
    {
        if (!_proto.TryIndex(sourceId, out FishingSourcePrototype? source))
            return 10f;

        return _random.NextFloat(source.WaitTimeMin, source.WaitTimeMax);
    }

    /// <summary>
    /// Base difficulty of a minigame for this source and fish, before rod and equipment
    /// modifiers are applied by other systems through <see cref="FishFightModifiersEvent"/>.
    /// </summary>
    public float CalculateDifficulty(ProtoId<FishingSourcePrototype> sourceId, ProtoId<FishPrototype>? fishId, EntityUid rod, EntityUid user)
    {
        if (!_proto.TryIndex(sourceId, out FishingSourcePrototype? source))
            return 15f;

        var difficulty = source.Difficulty;

        if (fishId is { } id && _proto.TryIndex(id, out FishPrototype? fish))
        {
            difficulty += fish.DifficultyModifier;

            foreach (var traitId in fish.Traits)
            {
                if (_proto.TryIndex(traitId, out FishTraitPrototype? trait))
                    difficulty += trait.AddedDifficulty;
            }
        }

        var ev = new FishFightModifiersEvent(difficulty);
        RaiseLocalEvent(rod, ref ev);
        return ev.Difficulty;
    }

    /// <summary>
    /// Rolls the loot table of the source without spawning anything. Used at the biting phase
    /// so the minigame already knows what it fights for.
    /// </summary>
    public EntProtoId? RollReward(ProtoId<FishingSourcePrototype> sourceId, EntityUid user, EntityUid rod)
    {
        if (!_net.IsServer)
            return null;

        if (!_proto.TryIndex(sourceId, out FishingSourcePrototype? source))
            return null;

        var lootEv = new FishLootModifiersEvent(user, rod, sourceId);
        RaiseLocalEvent(rod, ref lootEv);

        var spawns = _tables.GetSpawns(source.Table, _random.GetRandom()).ToList();

        for (var i = 0; i < lootEv.DudRerolls && spawns.Count == 0; i++)
        {
            spawns = _tables.GetSpawns(source.Table, _random.GetRandom()).ToList();
        }

        return spawns.Count == 0 ? (EntProtoId?) null : spawns[_random.Next(spawns.Count)];
    }

    /// <summary>
    /// Spawns the given catch and hands it to the user.
    /// </summary>
    public EntityUid? CatchFish(EntityUid user, EntityUid rod, EntProtoId reward, EntityCoordinates coords)
    {
        if (!_net.IsServer)
            return null;

        var caught = Spawn(reward, coords);

        if (TryComp<FishComponent>(caught, out var fish))
        {
            fish.Catcher = Name(user);
            fish.CatchDate = _timing.CurTime;
            Dirty(caught, fish);

            var caughtEv = new FishCaughtEvent(caught);
            RaiseLocalEvent(user, ref caughtEv);

            _popup.PopupEntity(Loc.GetString("fishing-caught-fish", ("fish", Name(caught))), user, user);
        }

        var userPos = _xform.GetWorldPosition(user);
        var catchPos = _xform.GetWorldPosition(caught);
        var direction = userPos - catchPos;
        if (direction.LengthSquared() > 0.01f)
            _throwing.TryThrow(caught, direction, 4f, user);

        return caught;
    }
}