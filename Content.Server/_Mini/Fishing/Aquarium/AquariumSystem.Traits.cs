// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing;
using Content.Shared._Mini.Fishing.Aquarium;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Traits;
using Content.Shared.Electrocution;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Light;
using Robust.Shared.Random;

namespace Content.Server._Mini.Fishing.Aquarium;

/// <summary>
/// Fish trait behaviour that only matters while the fish lives in an aquarium.
/// </summary>
public sealed partial class AquariumSystem
{
    private const float EmulsijackDamage = 3f;
    private const float EmulsijackHeal = 3f;
    private const float NecrophageHunger = 0.75f;
    private const float TerritorialChancePerSecond = 0.01f;
    private const float NocturnalDamage = 0.5f;
    private const int AnxietyFishTolerance = 3;
    private const int TerritorialMinFish = 5;
    private const float BioelectricZapRange = 5f;
    private const int BioelectricZapDamage = 5;

    private static readonly TimeSpan BioelectricZapInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan BioelectricZapTime = TimeSpan.FromSeconds(2);

    [Dependency] private SharedFishTraitSystem _traits = default!;
    [Dependency] private SharedElectrocutionSystem _electrocution = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private LightLevelSystem _light = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    /// <summary>
    /// When the tank, upgraded with a bioelectric generator, may zap its surroundings again.
    /// </summary>
    private readonly Dictionary<EntityUid, TimeSpan> _bioelectricAt = new();

    /// <summary>
    /// Runs every trait effect that acts on the fish inside one tank, once per second.
    /// </summary>
    internal void TickTraitEffects(Entity<AquariumComponent> aquarium, List<Entity<FishComponent>> fishes,
        float seconds, TimeSpan now)
    {
        if (fishes.Count == 0)
            return;

        TryAnxiety(aquarium, fishes);
        TryEmulsijack(aquarium, fishes, seconds, now);
        TryNecrophage(aquarium, fishes, now);
        TryTerritorial(aquarium, fishes, seconds);
        ApplyNocturnal(aquarium, fishes, seconds);
        TryBioelectricGenerator(aquarium, fishes, now);
    }

    /// <summary>
    /// An anxious fish dies of stress as soon as it has to share the tank with more than three fish.
    /// </summary>
    private void TryAnxiety(Entity<AquariumComponent> aquarium, List<Entity<FishComponent>> fishes)
    {
        if (fishes.Count <= AnxietyFishTolerance)
            return;

        foreach (var fish in fishes)
        {
            if (!fish.Comp.Alive || !_traits.HasTrait(fish.Comp, FishTraitFlags.Anxiety))
                continue;

            _popup.PopupEntity(Loc.GetString("aquarium-fish-anxiety", ("fish", fish.Owner)), aquarium.Owner);
            KillFish(fish);
        }
    }

    /// <summary>
    /// An emulsifier poisons every other fish in the tank and feeds on them.
    /// </summary>
    private void TryEmulsijack(Entity<AquariumComponent> aquarium, List<Entity<FishComponent>> fishes,
        float seconds, TimeSpan now)
    {
        foreach (var fish in fishes)
        {
            if (!fish.Comp.Alive || !_traits.HasTrait(fish.Comp, FishTraitFlags.Emulsijack))
                continue;

            var emulsified = false;
            foreach (var victim in fishes)
            {
                if (victim.Owner == fish.Owner || !victim.Comp.Alive)
                    continue;

                if (_traits.HasTrait(victim.Comp, FishTraitFlags.ToxinImmunity))
                    continue;

                DamageFish(victim, EmulsijackDamage * seconds);
                emulsified = true;
            }

            if (!emulsified)
                continue;

            HealFish(fish, EmulsijackHeal * seconds);
            Satiate(fish, now);
        }
    }

    /// <summary>
    /// A hungry necrophage eats the carcass of any dead fish in the tank that is not yucky.
    /// </summary>
    private void TryNecrophage(Entity<AquariumComponent> aquarium, List<Entity<FishComponent>> fishes, TimeSpan now)
    {
        if (!_aquarium.TryGetFishContainer(aquarium, out var container))
            return;

        foreach (var fish in fishes)
        {
            if (!fish.Comp.Alive || !_traits.HasTrait(fish.Comp, FishTraitFlags.Necrophage))
                continue;

            var proto = _aquarium.GetProto(fish.Comp);
            if (_aquarium.GetHunger(fish.Comp, proto, now) < NecrophageHunger)
                continue;

            Entity<FishComponent>? prey = null;
            foreach (var candidate in fishes)
            {
                if (candidate.Owner == fish.Owner || candidate.Comp.Alive)
                    continue;

                if (_traits.HasTrait(candidate.Comp, FishTraitFlags.Yucky))
                    continue;

                prey = candidate;
                break;
            }

            if (prey is not { } victim)
                continue;

            _popup.PopupEntity(
                Loc.GetString("aquarium-fish-eaten", ("predator", fish.Owner), ("prey", victim.Owner)),
                aquarium.Owner);
            _container.Remove(victim.Owner, container, force: true);
            QueueDel(victim.Owner);
            _health.Remove(victim.Owner);
            Satiate(fish, now);
        }
    }

    /// <summary>
    /// A territorial fish in a crowded tank attacks another fish, hurting the smaller ones the most.
    /// </summary>
    private void TryTerritorial(Entity<AquariumComponent> aquarium, List<Entity<FishComponent>> fishes, float seconds)
    {
        if (fishes.Count < TerritorialMinFish)
            return;

        foreach (var fish in fishes)
        {
            if (!fish.Comp.Alive || !_traits.HasTrait(fish.Comp, FishTraitFlags.Territorial))
                continue;

            if (!_random.Prob(TerritorialChancePerSecond * seconds))
                continue;

            Entity<FishComponent>? victim = null;
            foreach (var candidate in fishes)
            {
                if (candidate.Owner == fish.Owner || !candidate.Comp.Alive)
                    continue;

                victim = candidate;
                break;
            }

            if (victim is not { } target)
                continue;

            // tg: round(rand(4, 20) * (source.size / victim.size)), smaller fish take extra damage.
            var ratio = fish.Comp.Size / MathF.Max(1f, target.Comp.Size);
            var damage = MathF.Max(1f, MathF.Round(_random.Next(4, 21) * ratio));
            DamageFish(target, damage);
            _popup.PopupEntity(
                Loc.GetString("aquarium-fish-attacked", ("aggressor", fish.Owner), ("victim", target.Owner)),
                aquarium.Owner);
        }
    }

    /// <summary>
    /// A nocturnal fish takes slow damage while the tank stands in bright light.
    /// </summary>
    private void ApplyNocturnal(Entity<AquariumComponent> aquarium, List<Entity<FishComponent>> fishes,
        float seconds)
    {
        if (!AnyTrait(fishes, FishTraitFlags.Nocturnal))
            return;

        if (_light.CalculateLightLevel(aquarium.Owner) <= SharedFishTraitSystem.BrightLightThreshold)
            return;

        foreach (var fish in fishes)
        {
            if (!fish.Comp.Alive || !_traits.HasTrait(fish.Comp, FishTraitFlags.Nocturnal))
                continue;

            DamageFish(fish, NocturnalDamage * seconds);
        }
    }

    /// <summary>
    /// A bioelectric generator turns the electric fields of electrogenesis fish into shocks that hit
    /// everything around the tank.
    /// </summary>
    private void TryBioelectricGenerator(Entity<AquariumComponent> aquarium, List<Entity<FishComponent>> fishes,
        TimeSpan now)
    {
        if (!aquarium.Comp.Upgrades.HasFlag(AquariumUpgradeFlags.BioelectricGenerator))
        {
            _bioelectricAt.Remove(aquarium.Owner);
            return;
        }

        if (!AnyTrait(fishes, FishTraitFlags.Electrogenesis))
            return;

        if (_bioelectricAt.TryGetValue(aquarium.Owner, out var at) && now < at)
            return;

        _bioelectricAt[aquarium.Owner] = now + BioelectricZapInterval;
        var coords = Transform(aquarium.Owner).Coordinates;

        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(coords, BioelectricZapRange))
        {
            if (!_mobState.IsAlive(mob))
                continue;

            _electrocution.TryDoElectrocution(mob.Owner, aquarium.Owner, BioelectricZapDamage,
                BioelectricZapTime, false);
        }
    }

    private bool AnyTrait(List<Entity<FishComponent>> fishes, FishTraitFlags flag)
    {
        foreach (var fish in fishes)
        {
            if (_traits.HasTrait(fish.Comp, flag))
                return true;
        }

        return false;
    }

    private void Satiate(Entity<FishComponent> fish, TimeSpan now)
    {
        fish.Comp.LastFed = now;
        Dirty(fish.Owner, fish.Comp);
    }

    /// <summary>
    /// Remaining integrity of a fish kept by a tank. Fish without an entry are at full health.
    /// </summary>
    internal float GetFishHealth(EntityUid fish)
    {
        return _health.TryGetValue(fish, out var health) ? health : MaxHealth;
    }

    private void KillFish(Entity<FishComponent> fish)
    {
        _health.Remove(fish.Owner);
        _fish.SetAlive(fish, false);
    }

    private void DamageFish(Entity<FishComponent> fish, float damage)
    {
        if (!fish.Comp.Alive || damage <= 0f)
            return;

        if (!_health.TryGetValue(fish.Owner, out var health))
            health = MaxHealth;

        health -= damage;
        if (health <= 0f)
        {
            KillFish(fish);
            return;
        }

        _health[fish.Owner] = health;
    }

    private void HealFish(Entity<FishComponent> fish, float heal)
    {
        if (!fish.Comp.Alive || heal <= 0f)
            return;

        if (!_health.TryGetValue(fish.Owner, out var health))
            health = MaxHealth;

        _health[fish.Owner] = MathF.Min(MaxHealth, health + heal);
    }
}
