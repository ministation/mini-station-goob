// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Goobstation.Maths.FixedPoint;
using Content.Server._Lavaland.Bubblegum;
using Content.Shared._Lavaland.Aggression;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;

namespace Content.Server._Lavaland.Megafauna;

/// <summary>
/// Slowly restores a boss that has lost every aggressor, so a player cannot chip a boss down
/// by running away between hits.
/// </summary>
public sealed class MegafaunaRegenSystem : EntitySystem
{
    /// <summary>Part of the maximum health restored per second while nobody is fighting the boss.</summary>
    private const float HealFractionPerSecond = 0.005f;

    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _threshold = default!;

    private float _accumulator;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < 1f)
            return;

        var seconds = _accumulator;
        _accumulator = 0f;

        var query = EntityQueryEnumerator<AggressiveComponent, DamageableComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var aggressive, out var damageable, out var mobState))
        {
            if (!_mobState.IsAlive(uid, mobState) || aggressive.Aggressors.Count > 0)
                continue;

            // Only lavaland bosses have a brain of their own, regular fauna is left alone.
            if (!HasComp<MegafaunaAiComponent>(uid) && !HasComp<BubblegumComponent>(uid))
                continue;

            if (damageable.TotalDamage <= 0)
                continue;

            if (!_threshold.TryGetThresholdForState(uid, MobState.Dead, out var maxHealth) || maxHealth <= 0)
                continue;

            var heal = FixedPoint2.New(maxHealth.Value.Float() * HealFractionPerSecond * seconds);
            _damageable.HealEvenly((uid, damageable), -heal);
        }
    }
}
