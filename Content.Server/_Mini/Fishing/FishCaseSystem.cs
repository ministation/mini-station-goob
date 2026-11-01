// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Prototypes;
using Content.Shared._Mini.Fishing.Systems;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Mini.Fishing;

/// <summary>
/// Puts a random fish into a fish case when it spawns, the way tg fish cases roll their contents.
/// </summary>
public sealed class FishCaseSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly FishSystem _fish = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishCaseComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<FishCaseComponent> ent, ref MapInitEvent args)
    {
        // The one to one component test adds this component to a bare entity without storage.
        if (!_net.IsServer || !HasComp<StorageComponent>(ent.Owner) || Pick(ent.Comp) is not { } fishId)
            return;

        var fish = Spawn(fishId, Transform(ent).Coordinates);

        if (ent.Comp.Dead && TryComp<FishComponent>(fish, out var fishComp))
            _fish.SetAlive((fish, fishComp), false);

        _storage.Insert(ent.Owner, fish, out _, playSound: false);
    }

    private ProtoId<FishPrototype>? Pick(FishCaseComponent caseComp)
    {
        if (caseComp.Fish.Count > 0)
            return _random.Pick(caseComp.Fish);

        var candidates = new List<(ProtoId<FishPrototype> Id, float Weight)>();
        var total = 0f;

        foreach (var fish in _proto.EnumeratePrototypes<FishPrototype>())
        {
            if (fish.Rarity <= 0 || !_proto.HasIndex<EntityPrototype>(fish.ID))
                continue;

            if (!FitsFluid(fish.Fluid, caseComp.Fluid))
                continue;

            candidates.Add((fish.ID, fish.Rarity));
            total += fish.Rarity;
        }

        if (candidates.Count == 0)
            return null;

        var roll = _random.NextFloat() * total;
        foreach (var (id, weight) in candidates)
        {
            roll -= weight;
            if (roll <= 0f)
                return id;
        }

        return candidates[^1].Id;
    }

    private static bool FitsFluid(AquariumFluid fishFluid, AquariumFluid? caseFluid)
    {
        if (caseFluid is not { } wanted || wanted == AquariumFluid.AnyWater)
            return true;

        if (fishFluid == wanted || fishFluid == AquariumFluid.AnyWater)
            return true;

        // Anadromous fish tolerate both kinds of water, tg lets them into both cases.
        return fishFluid == AquariumFluid.Anadromous && wanted is AquariumFluid.Freshwater or AquariumFluid.Saltwater;
    }
}
