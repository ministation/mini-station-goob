// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Events;
using Content.Shared._Mini.Fishing.Prototypes;
using Content.Server.Botany;
using Content.Server.Botany.Components;
using Content.Shared.Item;
using Content.Shared.VendingMachines;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Mini.Fishing.Sources;

/// <summary>
/// Loot of the dynamic fishing sources: a vending machine hands out its own products, a
/// hydroponics tray hands out random seeds. The source component itself is declared on the
/// base prototypes of those machines in YAML.
/// </summary>
public sealed partial class FishingDynamicSourceSystem : EntitySystem
{
    private const int HydroSeedCandidates = 8;

    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;

    private List<EntProtoId>? _seeds;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VendingMachineComponent, FishingDynamicRewardEvent>(OnVendingReward);
        SubscribeLocalEvent<PlantHolderComponent, FishingDynamicRewardEvent>(OnHydroTrayReward);

        _proto.PrototypesReloaded += OnPrototypesReloaded;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _proto.PrototypesReloaded -= OnPrototypesReloaded;
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<EntityPrototype>())
            _seeds = null;
    }

    private void OnVendingReward(Entity<VendingMachineComponent> ent, ref FishingDynamicRewardEvent args)
    {
        args.Handled = true;

        if (!_proto.TryIndex(ent.Comp.PackPrototypeId, out VendingMachineInventoryPrototype? pack))
            return;

        foreach (var product in pack.StartingInventory.Keys)
        {
            if (_proto.TryIndex(product, out EntityPrototype? proto)
                && !proto.Abstract
                && proto.TryGetComponent<ItemComponent>(out _, EntityManager.ComponentFactory))
            {
                args.Candidates.Add(product);
            }
        }
    }

    private void OnHydroTrayReward(Entity<PlantHolderComponent> ent, ref FishingDynamicRewardEvent args)
    {
        args.Handled = true;

        // Same rules as tg: no water or a plant already growing means nothing can be fished here.
        if (ent.Comp.Seed != null || ent.Comp.WaterLevel <= 0f)
            return;

        var seeds = GetSeeds();
        if (seeds.Count == 0)
            return;

        var pool = new List<EntProtoId>(seeds);
        _random.Shuffle(pool);

        var count = Math.Min(HydroSeedCandidates, pool.Count);
        for (var i = 0; i < count; i++)
        {
            args.Candidates.Add(pool[i]);
        }
    }

    private List<EntProtoId> GetSeeds()
    {
        if (_seeds != null)
            return _seeds;

        _seeds = new List<EntProtoId>();

        foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract || !proto.TryGetComponent<SeedComponent>(out var seed, EntityManager.ComponentFactory))
                continue;

            // Unharvestable mutations are not worth fishing for.
            if (seed.Seed != null && seed.Seed.Yield == -1)
                continue;

            if (seed.SeedId is { } seedId && _proto.TryIndex(seedId, out SeedPrototype? seedProto) && seedProto.Yield == -1)
                continue;

            _seeds.Add(proto.ID);
        }

        return _seeds;
    }
}