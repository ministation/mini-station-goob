// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// TG bileworm: it is rooted in place, burrows and resurfaces near its target,
/// and devours an unconscious victim to heal itself.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandBurrowComponent : Component
{
    /// <summary>
    /// How far the mob looks for a target to burrow towards.
    /// </summary>
    [DataField]
    public float SearchRange = 20f;

    /// <summary>
    /// Maximum distance from the target it resurfaces at.
    /// </summary>
    [DataField]
    public float ResurfaceRange = 5f;

    /// <summary>
    /// Seconds between burrows.
    /// </summary>
    [DataField]
    public float ResurfaceCooldown = 12f;

    /// <summary>
    /// How close an unconscious victim has to be to be devoured.
    /// </summary>
    [DataField]
    public float DevourRange = 2f;

    /// <summary>
    /// Seconds between devours.
    /// </summary>
    [DataField]
    public float DevourCooldown = 10f;

    [ViewVariables]
    public TimeSpan NextResurface;

    [ViewVariables]
    public TimeSpan NextDevour;
}

/// <summary>
/// TG goldgrub burrow escape: when hurt, it digs away from the attacker.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandBurrowEscapeComponent : Component
{
    /// <summary>
    /// How far it digs away.
    /// </summary>
    [DataField]
    public float Distance = 6f;

    /// <summary>
    /// Seconds between escapes.
    /// </summary>
    [DataField]
    public float Cooldown = 8f;

    [ViewVariables]
    public TimeSpan NextEscape;
}

public sealed class LavalandBurrowSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandBurrowEscapeComponent, DamageChangedEvent>(OnEscapeDamage);
    }

    private void OnEscapeDamage(Entity<LavalandBurrowEscapeComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin == null || _timing.CurTime < ent.Comp.NextEscape)
            return;

        ent.Comp.NextEscape = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.Cooldown);

        var coords = _transform.GetMapCoordinates(ent);
        var offset = _random.NextAngle().ToVec() * _random.NextFloat(1f, ent.Comp.Distance);
        _transform.SetMapCoordinates(ent, new MapCoordinates(coords.Position + offset, coords.MapId));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LavalandBurrowComponent, MobStateComponent, NpcFactionMemberComponent>();
        while (query.MoveNext(out var uid, out var burrow, out _, out _))
        {
            if (!_mobState.IsAlive(uid))
                continue;

            if (_timing.CurTime >= burrow.NextDevour && TryDevour(uid, burrow))
                continue;

            if (_timing.CurTime < burrow.NextResurface || !TryFindTarget(uid, burrow.SearchRange, out var target))
                continue;

            burrow.NextResurface = _timing.CurTime + TimeSpan.FromSeconds(burrow.ResurfaceCooldown);

            var victim = _transform.GetMapCoordinates(target);
            // Shortcut: no passability check, the mob is rooted anyway and only needs to
            // reappear in the open near the target. Upgrade to a tile check if it ever matters.
            var offset = _random.NextAngle().ToVec() * _random.NextFloat(1f, burrow.ResurfaceRange);
            _transform.SetMapCoordinates(uid, new MapCoordinates(victim.Position + offset, victim.MapId));
        }
    }

    private bool TryDevour(EntityUid uid, LavalandBurrowComponent burrow)
    {
        var coords = _transform.GetMapCoordinates(uid);
        foreach (var other in _lookup.GetEntitiesInRange<MobStateComponent>(coords, burrow.DevourRange))
        {
            if (other.Owner == uid
                || !_mobState.IsCritical(other.Owner)
                || _npcFaction.IsEntityFriendly(uid, other.Owner))
            {
                continue;
            }

            var victim = _transform.GetMapCoordinates(other.Owner);
            _transform.SetMapCoordinates(uid, new MapCoordinates(victim.Position, victim.MapId));

            _damageable.SetDamage(uid, new DamageSpecifier());

            QueueDel(other.Owner);

            burrow.NextDevour = _timing.CurTime + TimeSpan.FromSeconds(burrow.DevourCooldown);
            burrow.NextResurface = _timing.CurTime + TimeSpan.FromSeconds(burrow.ResurfaceCooldown);
            return true;
        }

        return false;
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
