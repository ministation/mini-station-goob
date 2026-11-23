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
/// TG lobster rush: the mob telegraphs the charge, dashes in a straight line at the spot the
/// victim stood on, knocks it down and is tired afterwards. The dash is not homing, so the victim
/// can step aside or hide behind cover while the mob winds up.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandRushComponent : Component
{
    /// <summary>
    /// TG charge_distance: how far away the mob looks for a victim, and how far the dash can go.
    /// </summary>
    [DataField]
    public float Range = 8f;

    /// <summary>
    /// TG charge_delay: the mob stands still and telegraphs the rush for this long.
    /// </summary>
    [DataField]
    public float WindupSeconds = 1.5f;

    /// <summary>
    /// TG charge_speed is 0.5 seconds per tile, which is 2 tiles per second.
    /// </summary>
    [DataField]
    public float ChargeSpeed = 2f;

    /// <summary>
    /// Seconds between rushes, counted from the start of the windup. TG cooldown_time is 6.
    /// </summary>
    [DataField]
    public float Cooldown = 6f;

    /// <summary>
    /// TG knockdown_duration.
    /// </summary>
    [DataField]
    public float KnockdownSeconds = 2.5f;

    /// <summary>
    /// TG charge_damage, 30 brute.
    /// </summary>
    [DataField]
    public DamageSpecifier RushDamage = new()
    {
        DamageDict = { { "Slash", 30f } },
    };

    /// <summary>
    /// Status effect applied to the rusher after the dash, TG tired_post_charge.
    /// </summary>
    [DataField]
    public EntProtoId TiredStatus = "LavalandStatusEffectTired";

    /// <summary>
    /// TG tired_post_charge lasts one second.
    /// </summary>
    [DataField]
    public float TiredSeconds = 1f;

    /// <summary>
    /// How close the dash has to get to a living victim to hit it.
    /// </summary>
    [DataField]
    public float HitRange = 0.75f;

    [ViewVariables]
    public TimeSpan NextRush;

    [ViewVariables]
    public TimeSpan DashStartsAt;

    /// <summary>
    /// Where the mob stood when the rush started, it is held there until the windup ends.
    /// </summary>
    [ViewVariables]
    public Vector2 Origin;

    /// <summary>
    /// The spot the victim stood on when the rush started. The dash is committed to it.
    /// </summary>
    [ViewVariables]
    public Vector2 Destination;

    [ViewVariables]
    public EntityUid? DashTarget;

    [ViewVariables]
    public bool DashHit;
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

            if (rush.DashTarget != null)
            {
                if (_timing.CurTime < rush.DashStartsAt)
                    _transform.SetWorldPosition(uid, rush.Origin); // TG does not move while it telegraphs the charge.
                else
                    UpdateDash(uid, rush, frameTime);

                continue;
            }

            if (_timing.CurTime < rush.NextRush || !TryFindTarget(uid, rush.Range, out var target))
                continue;

            // TG starts the cooldown when the charge is activated and only then waits out charge_delay.
            rush.NextRush = _timing.CurTime + TimeSpan.FromSeconds(rush.Cooldown);
            rush.DashStartsAt = _timing.CurTime + TimeSpan.FromSeconds(rush.WindupSeconds);
            rush.Origin = _transform.GetWorldPosition(uid);
            rush.Destination = _transform.GetWorldPosition(target);
            rush.DashTarget = target;
            rush.DashHit = false;
        }
    }

    private void UpdateDash(EntityUid uid, LavalandRushComponent rush, float frameTime)
    {
        var self = _transform.GetWorldPosition(uid);

        // TG hurts whatever the charge runs into, so a victim that stepped aside is not hit.
        if (!rush.DashHit
            && rush.DashTarget is { } target
            && Exists(target)
            && _mobState.IsAlive(target)
            && (self - _transform.GetWorldPosition(target)).Length() <= rush.HitRange)
        {
            rush.DashHit = true;
            _stun.TryKnockdown(target, TimeSpan.FromSeconds(rush.KnockdownSeconds), true);
            _damageable.TryChangeDamage(target, rush.RushDamage, origin: uid);
        }

        var delta = rush.Destination - self;
        var remaining = delta.Length();
        var destination = new MapCoordinates(rush.Destination, Transform(uid).MapID);

        // Cover stops the rush instead of being dashed through.
        if (remaining <= 0.01f || !_interaction.InRangeUnobstructed(uid, destination, range: -1f))
        {
            FinishRush(uid, rush);
            return;
        }

        var step = MathF.Min(rush.ChargeSpeed * frameTime, remaining);
        _transform.SetWorldPosition(uid, self + delta / remaining * step);
    }

    private void FinishRush(EntityUid uid, LavalandRushComponent rush)
    {
        rush.DashTarget = null;
        rush.DashHit = false;
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
