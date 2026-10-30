// SPDX-FileCopyrightText: 2026 Goob Station Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Events;
using Content.Shared._Mini.Fishing.Prototypes;
using Content.Shared.Mobs.Components;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Server.Achievements.Conditions;

public sealed partial class FishRarityCondition : AchievementCondition
{
    [DataField]
    public int MaxRarity = 200;
}

public sealed partial class FishPrototypeCaughtCondition : AchievementCondition
{
    [DataField]
    public List<EntProtoId> Fish = new();
}

public sealed partial class FishSizeCondition : AchievementCondition
{
    [DataField]
    public float MinSize = 90;
}

public sealed partial class FishTraitCaughtCondition : AchievementCondition
{
    [DataField]
    public ProtoId<FishTraitPrototype> Trait;
}

public sealed partial class FishSourceCaughtCondition : AchievementCondition
{
    [DataField]
    public List<ProtoId<FishingSourcePrototype>> Sources = new();
}

public sealed partial class FishingTreasureChestCondition : AchievementCondition;

/// <summary>
/// Checks every fishing achievement condition. The event bus allows a single handler per
/// component and event, and ActorComponent plus FishCaughtEvent is already used by
/// FishCaughtConditionSystem, so all fishing conditions are dispatched from one system.
/// </summary>
public sealed partial class FishingAchievementSystem : EntitySystem
{
    [Dependency] private AchievementSystem _achievements = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateComponent, FishCaughtEvent>(OnFishCaught);
        SubscribeLocalEvent<MobStateComponent, FishingTreasureChestPullEvent>(OnTreasurePulled);
    }

    private void OnFishCaught(Entity<MobStateComponent> ent, ref FishCaughtEvent args)
    {
        int? rarity = null;
        string? fishId = null;
        string? entityId = args.FishId?.Id;
        var size = 0f;
        var traits = new HashSet<ProtoId<FishTraitPrototype>>();

        if (args.Fish is { } fishUid && !TerminatingOrDeleted(fishUid) && TryComp<FishComponent>(fishUid, out var fish))
        {
            size = fish.Size;
            fishId = fish.Fish?.Id;
            entityId = MetaData(fishUid).EntityPrototype?.ID ?? entityId;

            if (fish.Fish is { } fishProtoId && _proto.TryIndex(fishProtoId, out FishPrototype? fishPrototype))
            {
                rarity = fishPrototype.Rarity;
                foreach (var trait in fishPrototype.Traits)
                    traits.Add(trait);
            }

            // Gene engineering can add or remove traits on a single fish instance.
            foreach (var trait in fish.ExtraTraits)
                traits.Add(trait);

            foreach (var trait in fish.RemovedTraits)
                traits.Remove(trait);
        }
        else if (args.FishId is { } id && _proto.TryIndex(id, out EntityPrototype? fishEntity))
        {
            entityId = fishEntity.ID;

            if (fishEntity.TryGetComponent<FishComponent>(out var fishComp, EntityManager.ComponentFactory) &&
                fishComp.Fish is { } fishProtoId &&
                _proto.TryIndex(fishProtoId, out FishPrototype? fishPrototype))
            {
                rarity = fishPrototype.Rarity;
                foreach (var trait in fishPrototype.Traits)
                    traits.Add(trait);
            }
        }

        foreach (var achievement in _proto.EnumeratePrototypes<AchievementPrototype>())
        {
            switch (achievement.Condition)
            {
                case FishRarityCondition rarityCondition when rarity is { } value && value <= rarityCondition.MaxRarity:
                case FishPrototypeCaughtCondition caughtCondition when caughtCondition.Fish.Any(id => id.Id == fishId || id.Id == entityId):
                case FishSizeCondition sizeCondition when size >= sizeCondition.MinSize:
                case FishTraitCaughtCondition traitCondition when traits.Contains(traitCondition.Trait):
                case FishSourceCaughtCondition sourceCondition when args.Source is { } source && sourceCondition.Sources.Contains(source):
                    _achievements.AddProgress(ent.Owner, achievement);
                    break;
            }
        }
    }

    private void OnTreasurePulled(Entity<MobStateComponent> ent, ref FishingTreasureChestPullEvent args)
    {
        foreach (var achievement in _proto.EnumeratePrototypes<AchievementPrototype>())
        {
            if (achievement.Condition is FishingTreasureChestCondition)
                _achievements.AddProgress(ent.Owner, achievement);
        }
    }
}
