using Content.Goobstation.Shared.Achievements;
using Content.Shared._Mini.DailyQuests;
using Content.Shared.EntityConditions;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Goobstation.Server.Achievements;

[Prototype]
public sealed partial class AchievementPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField(required: true)]
    public ProtoId<AchievementCategoryPrototype> Category;

    [DataField(required: true)]
    public SpriteSpecifier Icon = default!;

    [DataField]
    public bool Hidden;

    [DataField(required: true)]
    public AchievementCondition Condition = default!;

    /// <summary>
    /// Reward tier. Uses the same rarities as weekly quests, so the reward scale matches.
    /// When omitted, the tier is derived from <see cref="AchievementCondition.Count"/>:
    /// one-off feats stay Rare (1 coin) and grindy ones scale up to Legendary.
    /// </summary>
    [DataField]
    public DailyQuestRarity? Rarity;

    public DailyQuestRarity GetRarity()
    {
        if (Rarity is { } explicitRarity)
            return explicitRarity;

        // Most achievements are one-off feats and stay Rare (one coin). Only long grinds scale up,
        // and the curated prototypes override the tier explicitly for especially hard feats.
        return Condition.Count switch
        {
            < 100 => DailyQuestRarity.Rare,
            < 500 => DailyQuestRarity.Epic,
            < 2000 => DailyQuestRarity.Mythic,
            _ => DailyQuestRarity.Legendary,
        };
    }

    /// <summary>Coins granted once when the achievement is unlocked.</summary>
    public int GetCoinReward() => GetRarity().GetDefaultReward();

    /// <summary>
    /// Идентификатор прототипа customGhost, выдаваемого вместо монет, когда достижение открыто.
    /// Когда задан, монетная награда не начисляется вовсе.
    /// </summary>
    [DataField]
    public string? GhostReward;
}

[ImplicitDataDefinitionForInheritors]
public abstract partial class AchievementCondition
{
    [DataField]
    public int Count = 1;

    [DataField]
    public EntityCondition[]? Requirements;
}
