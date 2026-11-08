// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared._Lavaland.Mobs;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// Drops the staff of storms when the last living Legion dies, a port of TG's legion death().
/// A legion that still has splits left spawns the next tier instead of dropping loot, so only
/// the tier with no splits can end the fight.
/// </summary>
public sealed class LegionLootSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;

    private static readonly EntProtoId StaffOfStorms = "LavalandStaffOfStorms";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LegionBossLootComponent, MobStateChangedEvent>(OnLegionKilled);
    }

    private void OnLegionKilled(Entity<LegionBossLootComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        // Cache it: the corpse is queued for deletion by SpawnLootOnDeath in the same event.
        var coords = Transform(ent.Owner).Coordinates;

        // Split still has to spawn the next tier, so the fight is not over yet.
        if (TryComp<LegionSplitComponent>(ent.Owner, out var split) && split.SplitsRemaining > 0)
            return;

        // Any other living legion means this one was not the last. Dead ones are deleted with
        // their corpse, so the living marker set is empty exactly when the fight is over.
        var query = EntityQueryEnumerator<LegionBossLootComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (uid != ent.Owner && _mobState.IsAlive(uid))
                return;
        }

        Spawn(StaffOfStorms, coords);
    }
}
