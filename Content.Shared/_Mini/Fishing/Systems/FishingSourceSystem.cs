// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Linq;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Events;
using Content.Shared._Mini.Fishing.Prototypes;
using Robust.Shared.Containers;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.EntityTable;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Mini.Fishing.Systems;

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
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    private readonly Dictionary<string, ProtoId<FishingSourcePrototype>> _tileSources = new();

    // Runtime limited fish counts, server only.
    private readonly Dictionary<string, Dictionary<EntProtoId, int>> _remaining = new();
    private readonly Dictionary<string, Dictionary<EntProtoId, float>> _regenTimers = new();
    private float _regenAccumulator;

    public override void Initialize()
    {
        base.Initialize();
        _proto.PrototypesReloaded += OnPrototypesReloaded;
        RebuildTileCache();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
        {
            _regenAccumulator = 0f;
            return;
        }

        _regenAccumulator += frameTime;
        while (_regenAccumulator >= 1f)
        {
            _regenAccumulator -= 1f;
            RegenCounts();
        }
    }

    private void RegenCounts()
    {
        foreach (var source in _proto.EnumeratePrototypes<FishingSourcePrototype>())
        {
            if (!_regenTimers.TryGetValue(source.ID, out var timers))
                continue;

            foreach (var (fish, timeLeft) in timers.ToList())
            {
                var next = timeLeft - 1f;
                if (next > 0f)
                {
                    timers[fish] = next;
                    continue;
                }

                timers.Remove(fish);

                if (!_remaining.TryGetValue(source.ID, out var counts) || !counts.TryGetValue(fish, out var current))
                    continue;

                var max = source.FishCounts.TryGetValue(fish, out var initial) ? initial : current + 1;
                counts[fish] = Math.Min(current + 1, max);

                if (counts[fish] < max && source.FishCountRegen.TryGetValue(fish, out var regen))
                    timers[fish] = regen;
            }
        }
    }

    private Dictionary<EntProtoId, int> GetRemaining(FishingSourcePrototype source)
    {
        if (_remaining.TryGetValue(source.ID, out var counts))
            return counts;

        counts = new Dictionary<EntProtoId, int>(source.FishCounts);
        _remaining[source.ID] = counts;
        return counts;
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

            // tg: favorite bait makes the fight easier, disliked bait makes it harder.
            if (GetBaitItem(rod) is { } bait)
            {
                if (IsFavoriteBait(fish, bait))
                    difficulty -= 5f;
                else if (IsDislikedBait(fish, bait))
                    difficulty += 15f;
            }
        }

        var ev = new FishFightModifiersEvent(difficulty);
        RaiseLocalEvent(rod, ref ev);
        return ev.Difficulty;
    }

    private EntityUid? GetBaitItem(EntityUid rod)
    {
        if (!_itemSlots.TryGetSlot(rod, "bait", out var slot))
            return null;

        return slot.Item;
    }

    private bool IsFavoriteBait(FishPrototype fish, EntityUid baitItem)
    {
        if (MatchesBaitItem(fish.FavoriteBaits, baitItem))
            return true;

        if (MatchesBaitReagents(fish.FavoriteReagents, baitItem))
            return true;

        return MatchesBaitTraits(fish.FavoriteBaitTraits, baitItem);
    }

    private bool IsDislikedBait(FishPrototype fish, EntityUid baitItem)
    {
        if (MatchesBaitItem(fish.DislikedBaits, baitItem))
            return true;

        if (MatchesBaitReagents(fish.DislikedReagents, baitItem))
            return true;

        return MatchesBaitTraits(fish.DislikedBaitTraits, baitItem);
    }

    private bool MatchesBaitItem(List<EntProtoId> baits, EntityUid baitItem)
    {
        if (baits.Count == 0)
            return false;

        var protoId = MetaData(baitItem).EntityPrototype?.ID;
        if (protoId == null)
            return false;

        foreach (var bait in baits)
        {
            if (bait.Id == protoId)
                return true;
        }

        return false;
    }

    private bool MatchesBaitReagents(Dictionary<ProtoId<ReagentPrototype>, float> reagents, EntityUid baitItem)
    {
        foreach (var (reagent, amount) in reagents)
        {
            if (_solutions.GetTotalPrototypeQuantity(baitItem, reagent.Id) >= amount)
                return true;
        }

        return false;
    }

    private bool MatchesBaitTraits(List<ProtoId<FishTraitPrototype>> traits, EntityUid baitItem)
    {
        if (traits.Count == 0 || !TryComp<FishComponent>(baitItem, out var baitFish))
            return false;

        if (baitFish.Fish is not { } species || !_proto.TryIndex(species, out FishPrototype? baitProto))
            return false;

        foreach (var trait in baitProto.Traits)
        {
            if (baitFish.RemovedTraits.Contains(trait))
                continue;

            if (traits.Contains(trait))
                return true;
        }

        foreach (var trait in baitFish.ExtraTraits)
        {
            if (baitFish.RemovedTraits.Contains(trait))
                continue;

            if (traits.Contains(trait))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Rolls the loot of the source without spawning anything. Used at the biting phase so the
    /// minigame already knows what it fights for. Dynamic sources build the loot from their host.
    /// </summary>
    public bool TryRollReward(
        EntityUid sourceEntity,
        ProtoId<FishingSourcePrototype> sourceId,
        EntityUid user,
        EntityUid rod,
        out EntProtoId? reward,
        out EntityUid? existingCatch)
    {
        reward = null;
        existingCatch = null;

        if (!_net.IsServer)
            return false;

        if (!_proto.TryIndex(sourceId, out FishingSourcePrototype? source))
            return false;

        var lootEv = new FishLootModifiersEvent(user, rod, sourceId);
        RaiseLocalEvent(rod, ref lootEv);

        var counts = GetRemaining(source);

        if (source.Dynamic != FishingDynamicSource.None)
        {
            var dynamicEv = new FishingDynamicRewardEvent(user, rod, sourceEntity);
            RaiseLocalEvent(sourceEntity, ref dynamicEv);

            if (dynamicEv.ExistingCatch is { } existing && !TerminatingOrDeleted(existing))
            {
                existingCatch = existing;
                return true;
            }

            var candidates = dynamicEv.Candidates
                .Where(c => !source.FishCounts.ContainsKey(c) ||
                            (counts.TryGetValue(c, out var left) && left > 0))
                .ToList();

            if (!dynamicEv.Handled || candidates.Count == 0)
                return true;

            reward = candidates[_random.Next(candidates.Count)];
            return true;
        }

        var spawns = RollTable(source, lootEv, counts).ToList();

        for (var i = 0; i < lootEv.DudRerolls && spawns.Count == 0; i++)
        {
            spawns = RollTable(source, lootEv, counts).ToList();
        }

        if (spawns.Count == 0)
            return true;

        reward = spawns[_random.Next(spawns.Count)];
        return true;
    }

    private IEnumerable<EntProtoId> RollTable(FishingSourcePrototype source, FishLootModifiersEvent lootEv, Dictionary<EntProtoId, int> counts)
    {
        foreach (var spawn in _tables.GetSpawns(source.Table, _random.GetRandom()))
        {
            if (source.FishCounts.ContainsKey(spawn) &&
                (!counts.TryGetValue(spawn, out var left) || left <= 0))
                continue;

            yield return spawn;
        }
    }

    /// <summary>
    /// Spawns the given catch and hands it to the user. Existing catch is used by sources like
    /// aquariums, which hand out the actual fish that lives in the tank.
    /// </summary>
    public EntityUid? CatchFish(
        EntityUid user,
        EntityUid rod,
        EntProtoId? reward,
        EntityCoordinates coords,
        ProtoId<FishingSourcePrototype>? sourceId = null,
        EntityUid? existingCatch = null)
    {
        if (!_net.IsServer)
            return null;

        EntityUid caught;

        if (existingCatch is { } existing && !TerminatingOrDeleted(existing))
        {
            if (_container.TryGetContainingContainer((existing, null, null), out var container))
                _container.Remove(existing, container);

            _xform.SetCoordinates(existing, coords);
            caught = existing;
        }
        else if (reward is { } rewardId)
        {
            caught = Spawn(rewardId, coords);
        }
        else
        {
            return null;
        }

        if (sourceId is { } id &&
            _proto.TryIndex(id, out FishingSourcePrototype? source) &&
            reward is { } rewardId2 &&
            source.FishCounts.ContainsKey(rewardId2))
        {
            var counts = GetRemaining(source);
            if (counts.TryGetValue(rewardId2, out var left) && left > 0)
                counts[rewardId2] = left - 1;

            if (source.FishCountRegen.TryGetValue(rewardId2, out var regen))
            {
                if (!_regenTimers.TryGetValue(source.ID, out var timers))
                {
                    timers = new Dictionary<EntProtoId, float>();
                    _regenTimers[source.ID] = timers;
                }

                timers.TryAdd(rewardId2, regen);
            }
        }

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