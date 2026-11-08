// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System;
using Content.Server._Lavaland.Megafauna.Systems;
using Content.Shared._Lavaland.Aggression;
using Content.Shared._Lavaland.BossHud;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.MobPhases;
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
    [Dependency] private readonly MobPhasesSystem _phases = default!;
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
        SubscribeLocalEvent<BossHudComponent, MegafaunaArmorChangedEvent>(OnArmorChanged);
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

    private void OnArmorChanged(Entity<BossHudComponent> ent, ref MegafaunaArmorChangedEvent args)
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

        // Armor is optional, only bosses with a MegafaunaArmor component show it.
        var armor = 0f;
        var maxArmor = 0f;
        var armorBroken = false;
        if (TryComp<MegafaunaArmorComponent>(ent, out var armorComp))
        {
            armor = armorComp.Armor;
            maxArmor = armorComp.MaxArmor;
            armorBroken = armorComp.Broken;
        }

        var active = _mobState.IsAlive(ent)
                     && TryComp<AggressiveComponent>(ent, out var aggressive)
                     && aggressive.Aggressors.Count > 0;

        var stage = 1;
        var maxStage = 1;
        if (TryComp<MegafaunaStageComponent>(ent, out var stageComp))
            UpdateStage(ent, stageComp, damageable, hasThreshold ? maxHealth : 0f, out stage, out maxStage);

        if (ent.Comp.Active == active
            && MathF.Abs(ent.Comp.Health - health) < 0.01f
            && MathF.Abs(ent.Comp.MaxHealth - maxHealth) < 0.01f
            && MathF.Abs(ent.Comp.Armor - armor) < 0.01f
            && MathF.Abs(ent.Comp.MaxArmor - maxArmor) < 0.01f
            && ent.Comp.ArmorBroken == armorBroken)
        {
            if (stageComp != null)
                TryUpdateStageComponent(ent, stageComp, stage, maxStage);
            return;
        }

        ent.Comp.Active = active;
        ent.Comp.Health = health;
        ent.Comp.MaxHealth = maxHealth;
        ent.Comp.Armor = armor;
        ent.Comp.MaxArmor = maxArmor;
        ent.Comp.ArmorBroken = armorBroken;
        Dirty(ent);

        if (stageComp != null)
            TryUpdateStageComponent(ent, stageComp, stage, maxStage);
    }

    private void UpdateStage(EntityUid uid, MegafaunaStageComponent stageComp, DamageableComponent damageable,
        float deathThreshold, out int stage, out int maxStage)
    {
        stage = 1;
        maxStage = Math.Max(1, stageComp.MaxStage);

        if (TryComp<MobPhasesComponent>(uid, out var phases))
        {
            // Phase updates share the damage event, resolve the phase before reading it.
            _phases.UpdatePhases((uid, phases, damageable));

            // MobPhases gates the AI attack pattern, it does not dictate how many stages
            // the HUD shows: that is declared by MegafaunaStage.
            stage = Math.Clamp(phases.CurrentPhase, 1, maxStage);
            return;
        }

        if (stageComp.NextStageHealthFraction is not { } threshold || deathThreshold <= 0f)
            return;

        var fraction = 1f - Math.Clamp(damageable.TotalDamage.Float() / deathThreshold, 0f, 1f);
        stage = fraction <= threshold ? maxStage : 1;
    }

    private void TryUpdateStageComponent(EntityUid uid, MegafaunaStageComponent stageComp, int stage, int maxStage)
    {
        if (stageComp.Stage == stage && stageComp.MaxStage == maxStage)
            return;

        stageComp.Stage = stage;
        stageComp.MaxStage = maxStage;
        Dirty(uid, stageComp);
    }
}
