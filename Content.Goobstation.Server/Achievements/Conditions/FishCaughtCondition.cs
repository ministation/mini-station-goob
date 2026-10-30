// SPDX-FileCopyrightText: 2026 Goob Station Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Mini.Fishing.Events;
using Robust.Shared.Player;

namespace Content.Goobstation.Server.Achievements.Conditions;

public sealed partial class FishCaughtCondition : AchievementCondition;

public sealed class FishCaughtConditionSystem : AchievementConditionSystem<FishCaughtCondition>
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActorComponent, FishCaughtEvent>(OnFishCaught);
    }

    private void OnFishCaught(Entity<ActorComponent> ent, ref FishCaughtEvent args)
    {
        ProgressAll(ent);
    }
}
