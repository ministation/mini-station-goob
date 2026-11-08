// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared._Lavaland.Mobs;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Components;
using Content.Shared.StepTrigger.Prototypes;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// TG parity for basilisk_overheat.dm. Heat sources (hot damage, welders, standing in lava) put the basilisk
/// into a three minute overheat: full heal, alert sprite, faster movement and a hot projectile instead of the
/// chilling gaze bolt. Death, cold damage and running out of time cool it back down.
/// </summary>
public sealed class BasiliskOverheatSystem : EntitySystem
{
    private const string HeatDamage = "Heat";
    private const string ColdDamage = "Cold";
    private const string LavaTriggerType = "Lava";
    private const string WeldingQuality = "Welding";

    private static readonly TimeSpan LavaCheckInterval = TimeSpan.FromSeconds(1);

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BasiliskOverheatComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<BasiliskOverheatComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<BasiliskOverheatComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<BasiliskOverheatComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.EndsAt > curTime)
                continue;

            if (comp.EndsAt != TimeSpan.Zero)
            {
                CoolDown((uid, comp));
                continue;
            }

            if (curTime < comp.NextLavaCheck)
                continue;

            comp.NextLavaCheck = curTime + LavaCheckInterval;
            if (_mobState.IsAlive(uid) && IsOnLava(xform))
                HeatUp((uid, comp));
        }
    }

    private bool IsOnLava(TransformComponent xform)
    {
        foreach (var other in _lookup.GetEntitiesInRange(xform.Coordinates, 0.1f))
        {
            if (!TryComp<StepTriggerComponent>(other, out var trigger)
                || trigger.TriggerGroups?.Types is not { } types)
                continue;

            foreach (ProtoId<StepTriggerTypePrototype> type in types)
            {
                if (type.Id == LavaTriggerType)
                    return true;
            }
        }

        return false;
    }

    private void OnDamageChanged(Entity<BasiliskOverheatComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta is not { } delta)
            return;

        if (delta.DamageDict.TryGetValue(HeatDamage, out var heat) && heat > 0)
        {
            HeatUp(ent);
            return;
        }

        if (ent.Comp.EndsAt > _timing.CurTime
            && delta.DamageDict.TryGetValue(ColdDamage, out var cold)
            && cold > 0)
            CoolDown(ent);
    }

    private void OnInteractUsing(Entity<BasiliskOverheatComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tool.HasQuality(args.Used, WeldingQuality))
            return;

        // Only a lit welder heats the basilisk up, and the interaction ends here.
        if (!TryComp<WelderComponent>(args.Used, out var welder) || !welder.Enabled)
            return;

        args.Handled = true;
        HeatUp(ent);
    }

    private void OnMobStateChanged(Entity<BasiliskOverheatComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead && ent.Comp.EndsAt != TimeSpan.Zero)
            CoolDown(ent);
    }

    private void HeatUp(Entity<BasiliskOverheatComponent> ent)
    {
        if (!_mobState.IsAlive(ent) || ent.Comp.EndsAt > _timing.CurTime)
            return;

        ent.Comp.EndsAt = _timing.CurTime + ent.Comp.Duration;

        if (TryComp<DamageableComponent>(ent, out var damageable))
            _damageable.SetDamage((ent.Owner, damageable), new DamageSpecifier());

        _appearance.SetData(ent, BasiliskOverheatVisuals.Overheated, true);

        if (TryComp<BatteryAmmoProviderComponent>(ent, out var ammo))
        {
            ent.Comp.OriginalProjectile = ammo.Prototype;
            ammo.Prototype = ent.Comp.HotProjectile;
            Dirty(ent, ammo);
        }

        if (TryComp<MovementSpeedModifierComponent>(ent, out var move))
        {
            ent.Comp.OriginalWalkSpeed = move.BaseWalkSpeed;
            ent.Comp.OriginalSprintSpeed = move.BaseSprintSpeed;
            ent.Comp.OriginalAcceleration = move.Acceleration;
            _speed.ChangeBaseSpeed(ent,
                move.BaseWalkSpeed * ent.Comp.SpeedMultiplier,
                move.BaseSprintSpeed * ent.Comp.SpeedMultiplier,
                move.Acceleration);
        }

        _popup.PopupEntity(Loc.GetString("basilisk-overheat-start"), ent, PopupType.MediumCaution);
    }

    private void CoolDown(Entity<BasiliskOverheatComponent> ent)
    {
        ent.Comp.EndsAt = TimeSpan.Zero;

        // A dead basilisk is shown by DamageStateVisuals, restoring the alert appearance would fight it.
        if (_mobState.IsAlive(ent))
            _appearance.SetData(ent, BasiliskOverheatVisuals.Overheated, false);

        if (ent.Comp.OriginalProjectile is { } original
            && TryComp<BatteryAmmoProviderComponent>(ent, out var ammo))
        {
            ammo.Prototype = original;
            ent.Comp.OriginalProjectile = null;
            Dirty(ent, ammo);
        }

        if (TryComp<MovementSpeedModifierComponent>(ent, out _))
        {
            _speed.ChangeBaseSpeed(ent,
                ent.Comp.OriginalWalkSpeed,
                ent.Comp.OriginalSprintSpeed,
                ent.Comp.OriginalAcceleration);
        }

        if (_mobState.IsAlive(ent))
            _popup.PopupEntity(Loc.GetString("basilisk-overheat-end"), ent);
    }
}
