// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Events;
using Content.Shared._Mini.Fishing.Prototypes;
using Robust.Shared.Containers;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.EntityTable;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Prototypes;
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
                if (!_proto.TryIndex(traitId, out FishTraitPrototype? trait))
                    continue;

                difficulty += trait.AddedDifficulty;

                // Wary fish see ordinary lines and are harder to fight.
                if ((trait.Flags & FishTraitFlags.Wary) != 0 && !HasCloakedLine(rod))
                    difficulty += 5f;
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

    /// <summary>
    /// Bait or lure currently slotted into the rod, if any.
    /// </summary>
    public EntityUid? GetBaitItem(EntityUid rod)
    {
        return GetSlotItem(rod, "bait");
    }

    /// <summary>
    /// Hook currently slotted into the rod, if any.
    /// </summary>
    public EntityUid? GetHookItem(EntityUid rod)
    {
        return GetSlotItem(rod, "hook");
    }

    /// <summary>
    /// Fishing line currently slotted into the rod, if any.
    /// </summary>
    public EntityUid? GetLineItem(EntityUid rod)
    {
        return GetSlotItem(rod, "line");
    }

    private EntityUid? GetSlotItem(EntityUid rod, string slotId)
    {
        if (!_itemSlots.TryGetSlot(rod, slotId, out var slot))
            return null;

        return slot.Item;
    }

    private bool HasCloakedLine(EntityUid rod)
    {
        return GetLineItem(rod) is { } lineItem &&
               TryComp<FishingLineComponent>(lineItem, out var line) &&
               line.IsCloaked;
    }

    /// <summary>
    /// Whether the bait or lure is a favorite of this fish species.
    /// </summary>
    public bool IsFavoriteBait(FishPrototype fish, EntityUid baitItem)
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
    /// Weight multiplier of a fish species for this rod and bait, from its traits. Zero means
    /// the fish will not take the bait at all, like a picky eater on plain bait.
    /// </summary>
    public float GetTraitCatchWeightMultiplier(FishPrototype fish, EntityUid rod, EntityUid? baitItem)
    {
        var multiplier = 1f;
        FishingBaitComponent? baitComp = null;
        if (baitItem is { } baitUid)
            TryComp(baitUid, out baitComp);

        var shinyHook = baitComp?.AttractsShinyLovers ?? false;
        if (GetHookItem(rod) is { } hookItem && TryComp<FishingHookComponent>(hookItem, out var hook))
            shinyHook |= hook.AttractsShinyLovers;

        foreach (var traitId in fish.Traits)
        {
            if (!_proto.TryIndex(traitId, out FishTraitPrototype? trait))
                continue;

            // Shiny lovers are twice as hard to find without a shiny hook.
            if ((trait.Flags & FishTraitFlags.ShinyLover) != 0 && !shinyHook)
                multiplier *= 0.5f;

            if ((trait.Flags & FishTraitFlags.PickyEater) != 0)
            {
                if (baitItem is not { } pickyBait)
                    return 0f;

                if (IsFavoriteBait(fish, pickyBait))
                    continue;

                // Good bait or an artificial lure satisfies a picky eater.
                var acceptable = baitComp != null &&
                                 ((baitComp.Flags & FishingBaitFlags.Artificial) != 0 ||
                                  baitComp.Quality != FishingBaitQuality.Basic);

                if (baitComp != null && !acceptable)
                    return 0f;
            }

            if ((trait.Flags & FishTraitFlags.Carnivore) != 0)
            {
                if (baitItem == null)
                    return 0f;

                if (baitComp != null &&
                    (baitComp.Flags & (FishingBaitFlags.Meat | FishingBaitFlags.Artificial)) == 0)
                    return 0f;
            }

            if ((trait.Flags & FishTraitFlags.Vegan) != 0)
            {
                if (baitItem == null)
                    return 0f;

                if (baitComp != null &&
                    (baitComp.Flags & (FishingBaitFlags.Produce | FishingBaitFlags.Artificial)) == 0)
                    return 0f;
            }
        }

        return multiplier;
    }

    /// <summary>tg FISH_BEAUTY_DISGUSTING.</summary>
    private const int DisgustingBeauty = -500;

    private static readonly ProtoId<FishingSourcePrototype> ToiletSource = "FishingSourceToilet";
    private static readonly ProtoId<FishingSourcePrototype> MoistureTrapSource = "FishingSourceMoistureTrap";

    /// <summary>
    /// Weight multiplier of a fish species for the artificial lure on this rod. Zero means the
    /// lure cannot catch it at all, a matching fish gets tg's ten times the weight.
    /// </summary>
    public float GetLureCatchWeightMultiplier(
        FishPrototype fish,
        EntityUid rod,
        EntityUid? baitItem,
        ProtoId<FishingSourcePrototype> sourceId)
    {
        if (baitItem is not { } baitUid ||
            !TryComp<FishingBaitComponent>(baitUid, out var bait) ||
            bait.LureFilter is not { } filter)
            return 1f;

        return MatchesLureFilter(fish, filter, sourceId) ? filter.WeightMultiplier : 0f;
    }

    private bool MatchesLureFilter(FishPrototype fish, FishingLureFilter filter, ProtoId<FishingSourcePrototype> sourceId)
    {
        if (fish.AverageSize < filter.MinSize || fish.AverageSize > filter.MaxSize)
            return false;

        // The plastic dropping also catches the junk of the toilet and of the moisture trap.
        if (filter.JunkFish &&
            fish.Beauty > DisgustingBeauty &&
            sourceId != ToiletSource &&
            sourceId != MoistureTrapSource)
            return false;

        var flags = GetFishTraitFlags(fish);

        if ((flags & filter.ExcludedTraits) != 0)
            return false;

        if ((flags & filter.RequiredTraits) != filter.RequiredTraits)
            return false;

        if (filter.RequiredAnyTraits != FishTraitFlags.None && (flags & filter.RequiredAnyTraits) == 0)
            return false;

        if (filter.Fluids is { } fluids)
        {
            if (!fluids.Contains(fish.Fluid))
                return false;

            // tg: an air fish only bites a lure when it is amphibious.
            if (fish.Fluid == AquariumFluid.Air && (flags & FishTraitFlags.Amphibious) == 0)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Flags of every trait of this fish species.
    /// </summary>
    public FishTraitFlags GetFishTraitFlags(FishPrototype fish)
    {
        var flags = FishTraitFlags.None;
        foreach (var traitId in fish.Traits)
        {
            if (_proto.TryIndex(traitId, out FishTraitPrototype? trait))
                flags |= trait.Flags;
        }

        return flags;
    }

    /// <summary>
    /// Additional minigame rules granted by the traits of the caught fish.
    /// </summary>
    public FishingMinigameRules GetTraitMinigameRules(FishPrototype fish)
    {
        var rules = FishingMinigameRules.None;

        foreach (var traitId in fish.Traits)
        {
            if (!_proto.TryIndex(traitId, out FishTraitPrototype? trait))
                continue;

            if ((trait.Flags & FishTraitFlags.Camouflage) != 0)
                rules |= FishingMinigameRules.Camo;

            if ((trait.Flags & FishTraitFlags.Antigrav) != 0)
                rules |= FishingMinigameRules.Antigrav;
        }

        return rules;
    }

    /// <summary>
    /// Sum of the idle velocity changes the traits of this fish apply in the minigame.
    /// </summary>
    public float GetTraitIdleVelocityBonus(FishPrototype fish)
    {
        var bonus = 0f;

        foreach (var traitId in fish.Traits)
        {
            if (_proto.TryIndex(traitId, out FishTraitPrototype? trait))
                bonus += trait.IdleVelocityBonus;
        }

        return bonus;
    }

    /// <summary>
    /// Product of the bait control speed multipliers of the traits of this fish.
    /// </summary>
    public float GetTraitControlSpeedMultiplier(FishPrototype fish)
    {
        var multiplier = 1f;

        foreach (var traitId in fish.Traits)
        {
            if (_proto.TryIndex(traitId, out FishTraitPrototype? trait))
                multiplier *= trait.ControlSpeedMultiplier;
        }

        return multiplier;
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

        reward = RollTableReward(source, lootEv, rod, counts);
        return true;
    }

    /// <summary>
    /// How many table rolls one catch may take. A high fish weight multiplier rejects most
    /// non-fish rolls, so the loop is bounded instead of running until a fish shows up.
    /// </summary>
    private const int MaxRollAttempts = 64;

    private EntProtoId? RollTableReward(
        FishingSourcePrototype source,
        FishLootModifiersEvent lootEv,
        EntityUid rod,
        Dictionary<EntProtoId, int> counts)
    {
        var fishMultiplier = MathF.Max(0f, lootEv.FishWeightMultiplier);
        // Multipliers above one are simulated by accepting other entries less often.
        var scale = MathF.Max(1f, fishMultiplier);
        var bait = GetBaitItem(rod);

        for (var attempt = 0; attempt < MaxRollAttempts; attempt++)
        {
            var spawns = RollTable(source, lootEv, counts).ToList();

            if (spawns.Count == 0)
            {
                if (lootEv.RemoveDuds)
                    continue;

                if (lootEv.DudRerolls > 0)
                {
                    lootEv.DudRerolls--;
                    continue;
                }

                return null;
            }

            var picked = spawns[_random.Next(spawns.Count)];
            var weight = 1f;

            if (TryGetCaughtFish(picked, out var fish))
            {
                weight = fishMultiplier *
                         GetTraitCatchWeightMultiplier(fish, rod, bait) *
                         GetLureCatchWeightMultiplier(fish, rod, bait, new ProtoId<FishingSourcePrototype>(source.ID));
            }
            else if (IsLivingCatch(picked))
                weight = fishMultiplier;

            if (weight <= 0f)
                continue;

            if (_random.NextFloat() * scale < weight)
                return picked;
        }

        return null;
    }

    /// <summary>
    /// Fish species of a caught prototype, if that prototype has species data.
    /// </summary>
    public bool TryGetCaughtFish(EntProtoId id, [NotNullWhen(true)] out FishPrototype? fish)
    {
        fish = default!;

        if (!_proto.TryIndex(id, out EntityPrototype? proto) ||
            !proto.TryGetComponent<FishComponent>(out var comp, EntityManager.ComponentFactory) ||
            comp.Fish is not { } fishId)
        {
            return false;
        }

        return _proto.TryIndex(fishId, out fish);
    }

    private bool IsLivingCatch(EntProtoId id)
    {
        return _proto.TryIndex(id, out EntityPrototype? proto) && proto.HasComponent<MobStateComponent>();
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

            var caughtEv = new FishCaughtEvent(caught, sourceId);
            RaiseLocalEvent(user, ref caughtEv);

            _popup.PopupEntity(Loc.GetString("fishing-caught-fish", ("fish", Name(caught))), user, user);
        }

        // Items and mobs are pulled out of the water towards the fisher. A structure like a
        // treasure chest would only topple over, so it is set down next to the fisher instead.
        if (HasComp<ItemComponent>(caught) || HasComp<MobStateComponent>(caught))
        {
            var userPos = _xform.GetWorldPosition(user);
            var catchPos = _xform.GetWorldPosition(caught);
            var direction = userPos - catchPos;
            if (direction.LengthSquared() > 0.01f)
                _throwing.TryThrow(caught, direction, 4f, user);
        }
        else
        {
            _xform.SetCoordinates(caught, Transform(user).Coordinates);
        }

        return caught;
    }
}