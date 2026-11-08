// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Lavaland.Mobs;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Server.NPC.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Throwing;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// Mook leap: a provoked mook winds up, throws itself at its attacker and
/// lands a rapid melee combo. TG parity: mook_leap ability (cooldown 7s,
/// wind-up 2s, throw_at range 7, combo of 4 melee hits at 0.4s intervals).
/// Mooks never start fights: only remembered attackers are leapt at.
/// </summary>
public sealed class MookLeapSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MookLeapComponent, ThrowDoHitEvent>(OnThrowHit);
        SubscribeLocalEvent<MookLeapComponent, LandEvent>(OnLand);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<MookLeapComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var leap, out var xform))
        {
            // Finish the wind-up and leap at the marked victim.
            if (leap.WindupUntil != TimeSpan.Zero)
            {
                if (now < leap.WindupUntil)
                    continue;

                leap.WindupUntil = TimeSpan.Zero;
                leap.Leaping = true;

                if (leap.LeapTarget != null && Exists(leap.LeapTarget.Value))
                {
                    _throwing.TryThrow(uid, Transform(leap.LeapTarget.Value).Coordinates, leap.ThrowSpeed);
                }
                else
                {
                    // The victim vanished during the wind-up: stand back up.
                    leap.Leaping = false;
                    SetLeapVisual(uid, false);
                }

                continue;
            }

            if (leap.Leaping || now < leap.NextLeap)
                continue;
            if (TerminatingOrDeleted(uid))
                continue;

            var coords = _transform.GetMapCoordinates(uid, xform);

            // TG: mooks retaliate — leap only at remembered attackers in range.
            EntityUid? target = null;
            var bestDistance = leap.LeapRange;
            if (TryComp<NPCRetaliationComponent>(uid, out var retaliation))
            {
                foreach (var (attacker, _) in retaliation.AttackMemories)
                {
                    if (attacker == uid || Deleted(attacker) || !_mobState.IsAlive(attacker))
                        continue;

                    var attackerCoords = _transform.GetMapCoordinates(attacker);
                    if (attackerCoords.MapId != coords.MapId)
                        continue;

                    var distance = (attackerCoords.Position - coords.Position).Length();
                    if (distance >= bestDistance)
                        continue;

                    target = attacker;
                    bestDistance = distance;
                }
            }

            if (target == null)
                continue;

            // TG: the mook visibly winds up before pouncing.
            leap.LeapTarget = target;
            leap.WindupUntil = now + leap.Windup;
            SetLeapVisual(uid, true);
        }
    }

    private void OnThrowHit(Entity<MookLeapComponent> ent, ref ThrowDoHitEvent args)
    {
        if (!ent.Comp.Leaping)
            return;

        var melee = CompOrNull<MeleeWeaponComponent>(ent);
        var damage = melee?.Damage ??
            new DamageSpecifier(_prototypes.Index<DamageTypePrototype>("Slash"), 15f);

        // TG: a successful leap is followed by a rapid melee combo.
        for (var i = 1; i <= ent.Comp.ComboHits; i++)
        {
            var victim = args.Target;
            Timer.Spawn(TimeSpan.FromSeconds(ent.Comp.ComboInterval * i), () =>
            {
                if (Deleted(victim) || !_mobState.IsAlive(victim))
                    return;

                _damageable.TryChangeDamage(victim, damage, origin: ent);
            });
        }

        EndLeap(ent);
    }

    private void OnLand(Entity<MookLeapComponent> ent, ref LandEvent args)
    {
        if (ent.Comp.Leaping)
            EndLeap(ent);
    }

    private void EndLeap(Entity<MookLeapComponent> ent)
    {
        ent.Comp.Leaping = false;
        ent.Comp.LeapTarget = null;
        ent.Comp.NextLeap = _timing.CurTime + ent.Comp.Cooldown;
        SetLeapVisual(ent, false);
    }

    private void SetLeapVisual(EntityUid uid, bool leaping)
    {
        _appearance.SetData(uid, MookVisuals.Leaping, leaping);
    }
}
