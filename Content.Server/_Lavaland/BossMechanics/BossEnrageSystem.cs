// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.NumberSelectors;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// Applies <see cref="BossEnrageComponent"/> when the boss is low on health.
/// </summary>
public sealed class BossEnrageSystem : EntitySystem
{
    [Dependency] private readonly MobThresholdSystem _threshold = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BossEnrageComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<BossEnrageComponent> ent, ref DamageChangedEvent args)
    {
        if (ent.Comp.Enraged
            || !TryComp<DamageableComponent>(ent, out var damageable)
            || !_threshold.TryGetThresholdForState(ent, MobState.Dead, out var threshold)
            || threshold is not { } maxHealth
            || maxHealth <= 0)
            return;

        var fraction = 1f - Math.Clamp(damageable.TotalDamage.Float() / maxHealth.Float(), 0f, 1f);
        if (fraction > ent.Comp.HealthFraction)
            return;

        Enrage(ent);
    }

    private void Enrage(Entity<BossEnrageComponent> ent)
    {
        var comp = ent.Comp;
        comp.Enraged = true;

        if (TryComp<MovementSpeedModifierComponent>(ent, out var speed))
        {
            _speed.ChangeBaseSpeed(ent.Owner,
                speed.BaseWalkSpeed * comp.SpeedMultiplier,
                speed.BaseSprintSpeed * comp.SpeedMultiplier,
                speed.Acceleration);
        }

        if (TryComp<MeleeWeaponComponent>(ent, out var melee))
        {
            melee.AttackRate *= comp.AttackRateMultiplier;
            Dirty(ent.Owner, melee);
        }

        if (comp.ActionDelay is { } actionDelay && TryComp<MegafaunaAiComponent>(ent, out var ai))
            ai.ActionDelaySelector = new MegafaunaConstantNumberSelector(actionDelay);

        _audio.PlayPvs(comp.Sound, ent);
    }
}
