// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Diagnostics.CodeAnalysis;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Prototypes;
using Content.Shared._Mini.Fishing.Systems;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Mini.Fishing.Aquarium;

/// <summary>
/// Shared queries and calculations for aquariums. The containers are the single source of truth
/// for what is inside a tank, the mutable aquarium state lives on <see cref="AquariumComponent"/>.
/// </summary>
public sealed partial class SharedAquariumSystem : EntitySystem
{
    /// <summary>
    /// Fallback feeding frequency for legacy fish that have no species prototype.
    /// </summary>
    public const float DefaultFeedingFrequency = 300f;

    /// <summary>
    /// Reagent the generic fish feed is made of. Fish without a special food eat this.
    /// </summary>
    public const string DefaultFeedReagent = "Nutriment";

    /// <summary>
    /// Fluids a tank can be filled with, in the order they are shown in the window.
    /// </summary>
    public static readonly AquariumFluid[] Fluids =
    {
        AquariumFluid.Freshwater,
        AquariumFluid.Saltwater,
        AquariumFluid.SulphWater,
        AquariumFluid.Air,
    };

    private const int DeadFishBeautyPenalty = -300;
    private const int GenericFishBeauty = 250;
    private const int MaxHappiness = 4;

    [Dependency] private FishSystem _fish = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private IGameTiming _timing = default!;

    public bool TryGetFishContainer(Entity<AquariumComponent> aquarium,
        [NotNullWhen(true)] out BaseContainer? container)
    {
        return _container.TryGetContainer(aquarium.Owner, AquariumComponent.FishContainerId, out container);
    }

    public bool TryGetPropContainer(Entity<AquariumComponent> aquarium,
        [NotNullWhen(true)] out BaseContainer? container)
    {
        return _container.TryGetContainer(aquarium.Owner, AquariumComponent.PropContainerId, out container);
    }

    public List<Entity<FishComponent>> GetFish(Entity<AquariumComponent> aquarium)
    {
        var fish = new List<Entity<FishComponent>>();
        if (!TryGetFishContainer(aquarium, out var container))
            return fish;

        foreach (var uid in container.ContainedEntities)
        {
            if (TryComp<FishComponent>(uid, out var comp))
                fish.Add((uid, comp));
        }

        return fish;
    }

    public List<Entity<AquariumPropComponent>> GetProps(Entity<AquariumComponent> aquarium)
    {
        var props = new List<Entity<AquariumPropComponent>>();
        if (!TryGetPropContainer(aquarium, out var container))
            return props;

        foreach (var uid in container.ContainedEntities)
        {
            if (TryComp<AquariumPropComponent>(uid, out var comp))
                props.Add((uid, comp));
        }

        return props;
    }

    public FishPrototype? GetProto(FishComponent fish)
    {
        return _fish.GetProto(fish);
    }

    public bool HasTrait(FishPrototype proto, FishTraitFlags flag)
    {
        foreach (var id in proto.Traits)
        {
            if (_proto.TryIndex(id, out FishTraitPrototype? trait) && (trait.Flags & flag) != 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Whether a fish survives in the given fluid. Mirrors tg's fish_compatible_fluid_types table.
    /// </summary>
    public bool IsFluidCompatible(FishPrototype proto, AquariumFluid fluid)
    {
        if (fluid == proto.Fluid)
            return true;

        switch (proto.Fluid)
        {
            case AquariumFluid.AnyWater:
                return fluid is AquariumFluid.Saltwater or AquariumFluid.Freshwater or AquariumFluid.SulphWater;
            case AquariumFluid.Anadromous:
                return fluid is AquariumFluid.Saltwater or AquariumFluid.Freshwater;
            default:
                // Amphibious water fish also survive an air filled tank.
                return fluid == AquariumFluid.Air && HasTrait(proto, FishTraitFlags.Amphibious);
        }
    }

    public bool IsTemperatureSafe(FishPrototype proto, float temperature)
    {
        return temperature >= proto.TemperatureMin && temperature <= proto.TemperatureMax;
    }

    /// <summary>
    /// Hunger from zero (fed) to one (starving). Above one the fish is taking starvation damage.
    /// </summary>
    public float GetHunger(FishComponent fish, FishPrototype? proto, TimeSpan now)
    {
        if (fish.LastFed is not { } fed)
            return 0f;

        var frequency = proto?.FeedingFrequency ?? DefaultFeedingFrequency;
        if (frequency <= 0f)
            return 0f;

        return Math.Clamp((float) (now - fed).TotalSeconds / frequency, 0f, 2f);
    }

    public bool CanEat(FishPrototype? proto, ProtoId<ReagentPrototype> reagent)
    {
        var needed = proto?.Food;
        return needed is not { } id || id == reagent;
    }

    public int GetHappiness(FishComponent fish, FishPrototype? proto, AquariumFluid fluid, float temperature,
        TimeSpan now)
    {
        if (!fish.Alive)
            return 0;

        var value = 1;
        if (GetHunger(fish, proto, now) < 0.5f)
            value++;

        if (proto == null || IsFluidCompatible(proto, fluid))
            value++;

        if (proto == null || IsTemperatureSafe(proto, temperature))
            value++;

        return Math.Clamp(value, 0, MaxHappiness);
    }

    public int GetFishBeauty(FishComponent fish, FishPrototype? proto)
    {
        var beauty = proto == null || proto.Beauty == 0 ? GenericFishBeauty : proto.Beauty;
        if (fish.Alive)
            return beauty;

        return Math.Clamp(beauty + DeadFishBeautyPenalty, -500, 0);
    }

    /// <summary>
    /// Recalculates the beauty of the tank and dirty the component if it changed.
    /// </summary>
    public bool UpdateBeauty(Entity<AquariumComponent> aquarium)
    {
        var beauty = aquarium.Comp.DefaultBeauty;

        foreach (var (_, fish) in GetFish(aquarium))
            beauty += GetFishBeauty(fish, _fish.GetProto(fish));

        foreach (var (_, prop) in GetProps(aquarium))
            beauty += prop.Beauty;

        if (aquarium.Comp.Beauty == beauty)
            return false;

        aquarium.Comp.Beauty = beauty;
        Dirty(aquarium, aquarium.Comp);
        return true;
    }

    public AquariumBoundUserInterfaceState BuildState(Entity<AquariumComponent> aquarium)
    {
        var now = _timing.CurTime;
        var fishes = GetFish(aquarium);
        var fishData = new AquariumFishData[fishes.Count];

        for (var i = 0; i < fishes.Count; i++)
        {
            var (uid, fish) = fishes[i];
            var proto = _fish.GetProto(fish);
            fishData[i] = new AquariumFishData(
                GetNetEntity(uid),
                Name(uid),
                Prototype(uid)?.ID ?? string.Empty,
                fish.Alive,
                GetHunger(fish, proto, now),
                GetHappiness(fish, proto, aquarium.Comp.Fluid, aquarium.Comp.Temperature, now),
                GetFishBeauty(fish, proto),
                fish.Size,
                fish.Weight);
        }

        var props = GetProps(aquarium);
        var propData = new AquariumPropData[props.Count];

        for (var i = 0; i < props.Count; i++)
        {
            var (uid, prop) = props[i];
            propData[i] = new AquariumPropData(GetNetEntity(uid), Name(uid), Prototype(uid)?.ID ?? string.Empty,
                prop.Beauty);
        }

        return new AquariumBoundUserInterfaceState(
            aquarium.Comp.Fluid,
            aquarium.Comp.Temperature,
            aquarium.Comp.MinTemperature,
            aquarium.Comp.MaxTemperature,
            aquarium.Comp.Mode,
            aquarium.Comp.Beauty,
            aquarium.Comp.GlassBroken,
            aquarium.Comp.Upgrades,
            aquarium.Comp.MaxFish,
            fishData,
            propData);
    }

    public void UpdateUi(Entity<AquariumComponent> aquarium)
    {
        if (!_ui.HasUi(aquarium.Owner, AquariumUiKey.Key))
            return;

        _ui.SetUiState(aquarium.Owner, AquariumUiKey.Key, BuildState(aquarium));
    }
}
