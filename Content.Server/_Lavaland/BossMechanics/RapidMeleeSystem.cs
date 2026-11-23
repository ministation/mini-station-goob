// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// TG parity for <see cref="RapidMeleeComponent"/>: the first hit of a melee swing starts a burst of
/// follow-up hits on the same target, each one allowed through even while the melee cooldown runs.
/// </summary>
public sealed class RapidMeleeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RapidMeleeComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<RapidMeleeComponent> ent, ref MeleeHitEvent args)
    {
        // The event is broadcast from the weapon, so it reaches every entity with this component.
        if (!args.IsHit ||
            args.User != ent.Owner ||
            args.HitEntities.Count == 0 ||
            ent.Comp.HitsLeft > 0 ||
            ent.Comp.Hits <= 1)
        {
            return;
        }

        ent.Comp.Weapon = args.Weapon;
        ent.Comp.Target = args.HitEntities[0];
        ent.Comp.HitsLeft = ent.Comp.Hits - 1;
        ent.Comp.NextHit = _timing.CurTime + ent.Comp.Delay;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<RapidMeleeComponent>();
        while (query.MoveNext(out var uid, out var rapid))
        {
            if (rapid.HitsLeft <= 0 || now < rapid.NextHit)
                continue;

            if (rapid.Target is not { } target ||
                rapid.Weapon is not { } weaponUid ||
                !TryComp<MeleeWeaponComponent>(weaponUid, out var weapon) ||
                TerminatingOrDeleted(target) ||
                _mobState.IsDead(uid) ||
                !_melee.InRange(uid, target, weapon.Range, null))
            {
                rapid.HitsLeft = 0;
                continue;
            }

            rapid.NextHit = now + rapid.Delay;

            // TG rapid_melee outranks the normal recovery window, otherwise the burst would be
            // eaten by the weapon cooldown after the first follow-up.
            if (weapon.NextAttack > now)
            {
                weapon.NextAttack = now;
                DirtyField(weaponUid, weapon, nameof(MeleeWeaponComponent.NextAttack));
            }

            _melee.AttemptLightAttack(uid, weaponUid, weapon, target);

            // Decrement after the attack: the hit event fired by our own swing must not start a new burst.
            rapid.HitsLeft--;
        }
    }
}
