// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Content.Shared._Lavaland.Aggression;
using Content.Shared._Lavaland.BossHud;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;

namespace Content.Server._Lavaland.BossHud;

/// <summary>
/// Keeps the networked boss HUD snapshot up to date. Health comes from the damage
/// taken against the entity death threshold, visibility from the live aggressor
/// list of <see cref="AggressiveComponent"/> and the boss life state.
/// </summary>
public sealed class BossHudSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Every staged megafauna is a boss fight, so the HUD comes with the stage
        // component instead of being repeated in every boss prototype.
        SubscribeLocalEvent<MegafaunaStageComponent, MapInitEvent>(OnBossMapInit);

        SubscribeLocalEvent<BossHudComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BossHudComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<BossHudComponent, AggressorAddedEvent>(OnAggressorAdded);
        SubscribeLocalEvent<BossHudComponent, AggressorRemovedEvent>(OnAggressorRemoved);
        SubscribeLocalEvent<BossHudComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnBossMapInit(Entity<MegafaunaStageComponent> ent, ref MapInitEvent args)
        => EnsureComp<BossHudComponent>(ent);

    private void OnStartup(Entity<BossHudComponent> ent, ref ComponentStartup args)
        => Refresh(ent);

    private void OnDamageChanged(Entity<BossHudComponent> ent, ref DamageChangedEvent args)
        => Refresh(ent);

    private void OnAggressorAdded(Entity<BossHudComponent> ent, ref AggressorAddedEvent args)
        => Refresh(ent);

    private void OnAggressorRemoved(Entity<BossHudComponent> ent, ref AggressorRemovedEvent args)
        => Refresh(ent);

    private void OnMobStateChanged(Entity<BossHudComponent> ent, ref MobStateChangedEvent args)
        => Refresh(ent);

    private void Refresh(Entity<BossHudComponent> ent)
    {
        if (!TryComp<DamageableComponent>(ent, out var damageable))
            return;

        var totalDamage = damageable.TotalDamage.Float();

        // The death threshold is the boss max health; a boss without one is treated as
        // invincible and shows a full bar.
        var maxHealth = 1f;
        var hasThreshold = _mobThreshold.TryGetThresholdForState(ent, MobState.Dead, out var threshold)
                           && threshold != null;
        if (hasThreshold)
            maxHealth = MathF.Max(threshold!.Value.Float(), 1f);

        var health = hasThreshold ? Math.Clamp(maxHealth - totalDamage, 0f, maxHealth) : maxHealth;
        var active = _mobState.IsAlive(ent)
                     && TryComp<AggressiveComponent>(ent, out var aggressive)
                     && aggressive.Aggressors.Count > 0;

        if (ent.Comp.Active == active
            && MathF.Abs(ent.Comp.Health - health) < 0.01f
            && MathF.Abs(ent.Comp.MaxHealth - maxHealth) < 0.01f)
            return;

        ent.Comp.Active = active;
        ent.Comp.Health = health;
        ent.Comp.MaxHealth = maxHealth;
        Dirty(ent);
    }
}
