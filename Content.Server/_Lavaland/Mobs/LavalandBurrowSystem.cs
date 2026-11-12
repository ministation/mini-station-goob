// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
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
/// TG goldgrub burrow escape: when hurt, it digs itself into the ground and comes
/// back up some distance away, hidden while it is under. The dig is telegraphed
/// with the same message and stone-breaking sound as TG.
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

    /// <summary>
    /// Seconds it spends digging down before it vanishes underground.
    /// </summary>
    [DataField]
    public float DigTime = 2.5f;

    /// <summary>
    /// Seconds it stays underground before coming back up.
    /// </summary>
    [DataField]
    public float UndergroundTime = 0.75f;

    /// <summary>
    /// Played when it goes under and when it comes back up.
    /// </summary>
    [DataField]
    public SoundSpecifier DigSound = new SoundPathSpecifier("/Audio/Effects/break_stone.ogg");

    [ViewVariables]
    public TimeSpan NextEscape;

    /// <summary>
    /// When the current dig started, <see cref="TimeSpan.Zero"/> when it is not digging.
    /// </summary>
    [ViewVariables]
    public TimeSpan DigStart;

    [ViewVariables]
    public TimeSpan EmergeAt;

    [ViewVariables]
    public bool Underground;

    /// <summary>
    /// True when the system added <see cref="StealthComponent"/> and has to remove it again.
    /// </summary>
    [ViewVariables]
    public bool Hid;
}

public sealed class LavalandBurrowSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandBurrowEscapeComponent, DamageChangedEvent>(OnEscapeDamage);
    }

    private void OnEscapeDamage(Entity<LavalandBurrowEscapeComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased
            || args.Origin == null
            || _timing.CurTime < ent.Comp.NextEscape
            || ent.Comp.DigStart != TimeSpan.Zero)
        {
            return;
        }

        ent.Comp.NextEscape = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.Cooldown);
        ent.Comp.DigStart = _timing.CurTime;

        _popup.PopupEntity(Loc.GetString("lavaland-goldgrub-burrow-start", ("grub", ent.Owner)),
            ent.Owner, PopupType.Medium);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<LavalandBurrowComponent, MobStateComponent, NpcFactionMemberComponent>();
        while (query.MoveNext(out var uid, out var burrow, out _, out _))
        {
            if (!_mobState.IsAlive(uid))
                continue;

            if (now >= burrow.NextDevour && TryDevour(uid, burrow))
                continue;

            if (now < burrow.NextResurface || !TryFindTarget(uid, burrow.SearchRange, out var target))
                continue;

            burrow.NextResurface = now + TimeSpan.FromSeconds(burrow.ResurfaceCooldown);

            var victim = _transform.GetMapCoordinates(target);
            // Shortcut: no passability check, the mob is rooted anyway and only needs to
            // reappear in the open near the target. Upgrade to a tile check if it ever matters.
            var offset = _random.NextAngle().ToVec() * _random.NextFloat(1f, burrow.ResurfaceRange);
            _transform.SetMapCoordinates(uid, new MapCoordinates(victim.Position + offset, victim.MapId));
        }

        UpdateEscapes(now);
    }

    private void UpdateEscapes(TimeSpan now)
    {
        var query = EntityQueryEnumerator<LavalandBurrowEscapeComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var escape, out _))
        {
            if (escape.DigStart == TimeSpan.Zero)
                continue;

            if (!_mobState.IsAlive(uid))
            {
                StopEscape(uid, escape);
                continue;
            }

            if (!escape.Underground)
            {
                if (now < escape.DigStart + TimeSpan.FromSeconds(escape.DigTime))
                    continue;

                escape.Underground = true;
                escape.EmergeAt = now + TimeSpan.FromSeconds(escape.UndergroundTime);

                // Out of sight while under the ground, like TG's phased mob. Do not let
                // a stray hit pop it back into view before it is supposed to resurface.
                if (!HasComp<StealthComponent>(uid))
                {
                    var stealth = EnsureComp<StealthComponent>(uid);
                    _stealth.SetRevealOnDamage((uid, stealth), false);
                    _stealth.SetRevealOnAttack((uid, stealth), false);
                    escape.Hid = true;
                }

                _stealth.SetVisibility(uid, -1.5f);

                var coords = _transform.GetMapCoordinates(uid);
                var offset = _random.NextAngle().ToVec() * _random.NextFloat(1f, escape.Distance);
                _transform.SetMapCoordinates(uid, new MapCoordinates(coords.Position + offset, coords.MapId));

                _audio.PlayPvs(escape.DigSound, uid);
                continue;
            }

            if (now < escape.EmergeAt)
                continue;

            _audio.PlayPvs(escape.DigSound, uid);
            _popup.PopupEntity(Loc.GetString("lavaland-goldgrub-burrow-end", ("grub", uid)),
                uid, PopupType.Medium);

            StopEscape(uid, escape);
        }
    }

    private void StopEscape(EntityUid uid, LavalandBurrowEscapeComponent escape)
    {
        if (escape.Hid && HasComp<StealthComponent>(uid))
            RemComp<StealthComponent>(uid);

        escape.DigStart = TimeSpan.Zero;
        escape.Underground = false;
        escape.Hid = false;
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
