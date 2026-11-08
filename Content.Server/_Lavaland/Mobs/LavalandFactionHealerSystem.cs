// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// Applies <see cref="LavalandFactionHealerComponent.Heal"/> to friendlies around the owner.
/// </summary>
public sealed class LavalandFactionHealerSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LavalandFactionHealerComponent, NpcFactionMemberComponent>();
        while (query.MoveNext(out var uid, out var healer, out _))
        {
            healer.Accumulator += frameTime;
            if (healer.Accumulator < healer.Interval)
                continue;

            healer.Accumulator -= healer.Interval;

            // A dead healer's body must not keep mending the pack.
            if (!_mobState.IsAlive(uid))
                continue;

            var coords = _transform.GetMapCoordinates(uid);
            foreach (var friend in _lookup.GetEntitiesInRange<NpcFactionMemberComponent>(coords, healer.Range))
            {
                if (friend.Owner == uid
                    || !_npcFaction.IsEntityFriendly(uid, friend.Owner)
                    || !_mobState.IsAlive(friend.Owner))
                {
                    continue;
                }

                _damageable.TryChangeDamage(friend.Owner, healer.Heal, true);
            }
        }
    }
}
