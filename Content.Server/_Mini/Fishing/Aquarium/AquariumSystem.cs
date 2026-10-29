// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Linq;
using Content.Shared._Mini.Fishing;
using Content.Shared._Mini.Fishing.Aquarium;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Events;
using Content.Shared._Mini.Fishing.Prototypes;
using Content.Shared._Mini.Fishing.Systems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.UserInterface;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Mini.Fishing.Aquarium;

/// <summary>
/// Runs the aquarium life cycle: hunger, growth, breeding, evolution, temperature drift and glass breakage.
/// </summary>
public sealed partial class AquariumSystem : EntitySystem
{
    private const float MaxHealth = 100f;
    private const float BadEnvironmentDamage = 2.5f;
    private const float StarvationDamage = 0.25f;
    private const float HealthRegen = 0.5f;
    private const float GrowthMultiplier = 0.38f;
    private const float GrowthHungerPeak = 0.45f;
    private const float MaxDeviation = 2.5f;
    private const float GrowthTickRate = 0.001f;
    private const float GrowthEvolutionThreshold = 0.95f;
    private const float GrowthEvolutionChance = 0.02f;
    private const float DefaultBreedingTime = 120f;
    private const float BreedRetryDelay = 10f;
    private const int BluespaceMaxFishMultiplier = 5;
    private const string GlassStackType = "Glass";

    [Dependency] private SharedAquariumSystem _aquarium = default!;
    [Dependency] private FishSystem _fish = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;

    /// <summary>
    /// Health of every fish the tank is currently keeping alive. Fish without an entry start at full health.
    /// </summary>
    private readonly Dictionary<EntityUid, float> _health = new();

    /// <summary>
    /// The next time each fish may breed again.
    /// </summary>
    private readonly Dictionary<EntityUid, TimeSpan> _breedAt = new();

    /// <summary>
    /// Species prototype to entity prototype cache, so offspring can be spawned as the right fish.
    /// </summary>
    private readonly Dictionary<string, EntProtoId> _speciesToEntity = new();

    private float _tick;
    private int _uiTick;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AquariumComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AquariumComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<AquariumComponent, DamageChangedEvent>(OnDamaged);
        SubscribeLocalEvent<AquariumComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<AquariumComponent, AfterActivatableUIOpenEvent>(OnUiOpened);

        SubscribeLocalEvent<AquariumComponent, AquariumSetTemperatureMessage>(OnSetTemperature);
        SubscribeLocalEvent<AquariumComponent, AquariumSetFluidMessage>(OnSetFluid);
        SubscribeLocalEvent<AquariumComponent, AquariumSetModeMessage>(OnSetMode);
        SubscribeLocalEvent<AquariumComponent, AquariumRemoveMessage>(OnRemove);
        SubscribeLocalEvent<AquariumComponent, AquariumFeedMessage>(OnFeed);
        SubscribeLocalEvent<AquariumComponent, FishingDynamicRewardEvent>(OnDynamicReward);

        _proto.PrototypesReloaded += OnPrototypesReloaded;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _proto.PrototypesReloaded -= OnPrototypesReloaded;
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        _speciesToEntity.Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        _tick += frameTime;
        if (_tick < 1f)
            return;

        _tick -= 1f;
        _uiTick++;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AquariumComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var ent = (uid, comp);
            TickAquarium(ent, 1f, now);

            if (_uiTick % 2 == 0 && _ui.IsUiOpen(uid, AquariumUiKey.Key))
                _aquarium.UpdateUi(ent);
        }
    }

    private void TickAquarium(Entity<AquariumComponent> ent, float seconds, TimeSpan now)
    {
        var fishes = _aquarium.GetFish(ent);

        if (ent.Comp.Mode == AquariumMode.Assisted)
            ApplyAssistedFluid(ent, fishes);

        var target = ent.Comp.Mode switch
        {
            AquariumMode.Assisted => GetAssistedTemperature(ent, fishes),
            AquariumMode.Stasis => ent.Comp.RoomTemperature,
            _ => ent.Comp.Temperature,
        };

        var step = ent.Comp.TemperatureDrift * seconds;
        var temperature = MoveTowards(ent.Comp.Temperature, target, step);
        if (MathF.Abs(temperature - ent.Comp.Temperature) > 0.001f)
        {
            ent.Comp.Temperature = temperature;
            Dirty(ent, ent.Comp);
        }

        var blocked = IsBlocked(ent, fishes, now);

        foreach (var fish in fishes)
            TickFish(ent, fish, seconds, blocked, now);

        if (_aquarium.UpdateBeauty(ent))
            _aquarium.UpdateUi(ent);
    }

    private void TickFish(Entity<AquariumComponent> aquarium, Entity<FishComponent> fish, float seconds, bool blocked,
        TimeSpan now)
    {
        var uid = fish.Owner;

        if (!fish.Comp.Alive)
        {
            _health.Remove(uid);
            return;
        }

        var proto = _aquarium.GetProto(fish.Comp);
        if (!_health.TryGetValue(uid, out var health))
            health = MaxHealth;

        if (!_breedAt.ContainsKey(uid))
            _breedAt[uid] = now + TimeSpan.FromSeconds(proto?.BreedingTime ?? DefaultBreedingTime);

        if (blocked)
        {
            _health[uid] = health;
            return;
        }

        var fluidOk = proto == null || _aquarium.IsFluidCompatible(proto, aquarium.Comp.Fluid);
        var temperatureOk = proto == null || _aquarium.IsTemperatureSafe(proto, aquarium.Comp.Temperature);
        var hunger = _aquarium.GetHunger(fish.Comp, proto, now);

        var change = 0f;
        if (!fluidOk || !temperatureOk)
            change -= BadEnvironmentDamage;
        else if (hunger > 1f)
            change -= StarvationDamage * hunger;
        else
            change += HealthRegen;

        health = Math.Clamp(health + change * seconds, 0f, MaxHealth);
        _health[uid] = health;

        if (health <= 0f)
        {
            _fish.SetAlive(fish, false);
            _aquarium.UpdateBeauty(aquarium);
            return;
        }

        if (proto == null || health < MaxHealth * 0.8f || hunger >= 1f)
            return;

        GrowFish(fish, proto, seconds);

        if (TryGrowthEvolution(aquarium, fish, proto, now))
            return;

        if (_breedAt.TryGetValue(uid, out var breedAt) && now >= breedAt && !TryBreed(aquarium, fish, proto, now))
            _breedAt[uid] = now + TimeSpan.FromSeconds(BreedRetryDelay);
    }

    #region Growth

    /// <summary>
    /// A fed and healthy fish slowly grows towards the maximum the species can reach.
    /// </summary>
    private void GrowFish(Entity<FishComponent> fish, FishPrototype proto, float seconds)
    {
        var maxSize = proto.AverageSize * MaxDeviation;
        var maxWeight = proto.AverageWeight * MaxDeviation;
        var factor = 1f - MathF.Exp(-GrowthTickRate * seconds);

        var size = fish.Comp.Size;
        var weight = fish.Comp.Weight;
        var newSize = size < maxSize ? MathF.Min(maxSize, size + (maxSize - size) * factor) : size;
        var newWeight = weight < maxWeight ? MathF.Min(maxWeight, weight + (maxWeight - weight) * factor) : weight;

        if (MathF.Abs(newSize - size) < 0.01f && MathF.Abs(newWeight - weight) < 0.01f)
            return;

        fish.Comp.Size = newSize;
        fish.Comp.Weight = newWeight;
        Dirty(fish.Owner, fish.Comp);
    }

    /// <summary>
    /// A fish that is almost at its maximum size may turn into the growth evolution of its species.
    /// </summary>
    private bool TryGrowthEvolution(Entity<AquariumComponent> aquarium, Entity<FishComponent> fish,
        FishPrototype proto, TimeSpan now)
    {
        if (proto.EvolutionTypes.Count == 0)
            return false;

        var maxSize = proto.AverageSize * MaxDeviation;
        var maxWeight = proto.AverageWeight * MaxDeviation;
        if (fish.Comp.Size < maxSize * GrowthEvolutionThreshold ||
            fish.Comp.Weight < maxWeight * GrowthEvolutionThreshold)
            return false;

        foreach (var id in proto.EvolutionTypes)
        {
            if (!_proto.TryIndex(id, out FishEvolutionPrototype? evolution) || !evolution.Growth)
                continue;

            if (aquarium.Comp.Temperature < evolution.TemperatureMin ||
                aquarium.Comp.Temperature > evolution.TemperatureMax)
                continue;

            if (!_random.Prob(GrowthEvolutionChance))
                continue;

            if (_proto.TryIndex(evolution.EvolvesTo, out FishPrototype? evolved) &&
                TryGetFishEntityProto(evolved, fish, out var entityProto))
                EvolveFish(aquarium, fish, evolved, evolution, entityProto, now);

            return true;
        }

        return false;
    }

    private void EvolveFish(Entity<AquariumComponent> aquarium, Entity<FishComponent> fish, FishPrototype evolved,
        FishEvolutionPrototype evolution, EntProtoId entityProto, TimeSpan now)
    {
        var newFish = Spawn(entityProto.Id, Transform(aquarium).Coordinates);
        if (!TryComp<FishComponent>(newFish, out var newComp))
        {
            QueueDel(newFish);
            return;
        }

        newComp.Fish = evolved.ID;
        newComp.Size = fish.Comp.Size;
        newComp.Weight = fish.Comp.Weight;
        newComp.Catcher = fish.Comp.Catcher;
        newComp.CatchDate = fish.Comp.CatchDate;
        newComp.LastFed = now;
        newComp.Initialized = true;
        newComp.ExtraTraits = new List<ProtoId<FishTraitPrototype>>(fish.Comp.ExtraTraits);
        newComp.RemovedTraits = new List<ProtoId<FishTraitPrototype>>(fish.Comp.RemovedTraits);
        Dirty(newFish, newComp);

        var evolvedEnt = new Entity<FishComponent>(newFish, newComp);
        foreach (var trait in evolution.NewTraits)
            _fish.AddTrait(evolvedEnt, trait);

        foreach (var trait in evolution.RemovedTraits)
            _fish.RemoveTrait(evolvedEnt, trait);

        if (_aquarium.TryGetFishContainer(aquarium, out var container))
        {
            _container.Insert(newFish, container);
            _container.Remove(fish.Owner, container);
        }

        _health.Remove(fish.Owner);
        _breedAt.Remove(fish.Owner);
        _health[newFish] = MaxHealth;
        _breedAt[newFish] = now + TimeSpan.FromSeconds(evolved.BreedingTime * 2f);

        QueueDel(fish.Owner);

        _aquarium.UpdateBeauty(aquarium);
        _aquarium.UpdateUi(aquarium);
    }

    #endregion

    /// <summary>
    /// Picks the fluid most fish in the tank can live in. Runs only in assisted mode.
    /// </summary>
    private void ApplyAssistedFluid(Entity<AquariumComponent> aquarium, List<Entity<FishComponent>> fishes)
    {
        if (fishes.Count == 0)
            return;

        var bestFluid = aquarium.Comp.Fluid;
        var bestScore = -1;

        foreach (var fluid in SharedAquariumSystem.Fluids)
        {
            var score = 0;
            foreach (var (_, fish) in fishes)
            {
                var proto = _aquarium.GetProto(fish);
                if (proto != null && _aquarium.IsFluidCompatible(proto, fluid))
                    score++;
            }

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestFluid = fluid;
        }

        if (bestFluid == aquarium.Comp.Fluid)
            return;

        aquarium.Comp.Fluid = bestFluid;
        Dirty(aquarium, aquarium.Comp);
    }

    private float GetAssistedTemperature(Entity<AquariumComponent> aquarium, List<Entity<FishComponent>> fishes)
    {
        var min = aquarium.Comp.MinTemperature;
        var max = aquarium.Comp.MaxTemperature;
        var any = false;

        foreach (var (_, fish) in fishes)
        {
            var proto = _aquarium.GetProto(fish);
            if (proto == null || !_aquarium.IsFluidCompatible(proto, aquarium.Comp.Fluid))
                continue;

            min = MathF.Max(min, proto.TemperatureMin);
            max = MathF.Min(max, proto.TemperatureMax);
            any = true;
        }

        if (!any)
            return aquarium.Comp.RoomTemperature;

        return Math.Clamp((min + max) / 2f, aquarium.Comp.MinTemperature, aquarium.Comp.MaxTemperature);
    }

    /// <summary>
    /// In stasis, and while an assisted tank is failing most of its fish, fish stop growing and breeding.
    /// </summary>
    private bool IsBlocked(Entity<AquariumComponent> aquarium, List<Entity<FishComponent>> fishes, TimeSpan now)
    {
        if (aquarium.Comp.Mode == AquariumMode.Stasis)
            return true;

        if (aquarium.Comp.Mode != AquariumMode.Assisted)
            return false;

        var alive = 0;
        var unhappy = 0;

        foreach (var (_, fish) in fishes)
        {
            if (!fish.Alive)
                continue;

            alive++;
            var proto = _aquarium.GetProto(fish);
            if (proto == null)
                continue;

            if (!_aquarium.IsFluidCompatible(proto, aquarium.Comp.Fluid) ||
                !_aquarium.IsTemperatureSafe(proto, aquarium.Comp.Temperature) ||
                _aquarium.GetHunger(fish, proto, now) > 1f)
                unhappy++;
        }

        return alive > 0 && unhappy * 2 > alive;
    }

    #region Breeding

    private bool TryBreed(Entity<AquariumComponent> aquarium, Entity<FishComponent> parent, FishPrototype parentProto,
        TimeSpan now)
    {
        var fishes = _aquarium.GetFish(aquarium);
        if (fishes.Count >= aquarium.Comp.MaxFish)
            return false;

        Entity<FishComponent>? partner = null;
        FishPrototype? partnerProto = null;

        if (!_aquarium.HasTrait(parentProto, FishTraitFlags.NoMating))
        {
            var candidates = new List<Entity<FishComponent>>();
            foreach (var fish in fishes)
            {
                if (fish.Owner == parent.Owner || !fish.Comp.Alive)
                    continue;

                var proto = _aquarium.GetProto(fish.Comp);
                if (proto == null || !CanMate(parentProto, proto))
                    continue;

                if (CountSpecies(fishes, proto.ID) >= proto.StablePopulation)
                    continue;

                if (!_breedAt.TryGetValue(fish.Owner, out var breedAt) || now < breedAt)
                    continue;

                candidates.Add(fish);
            }

            if (candidates.Count > 0)
            {
                partner = _random.Pick(candidates);
                partnerProto = _aquarium.GetProto(partner.Value.Comp);
            }
        }

        var selfReproduce = _aquarium.HasTrait(parentProto, FishTraitFlags.Parthenogenesis);
        if (partner == null && !selfReproduce)
            return false;

        var chosen = PickEvolution(parentProto, partnerProto, aquarium.Comp.Temperature);
        if (chosen == null)
        {
            if (partnerProto == null)
            {
                chosen = GetProtoById(_random.Pick(GetSpawnTypes(parentProto)));
            }
            else
            {
                var own = GetSpawnTypes(parentProto);
                var theirs = GetSpawnTypes(partnerProto);
                var parentRecessive = _aquarium.HasTrait(parentProto, FishTraitFlags.Recessive);
                var partnerRecessive = _aquarium.HasTrait(partnerProto, FishTraitFlags.Recessive);
                var overpopulated = CountSpecies(fishes, parentProto.ID) >= parentProto.StablePopulation;

                List<ProtoId<FishPrototype>> picks;
                if (overpopulated)
                {
                    if (partnerRecessive && !parentRecessive)
                        return false;

                    picks = theirs;
                }
                else if (parentRecessive && !partnerRecessive)
                {
                    picks = theirs;
                }
                else if (partnerRecessive && !parentRecessive)
                {
                    picks = own;
                }
                else
                {
                    picks = own.Concat(theirs).ToList();
                }

                chosen = GetProtoById(_random.Pick(picks));
            }
        }

        if (chosen == null)
            return false;

        SpawnOffspring(aquarium, parent, partner, chosen, now);
        _breedAt[parent.Owner] = now + TimeSpan.FromSeconds(parentProto.BreedingTime);
        if (partner is { } other)
            _breedAt[other.Owner] = now + TimeSpan.FromSeconds(parentProto.BreedingTime);

        return true;
    }

    private bool CanMate(FishPrototype first, FishPrototype second)
    {
        if (first.ID == second.ID)
            return true;

        if (_aquarium.HasTrait(first, FishTraitFlags.Crossbreeder))
            return true;

        foreach (var id in first.CompatibleTypes)
        {
            if (id.Id == second.ID)
                return true;
        }

        return false;
    }

    private FishPrototype? PickEvolution(FishPrototype parent, FishPrototype? partner, float temperature)
    {
        var possible = new List<FishPrototype>();

        AddEvolutions(parent, temperature, possible);
        if (partner != null)
            AddEvolutions(partner, temperature, possible);

        return possible.Count == 0 ? null : _random.Pick(possible);
    }

    private void AddEvolutions(FishPrototype source, float temperature, List<FishPrototype> possible)
    {
        foreach (var id in source.EvolutionTypes)
        {
            if (!_proto.TryIndex(id, out FishEvolutionPrototype? evolution))
                continue;

            if (temperature < evolution.TemperatureMin || temperature > evolution.TemperatureMax)
                continue;

            if (!_random.Prob(Math.Clamp(evolution.Probability / 100f, 0f, 1f)))
                continue;

            var evolved = GetProtoById(evolution.EvolvesTo);
            if (evolved != null)
                possible.Add(evolved);
        }
    }

    private static List<ProtoId<FishPrototype>> GetSpawnTypes(FishPrototype proto)
    {
        return proto.SpawnTypes.Count > 0 ? proto.SpawnTypes : new List<ProtoId<FishPrototype>> { proto.ID };
    }

    private int CountSpecies(List<Entity<FishComponent>> fishes, string species)
    {
        var count = 0;
        foreach (var (_, fish) in fishes)
        {
            if (_aquarium.GetProto(fish)?.ID == species)
                count++;
        }

        return count;
    }

    private FishPrototype? GetProtoById(ProtoId<FishPrototype> id)
    {
        return _proto.TryIndex(id, out FishPrototype? proto) ? proto : null;
    }

    private void SpawnOffspring(Entity<AquariumComponent> aquarium, Entity<FishComponent> parent,
        Entity<FishComponent>? partner, FishPrototype species, TimeSpan now)
    {
        if (!TryGetFishEntityProto(species, parent, out var entityProto))
            return;

        var child = Spawn(entityProto.Id, Transform(aquarium).Coordinates);
        if (!TryComp<FishComponent>(child, out var childFish))
        {
            QueueDel(child);
            return;
        }

        var size = parent.Comp.Size;
        var weight = parent.Comp.Weight;
        if (partner is { } other)
        {
            size = (size + other.Comp.Size) / 2f;
            weight = (weight + other.Comp.Weight) / 2f;
        }

        childFish.Fish = species.ID;
        childFish.Size = size;
        childFish.Weight = weight;
        childFish.LastFed = now;
        childFish.Initialized = true;
        Dirty(child, childFish);

        _health[child] = MaxHealth;
        _breedAt[child] = now + TimeSpan.FromSeconds(species.BreedingTime * 2f);

        if (_aquarium.TryGetFishContainer(aquarium, out var container))
            _container.Insert(child, container);

        _aquarium.UpdateBeauty(aquarium);
        _aquarium.UpdateUi(aquarium);
    }

    /// <summary>
    /// Finds the entity prototype of a fish species by looking for a fish entity that points at it.
    /// </summary>
    private bool TryGetFishEntityProto(FishPrototype species, Entity<FishComponent> fallback, out EntProtoId entityProto)
    {
        if (_speciesToEntity.Count == 0)
            RebuildSpeciesCache();

        if (_speciesToEntity.TryGetValue(species.ID, out entityProto))
            return true;

        // Fall back to the parent so a fish is still born even if the species has no entity yet.
        if (Prototype(fallback.Owner) is not { } proto)
        {
            entityProto = default;
            return false;
        }

        entityProto = proto.ID;
        return true;
    }

    private void RebuildSpeciesCache()
    {
        _speciesToEntity.Clear();

        foreach (var entityProto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (entityProto.Abstract)
                continue;

            if (!entityProto.TryGetComponent<FishComponent>("Fish", out var fish))
                continue;

            if (fish.Fish is not { } species)
                continue;

            _speciesToEntity.TryAdd(species.Id, entityProto.ID);
        }
    }

    #endregion

    #region Interaction

    private void OnInteractUsing(Entity<AquariumComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var used = args.Used;

        if (TryComp<FishComponent>(used, out _))
        {
            InsertFish(ent, args.User, used);
            args.Handled = true;
            return;
        }

        if (TryComp<AquariumPropComponent>(used, out _))
        {
            InsertProp(ent, args.User, used);
            args.Handled = true;
            return;
        }

        if (TryComp<FishFeedComponent>(used, out var feed))
        {
            Feed(ent, args.User, used, feed);
            args.Handled = true;
            return;
        }

        if (TryComp<AquariumUpgradeComponent>(used, out var upgrade))
        {
            args.Handled = TryUpgrade(ent, args.User, used, upgrade);
            return;
        }

        if (TryComp<StackComponent>(used, out var stack) && stack.StackTypeId.Id == GlassStackType)
        {
            TryRepair(ent, args.User, used);
            args.Handled = true;
        }
    }

    private void InsertFish(Entity<AquariumComponent> ent, EntityUid user, EntityUid item)
    {
        if (!TryComp<FishComponent>(item, out var fish))
            return;

        if (ent.Comp.GlassBroken)
        {
            _popup.PopupEntity(Loc.GetString("aquarium-broken"), ent, user);
            return;
        }

        var fishes = _aquarium.GetFish(ent);
        if (fishes.Count >= ent.Comp.MaxFish)
        {
            _popup.PopupEntity(Loc.GetString("aquarium-full"), ent, user);
            return;
        }

        var proto = _aquarium.GetProto(fish);

        if (proto != null && ent.Comp.Mode == AquariumMode.Assisted)
        {
            ApplyAssistedFluid(ent, fishes);
            ent.Comp.Temperature = Math.Clamp(GetAssistedTemperature(ent, fishes),
                ent.Comp.MinTemperature, ent.Comp.MaxTemperature);
            Dirty(ent, ent.Comp);
        }
        else if (proto != null)
        {
            if (!_aquarium.IsFluidCompatible(proto, ent.Comp.Fluid))
            {
                _popup.PopupEntity(Loc.GetString("aquarium-incompatible-fluid"), ent, user);
                return;
            }

            if (!_aquarium.IsTemperatureSafe(proto, ent.Comp.Temperature))
            {
                _popup.PopupEntity(Loc.GetString("aquarium-incompatible-temperature"), ent, user);
                return;
            }
        }

        if (!_aquarium.TryGetFishContainer(ent, out var container) || !_container.Insert(item, container))
            return;

        if (fish.LastFed is null)
        {
            fish.LastFed = _timing.CurTime;
            Dirty(item, fish);
        }

        _health[item] = MaxHealth;
        _breedAt[item] = _timing.CurTime + TimeSpan.FromSeconds(proto?.BreedingTime ?? DefaultBreedingTime);

        _popup.PopupEntity(Loc.GetString("aquarium-fish-added"), ent, user);
        _aquarium.UpdateBeauty(ent);
        _aquarium.UpdateUi(ent);
    }

    private void InsertProp(Entity<AquariumComponent> ent, EntityUid user, EntityUid item)
    {
        if (ent.Comp.GlassBroken)
        {
            _popup.PopupEntity(Loc.GetString("aquarium-broken"), ent, user);
            return;
        }

        if (!_aquarium.TryGetPropContainer(ent, out var container))
            return;

        var prototype = Prototype(item)?.ID;
        foreach (var uid in container.ContainedEntities)
        {
            if (Prototype(uid)?.ID != prototype)
                continue;

            _popup.PopupEntity(Loc.GetString("aquarium-prop-duplicate"), ent, user);
            return;
        }

        if (!_container.Insert(item, container))
            return;

        _popup.PopupEntity(Loc.GetString("aquarium-prop-added"), ent, user);
        _aquarium.UpdateBeauty(ent);
        _aquarium.UpdateUi(ent);
    }

    private void Feed(Entity<AquariumComponent> ent, EntityUid user, EntityUid feedItem, FishFeedComponent feed)
    {
        if (feed.Uses <= 0)
        {
            _popup.PopupEntity(Loc.GetString("aquarium-feed-empty"), ent, user);
            return;
        }

        var fishes = _aquarium.GetFish(ent);
        if (fishes.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("aquarium-no-fish"), ent, user);
            return;
        }

        var now = _timing.CurTime;
        var blocked = IsBlocked(ent, fishes, now);

        foreach (var fish in fishes)
            FeedFish(fish, feed.Reagent, blocked, now);

        feed.Uses--;
        Dirty(feedItem, feed);

        _popup.PopupEntity(Loc.GetString(feed.Uses > 0 ? "aquarium-fed" : "aquarium-feed-last"), ent, user);
        _aquarium.UpdateBeauty(ent);
        _aquarium.UpdateUi(ent);
    }

    private void FeedFish(Entity<FishComponent> fish, ProtoId<ReagentPrototype> reagent, bool blocked, TimeSpan now)
    {
        if (!fish.Comp.Alive)
            return;

        var proto = _aquarium.GetProto(fish.Comp);
        if (!_aquarium.CanEat(proto, reagent))
            return;

        var hunger = _aquarium.GetHunger(fish.Comp, proto, now);
        fish.Comp.LastFed = now;
        Dirty(fish, fish.Comp);

        if (blocked || proto == null)
            return;

        var maxSize = proto.AverageSize * MaxDeviation;
        var maxWeight = proto.AverageWeight * MaxDeviation;
        var multiplier = GrowthMultiplier * GrowthHungerMultiplier(hunger);

        if (multiplier <= 0f)
            return;

        var size = fish.Comp.Size;
        var weight = fish.Comp.Weight;
        var newSize = MathF.Min(maxSize, size + MathF.Max(1f, (maxSize - size) * multiplier / 2f));
        var newWeight = MathF.Min(maxWeight, weight + MathF.Max(1f, (maxWeight - weight) * multiplier / 2f));

        if (MathF.Abs(newSize - size) < 0.01f && MathF.Abs(newWeight - weight) < 0.01f)
            return;

        fish.Comp.Size = newSize;
        fish.Comp.Weight = newWeight;
        Dirty(fish, fish.Comp);
    }

    private static float GrowthHungerMultiplier(float hunger)
    {
        if (hunger <= GrowthHungerPeak)
            return hunger / GrowthHungerPeak;

        return MathF.Max(0f, 1f - (hunger - GrowthHungerPeak) * 4f);
    }

    private bool TryUpgrade(Entity<AquariumComponent> ent, EntityUid user, EntityUid used, AquariumUpgradeComponent upgrade)
    {
        if (upgrade.Upgrade == AquariumUpgradeFlags.None)
            return false;

        if ((ent.Comp.Upgrades & upgrade.Upgrade) != 0)
        {
            _popup.PopupEntity(Loc.GetString("aquarium-upgrade-present"), ent, user);
            return true;
        }

        ent.Comp.Upgrades |= upgrade.Upgrade;

        if (upgrade.Upgrade.HasFlag(AquariumUpgradeFlags.BluespaceCapacity))
            ent.Comp.MaxFish *= BluespaceMaxFishMultiplier;

        Dirty(ent, ent.Comp);
        QueueDel(used);

        _popup.PopupEntity(Loc.GetString("aquarium-upgrade-applied"), ent, user);
        _aquarium.UpdateUi(ent);
        return true;
    }

    private void TryRepair(Entity<AquariumComponent> ent, EntityUid user, EntityUid used)
    {
        if (!ent.Comp.GlassBroken)
        {
            _popup.PopupEntity(Loc.GetString("aquarium-glass-intact"), ent, user);
            return;
        }

        if (!TryComp<StackComponent>(used, out var stack) || !_stack.TryUse((used, stack), 2))
        {
            _popup.PopupEntity(Loc.GetString("aquarium-glass-needs-glass"), ent, user);
            return;
        }

        ent.Comp.GlassBroken = false;
        Dirty(ent, ent.Comp);

        if (TryComp<DamageableComponent>(ent, out var damageable))
            _damageable.SetDamage(new Entity<DamageableComponent?>(ent.Owner, damageable), new DamageSpecifier());

        _popup.PopupEntity(Loc.GetString("aquarium-glass-repaired"), ent, user);
        _aquarium.UpdateUi(ent);
    }

    #endregion

    #region Damage and inspect

    private void OnDamaged(Entity<AquariumComponent> ent, ref DamageChangedEvent args)
    {
        if (ent.Comp.GlassBroken || !args.DamageIncreased)
            return;

        var total = (float) _damageable.GetDamage((ent.Owner, args.Damageable)).GetTotal();
        if (total < ent.Comp.GlassBreakDamage)
            return;

        BreakGlass(ent);
    }

    private void BreakGlass(Entity<AquariumComponent> ent)
    {
        ent.Comp.GlassBroken = true;
        Dirty(ent, ent.Comp);

        _popup.PopupEntity(Loc.GetString("aquarium-glass-broken"), ent, PopupType.MediumCaution);

        if (_aquarium.TryGetFishContainer(ent, out var fishContainer))
            _container.EmptyContainer(fishContainer);

        if (_aquarium.TryGetPropContainer(ent, out var propContainer))
            _container.EmptyContainer(propContainer);

        _health.Clear();
        _breedAt.Clear();

        _aquarium.UpdateBeauty(ent);
        _aquarium.UpdateUi(ent);
    }

    private void OnExamined(Entity<AquariumComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.GlassBroken)
        {
            args.PushMarkup(Loc.GetString("aquarium-examine-broken"));
            return;
        }

        args.PushMarkup(Loc.GetString("aquarium-examine-state",
            ("fluid", Loc.GetString($"aquarium-fluid-{ent.Comp.Fluid.ToString().ToLowerInvariant()}")),
            ("temperature", (int) ent.Comp.Temperature),
            ("mode", Loc.GetString($"aquarium-mode-{ent.Comp.Mode.ToString().ToLowerInvariant()}"))));
        args.PushMarkup(Loc.GetString("aquarium-examine-beauty", ("beauty", ent.Comp.Beauty)));
    }

    #endregion

    #region UI

    private void OnUiOpened(Entity<AquariumComponent> ent, ref AfterActivatableUIOpenEvent args)
    {
        _aquarium.UpdateUi(ent);
    }

    private void OnSetTemperature(Entity<AquariumComponent> ent, ref AquariumSetTemperatureMessage args)
    {
        if (ent.Comp.Mode != AquariumMode.Manual)
            return;

        ent.Comp.Temperature = Math.Clamp(args.Temperature, ent.Comp.MinTemperature, ent.Comp.MaxTemperature);
        Dirty(ent, ent.Comp);
        _aquarium.UpdateUi(ent);
    }

    private void OnSetFluid(Entity<AquariumComponent> ent, ref AquariumSetFluidMessage args)
    {
        if (ent.Comp.Mode != AquariumMode.Manual || !SharedAquariumSystem.Fluids.Contains(args.Fluid))
            return;

        ent.Comp.Fluid = args.Fluid;
        Dirty(ent, ent.Comp);
        _aquarium.UpdateBeauty(ent);
        _aquarium.UpdateUi(ent);
    }

    private void OnSetMode(Entity<AquariumComponent> ent, ref AquariumSetModeMessage args)
    {
        if (ent.Comp.Mode == args.Mode)
            return;

        ent.Comp.Mode = args.Mode;
        Dirty(ent, ent.Comp);
        _aquarium.UpdateUi(ent);
    }

    private void OnRemove(Entity<AquariumComponent> ent, ref AquariumRemoveMessage args)
    {
        if (!TryGetEntity(args.Entity, out var uid))
            return;

        BaseContainer? container = null;
        if (_aquarium.TryGetFishContainer(ent, out var fishContainer) && fishContainer.Contains(uid.Value))
            container = fishContainer;
        else if (_aquarium.TryGetPropContainer(ent, out var propContainer) && propContainer.Contains(uid.Value))
            container = propContainer;

        if (container == null)
            return;

        if (!_container.Remove(uid.Value, container))
            return;

        _health.Remove(uid.Value);
        _breedAt.Remove(uid.Value);

        if (args.Actor is { Valid: true } user)
        {
            if (!_hands.TryPickupAnyHand(user, uid.Value))
                _xform.SetCoordinates(uid.Value, Transform(ent).Coordinates);

            _popup.PopupEntity(Loc.GetString("aquarium-removed"), ent, user);
        }
        else
        {
            _xform.SetCoordinates(uid.Value, Transform(ent).Coordinates);
            _popup.PopupEntity(Loc.GetString("aquarium-removed"), ent);
        }
        _aquarium.UpdateBeauty(ent);
        _aquarium.UpdateUi(ent);
    }

    private void OnFeed(Entity<AquariumComponent> ent, ref AquariumFeedMessage args)
    {
        if (args.Actor is not { Valid: true } user)
            return;

        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (!TryComp<FishFeedComponent>(held, out var feed))
                continue;

            Feed(ent, user, held, feed);
            return;
        }

        _popup.PopupEntity(Loc.GetString("aquarium-need-feed"), ent, user);
    }

    #endregion

    #region Fishing

    /// <summary>
    /// Fishing in the tank gives out one of the living fish inside instead of rolling the loot table.
    /// </summary>
    private void OnDynamicReward(Entity<AquariumComponent> ent, ref FishingDynamicRewardEvent args)
    {
        args.Handled = true;

        if (ent.Comp.GlassBroken)
            return;

        var alive = new List<Entity<FishComponent>>();
        foreach (var fish in _aquarium.GetFish(ent))
        {
            if (fish.Comp.Alive)
                alive.Add(fish);
        }

        if (alive.Count == 0)
            return;

        args.ExistingCatch = _random.Pick(alive).Owner;
    }

    #endregion

    private void OnMapInit(Entity<AquariumComponent> ent, ref MapInitEvent args)
    {
        var now = _timing.CurTime;

        foreach (var (uid, fish) in _aquarium.GetFish(ent))
        {
            if (fish.LastFed is null)
            {
                fish.LastFed = now;
                Dirty(uid, fish);
            }

            _health[uid] = MaxHealth;
            _breedAt[uid] = now + TimeSpan.FromSeconds(_aquarium.GetProto(fish)?.BreedingTime ?? DefaultBreedingTime);
        }

        _aquarium.UpdateBeauty(ent);
    }

    private static float MoveTowards(float current, float target, float maxDelta)
    {
        if (MathF.Abs(target - current) <= maxDelta)
            return target;

        return current + MathF.Sign(target - current) * maxDelta;
    }
}
