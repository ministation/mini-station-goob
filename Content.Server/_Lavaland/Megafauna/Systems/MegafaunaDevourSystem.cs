// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Body.Systems;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared._Shitmed.Body.Organ;

namespace Content.Server._Lavaland.Megafauna.Systems;

/// <summary>
///     Megafauna feasts on adjacent fresh corpses (TG _megafauna.dm devour): heals for
///     a fraction of the victim's max health, rips out heart, lungs and liver, mangles
///     the body and marks it gutted. Live aggressors always take priority: the chase
///     system handles them, this system only eats what is lying nearby.
/// </summary>
public sealed class MegafaunaDevourSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private EntityQuery<MegafaunaGuttedComponent> _guttedQuery;

    public override void Initialize()
    {
        _guttedQuery = GetEntityQuery<MegafaunaGuttedComponent>();
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<MegafaunaDevourComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var devour, out var xform))
        {
            if (_mobState.IsDead(uid))
                continue;

            foreach (var entity in _lookup.GetEntitiesInRange<MobStateComponent>(xform.Coordinates, devour.Range))
            {
                if (entity.Owner == uid
                    || !_mobState.IsDead(entity.Owner)
                    || !HasComp<MobThresholdsComponent>(entity.Owner)
                    || _guttedQuery.HasComp(entity.Owner))
                {
                    continue;
                }

                Devour((uid, devour), entity.Owner);
                break; // One corpse per tick per boss.
            }
        }
    }

    private void Devour(Entity<MegafaunaDevourComponent> ent, EntityUid victim)
    {
        EnsureComp<MegafaunaGuttedComponent>(victim);

        // TG heals only off-station; our megafauna never leaves Lavaland, so always heal.
        if (TryComp<MobThresholdsComponent>(victim, out var thresholds) &&
            _thresholds.TryGetThresholdForState(victim, MobState.Critical, out var max, thresholds))
        {
            var heal = new DamageSpecifier();
            heal.DamageDict["Blunt"] = -max.Value * ent.Comp.HealFraction;
            _damageable.TryChangeDamage(ent.Owner, heal, true);
        }

        // Vital organs are ripped out: the body becomes unrevivable.
        RemoveOrgans<LungComponent>(victim);
        RemoveOrgans<HeartComponent>(victim);
        RemoveOrgans<LiverComponent>(victim);

        _popup.PopupEntity(
            Loc.GetString("megafauna-devour", ("boss", ent.Owner), ("victim", victim)),
            ent.Owner,
            PopupType.MediumCaution);
    }

    private void RemoveOrgans<T>(EntityUid victim) where T : IComponent
    {
        if (_body.TryGetBodyOrganEntityComps<T>(victim, out var organs))
        {
            foreach (var organ in organs)
                QueueDel(organ);
        }
    }
}
