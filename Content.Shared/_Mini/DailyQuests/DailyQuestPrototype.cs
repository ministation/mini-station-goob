// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Mini.DailyQuests;

[Prototype]
public sealed partial class DailyQuestPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public DailyQuestType QuestType;

    [DataField]
    public ProtoId<JobIconPrototype>? Icon;

    /// <summary>
    /// Optional sprite override when a job icon prototype is not suitable.
    /// </summary>
    [DataField]
    public SpriteSpecifier? Sprite;

    [DataField]
    public LocId Name = string.Empty;

    [DataField]
    public LocId Description = string.Empty;

    [DataField]
    public ProtoId<JobPrototype>? RequiredJob;

    [DataField]
    public int TargetCount = 1;

    [DataField]
    public int RewardCoins = 0;

    [DataField]
    public DailyQuestRarity Rarity = DailyQuestRarity.Rare;

    /// <summary>
    /// Minimum active playtime required for round-based passive quests.
    /// </summary>
    [DataField]
    public TimeSpan MinRoundPlaytime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Spawn weight override. Negative values use <see cref="Rarity"/> drop weight.
    /// </summary>
    [DataField]
    public float Weight = -1f;

    public float GetDropWeight() => Weight >= 0f ? Weight : Rarity.GetDropWeight();

    public int GetRewardCoins() => RewardCoins > 0 ? RewardCoins : Rarity.GetDefaultReward();
}
