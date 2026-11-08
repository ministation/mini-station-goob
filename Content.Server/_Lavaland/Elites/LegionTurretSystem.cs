// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared._Lavaland.Elites.Components;
using Content.Shared._Lavaland.Megafauna.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Elites;

/// <summary>
/// Bone turrets placed by the legionnaire fire bolts at nearby hostiles.
/// </summary>
public sealed class LegionTurretSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LegionTurretComponent, NpcFactionMemberComponent>();
        while (query.MoveNext(out var uid, out var turret, out var faction))
        {
            if (now < turret.NextShot)
                continue;

            var self = new Entity<NpcFactionMemberComponent?, FactionExceptionComponent?>(uid, faction, null);
            EntityUid? target = null;
            foreach (var hostile in _faction.GetNearbyHostiles(self, turret.Range))
            {
                if (!_mobState.IsAlive(hostile))
                    continue;

                target = hostile;
                break;
            }

            if (target is not { } targetUid)
                continue;

            turret.NextShot = now + TimeSpan.FromSeconds(turret.FireRate);
            // Action events are plain broadcasts, so the shot system only hears them when they are raised as such.
            RaiseLocalEvent(uid, new MegafaunaShotActionEvent
            {
                Performer = uid,
                Target = Transform(targetUid).Coordinates,
                Projectile = turret.Projectile,
                Count = 1,
                Speed = 20f,
                Pattern = MegafaunaShotPattern.Cone,
            }, broadcast: true);
        }
    }
}
