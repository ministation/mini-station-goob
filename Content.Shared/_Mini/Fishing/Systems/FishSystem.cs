// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Events;
using Content.Shared._Mini.Fishing.Prototypes;
using Content.Shared.Nutrition.Components;
using Content.Shared.Item;
using Content.Shared.Storage;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared._Mini.Fishing.Systems;

/// <summary>
/// Handles the life cycle of caught fish: rolling size and weight, dry land suffocation and fillet amounts.
/// </summary>
public sealed partial class FishSystem : EntitySystem
{
    private const float SuffocationTime = 100f;

    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private FishingSourceSystem _sources = default!;
    [Dependency] private INetManager _net = default!;

    private float _updateTimer;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<FishComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Initialized)
            return;

        ent.Comp.Initialized = true;
        var proto = GetProto(ent.Comp);
        if (proto == null)
        {
            // Legacy fish still raise this so edible and other systems see them.
            RaiseLocalEvent(ent.Owner, new FishInitializedEvent(ent.Owner), broadcast: true);
            return;
        }

        Randomize(ent, proto);
        UpdateHeldPrefix(ent, proto);
        UpdateButcherable(ent, proto);
        ent.Comp.FishDifficulty = proto.Rarity <= 400 ? 0.035f : FishComponent.DefaultDifficulty;
        Dirty(ent, ent.Comp);

        RaiseLocalEvent(ent.Owner, new FishInitializedEvent(ent.Owner), broadcast: true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        _updateTimer += frameTime;
        if (_updateTimer < 1f)
            return;

        _updateTimer -= 1f;
        UpdateSuffocation();
    }

    private void UpdateSuffocation()
    {
        var query = EntityQueryEnumerator<FishComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var fish, out var xform))
        {
            if (!fish.Alive)
                continue;

            // Anything in a container or in someone's hands is not drying out on the floor.
            if (!HasComp<MapComponent>(xform.ParentUid) && !HasComp<MapGridComponent>(xform.ParentUid))
            {
                fish.TimeOutOfWater = 0;
                continue;
            }

            if (_sources.TryGetSource(xform.Coordinates, null, out _))
            {
                fish.TimeOutOfWater = 0;
                continue;
            }

            fish.TimeOutOfWater += 1f;
            if (fish.TimeOutOfWater < SuffocationTime)
                continue;

            SetAlive((uid, fish), false);
        }
    }

    public FishPrototype? GetProto(FishComponent comp)
    {
        if (comp.Fish is not { } id)
            return null;

        return _proto.TryIndex(id, out FishPrototype? proto) ? proto : null;
    }

    public SpriteSpecifier? GetIcon(FishComponent comp)
    {
        return GetProto(comp)?.Sprite;
    }

    /// <summary>
    /// All traits of this fish instance: species traits minus removed ones plus injected ones.
    /// </summary>
    public IEnumerable<ProtoId<FishTraitPrototype>> GetTraits(FishComponent comp)
    {
        var proto = GetProto(comp);

        if (proto != null)
        {
            foreach (var trait in proto.Traits)
            {
                if (!comp.RemovedTraits.Contains(trait))
                    yield return trait;
            }
        }

        foreach (var trait in comp.ExtraTraits)
        {
            if (!comp.RemovedTraits.Contains(trait))
                yield return trait;
        }
    }

    public bool HasTrait(FishComponent comp, ProtoId<FishTraitPrototype> trait)
    {
        foreach (var current in GetTraits(comp))
        {
            if (current == trait)
                return true;
        }

        return false;
    }

    public void AddTrait(Entity<FishComponent> ent, ProtoId<FishTraitPrototype> trait)
    {
        ent.Comp.RemovedTraits.Remove(trait);

        if (!ent.Comp.ExtraTraits.Contains(trait))
            ent.Comp.ExtraTraits.Add(trait);

        Dirty(ent, ent.Comp);
    }

    public void RemoveTrait(Entity<FishComponent> ent, ProtoId<FishTraitPrototype> trait)
    {
        ent.Comp.ExtraTraits.Remove(trait);

        var proto = GetProto(ent.Comp);
        if (proto != null && proto.Traits.Contains(trait) && !ent.Comp.RemovedTraits.Contains(trait))
            ent.Comp.RemovedTraits.Add(trait);

        Dirty(ent, ent.Comp);
    }

    public void Randomize(Entity<FishComponent> ent, FishPrototype proto)
    {
        ent.Comp.Size = MathF.Max(1f, proto.AverageSize * (1f + _random.NextFloat(-proto.SizeDeviation, proto.SizeDeviation)));
        ent.Comp.Weight = MathF.Max(1f, proto.AverageWeight * (1f + _random.NextFloat(-proto.WeightDeviation, proto.WeightDeviation)));
        Dirty(ent, ent.Comp);
    }

    private void UpdateHeldPrefix(Entity<FishComponent> ent, FishPrototype proto)
    {
        if (!TryComp<ItemComponent>(ent, out var item))
            return;

        var prefix = proto.InhandPrefix;
        if (prefix == null)
        {
            prefix = ent.Comp.Size switch
            {
                < 50f => "fish_small",
                < 80f => "fish_normal",
                < 120f => "fish_bulky",
                _ => "fish_huge",
            };
        }

        _item.SetHeldPrefix(ent, prefix, false, item);
    }

    private void UpdateButcherable(Entity<FishComponent> ent, FishPrototype proto)
    {
        if (proto.Fillet is not { } fillet || !TryComp<ButcherableComponent>(ent, out var butcherable))
            return;

        var amount = Math.Max(1, (int) MathF.Round(proto.Fillets * ent.Comp.Size / 50f));
        butcherable.SpawnedEntities = new List<EntitySpawnEntry>
        {
            new() { PrototypeId = fillet, Amount = amount, MaxAmount = amount },
        };
        Dirty(ent, butcherable);
    }

    public void SetAlive(Entity<FishComponent> ent, bool alive)
    {
        if (ent.Comp.Alive == alive)
            return;

        ent.Comp.Alive = alive;
        ent.Comp.TimeOutOfWater = 0;
        Dirty(ent, ent.Comp);
        RaiseLocalEvent(ent, new FishStatusChangedEvent(ent, alive));
    }
}