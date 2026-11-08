// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Stunnable;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// TG lobster rush: the mob dashes at a nearby target, knocks it down, hits it and
/// then gets tired for a while.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandRushComponent : Component
{
    /// <summary>
    /// How far the mob looks for a target to rush.
    /// </summary>
    [DataField]
    public float Range = 8f;

    /// <summary>
    /// Seconds between rushes.
    /// </summary>
    [DataField]
    public float Cooldown = 8f;

    /// <summary>
    /// Seconds the victim stays knocked down.
    /// </summary>
    [DataField]
    public float KnockdownSeconds = 2.5f;

    /// <summary>
    /// Damage dealt by the rush itself, before the regular melee attack.
    /// </summary>
    [DataField]
    public DamageSpecifier RushDamage = new()
    {
        DamageDict = { { "Slash", 17f } },
    };

    /// <summary>
    /// Status effect applied to the rusher after the dash.
    /// </summary>
    [DataField]
    public EntProtoId TiredStatus = "LavalandStatusEffectTired";

    /// <summary>
    /// Seconds the tired status effect lasts.
    /// </summary>
    [DataField]
    public float TiredSeconds = 4f;

    [ViewVariables]
    public TimeSpan NextRush;
}

public sealed class LavalandRushSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LavalandRushComponent, MobStateComponent, NpcFactionMemberComponent>();
        while (query.MoveNext(out var uid, out var rush, out _, out _))
        {
            if (!_mobState.IsAlive(uid) || _timing.CurTime < rush.NextRush)
                continue;

            if (!TryFindTarget(uid, rush.Range, out var target))
                continue;

            rush.NextRush = _timing.CurTime + TimeSpan.FromSeconds(rush.Cooldown);

            var self = _transform.GetMapCoordinates(uid);
            var victim = _transform.GetMapCoordinates(target);
            var delta = self.Position - victim.Position;
            delta = delta.LengthSquared() > 0.001f ? Vector2.Normalize(delta) : new Vector2(0f, 1f);

            // Jump right next to the victim, that is the dash.
            _transform.SetMapCoordinates(uid, new MapCoordinates(victim.Position + delta * 0.5f, victim.MapId));

            _stun.TryKnockdown(target, TimeSpan.FromSeconds(rush.KnockdownSeconds), true);
            _damageable.TryChangeDamage(target, rush.RushDamage, origin: uid);
            _status.TryAddStatusEffectDuration(uid, rush.TiredStatus, TimeSpan.FromSeconds(rush.TiredSeconds));
        }
    }

    private bool TryFindTarget(EntityUid uid, float range, out EntityUid target)
    {
        target = EntityUid.Invalid;

        var closest = float.MaxValue;
        var coords = _transform.GetMapCoordinates(uid);
        foreach (var other in _lookup.GetEntitiesInRange<MobStateComponent>(coords, range))
        {
            if (other.Owner == uid
                || !_mobState.IsAlive(other.Owner)
                || _npcFaction.IsEntityFriendly(uid, other.Owner))
            {
                continue;
            }

            var distance = (_transform.GetMapCoordinates(other.Owner).Position - coords.Position).LengthSquared();
            if (distance >= closest)
                continue;

            closest = distance;
            target = other.Owner;
        }

        return target.IsValid();
    }
}
