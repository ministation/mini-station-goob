// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
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

    /// <summary>
    /// Dash speed in tiles per second.
    /// </summary>
    [DataField]
    public float DashSpeed = 20f;

    /// <summary>
    /// How close the dash has to get to the target to hit it.
    /// </summary>
    [DataField]
    public float HitRange = 0.75f;

    [ViewVariables]
    public TimeSpan NextRush;

    [ViewVariables]
    public EntityUid? DashTarget;
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
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LavalandRushComponent, MobStateComponent, NpcFactionMemberComponent>();
        while (query.MoveNext(out var uid, out var rush, out _, out _))
        {
            if (!_mobState.IsAlive(uid))
            {
                rush.DashTarget = null;
                continue;
            }

            if (rush.DashTarget is { } target)
            {
                UpdateDash(uid, rush, target, frameTime);
                continue;
            }

            if (_timing.CurTime < rush.NextRush || !TryFindTarget(uid, rush.Range, out target))
                continue;

            rush.NextRush = _timing.CurTime + TimeSpan.FromSeconds(rush.Cooldown);
            rush.DashTarget = target;
        }
    }

    private void UpdateDash(EntityUid uid, LavalandRushComponent rush, EntityUid target, float frameTime)
    {
        // The rush cannot cross obstructions, and the target can duck behind cover mid dash.
        if (!Exists(target)
            || !_mobState.IsAlive(target)
            || !_interaction.InRangeUnobstructed((uid, null), (target, null), range: -1f))
        {
            FinishRush(uid, rush);
            return;
        }

        var self = _transform.GetMapCoordinates(uid);
        var victim = _transform.GetMapCoordinates(target);
        var delta = victim.Position - self.Position;
        var distance = delta.Length();

        if (distance <= rush.HitRange)
        {
            _stun.TryKnockdown(target, TimeSpan.FromSeconds(rush.KnockdownSeconds), true);
            _damageable.TryChangeDamage(target, rush.RushDamage, origin: uid);
            FinishRush(uid, rush);
            return;
        }

        var step = MathF.Min(rush.DashSpeed * frameTime, distance - rush.HitRange);
        _transform.SetMapCoordinates(uid, new MapCoordinates(self.Position + delta / distance * step, self.MapId));
    }

    private void FinishRush(EntityUid uid, LavalandRushComponent rush)
    {
        rush.DashTarget = null;
        _status.TryAddStatusEffectDuration(uid, rush.TiredStatus, TimeSpan.FromSeconds(rush.TiredSeconds));
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
                || _npcFaction.IsEntityFriendly(uid, other.Owner)
                || !_interaction.InRangeUnobstructed((uid, null), (other.Owner, null), range: -1f))
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
