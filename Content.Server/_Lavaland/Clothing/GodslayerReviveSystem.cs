// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Lavaland.Clothing;
using Content.Shared._Shitmed.Damage;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Clothing;

/// <summary>
/// Godslayer armour and helm heal the wearer back from crit or death.
/// Each item keeps its own cooldown and only one of them fires per state change.
/// </summary>
public sealed class GodslayerReviveSystem : EntitySystem
{
    private static readonly string[] ReviveSlots = ["outerClothing", "head"];

    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _threshold = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<GodslayerReviveComponent, ExaminedEvent>(OnExamined);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState is not (MobState.Critical or MobState.Dead))
            return;

        if (!TryComp<DamageableComponent>(args.Target, out var damageable)
            || damageable.TotalDamage <= FixedPoint2.Zero)
            return;

        foreach (var slot in ReviveSlots)
        {
            if (!_inventory.TryGetSlotEntity(args.Target, slot, out var item)
                || !TryComp<GodslayerReviveComponent>(item, out var revive)
                || _timing.CurTime < revive.CooldownEnd)
                continue;

            TryRevive(args.Target, (item.Value, revive), damageable);
            return;
        }
    }

    private void TryRevive(EntityUid body, Entity<GodslayerReviveComponent> item, DamageableComponent damageable)
    {
        // Set the cooldown first: healing re-raises the state change event.
        item.Comp.CooldownEnd = _timing.CurTime + item.Comp.Cooldown;

        var wasDead = _mobState.IsDead(body);
        var total = damageable.TotalDamage;
        var heal = item.Comp.HealAmount < total
            ? damageable.Damage * item.Comp.HealAmount / total
            : damageable.Damage;

        // Healing alone cannot leave the dead state, so allow revives for the duration of the heal.
        _threshold.SetAllowRevives(body, true);
        _damageable.ChangeDamage(body,
            -heal,
            true,
            false,
            targetPart: TargetBodyPart.All,
            splitDamage: SplitDamageBehavior.SplitEnsureAll);
        _threshold.SetAllowRevives(body, false);

        // Some deaths, like a destroyed brain, cannot be healed away; do not spend the charge on them.
        if (wasDead && _mobState.IsDead(body))
        {
            item.Comp.CooldownEnd = TimeSpan.Zero;
            return;
        }

        _popup.PopupEntity(Loc.GetString("godslayer-revive-triggered", ("user", body)), body, PopupType.Large);
    }

    private void OnExamined(Entity<GodslayerReviveComponent> ent, ref ExaminedEvent args)
    {
        var left = ent.Comp.CooldownEnd - _timing.CurTime;
        if (left > TimeSpan.Zero)
        {
            args.PushMarkup(Loc.GetString("godslayer-revive-examine-cooldown",
                ("seconds", (int) Math.Ceiling(left.TotalSeconds))));
            return;
        }

        args.PushMarkup(Loc.GetString("godslayer-revive-examine-ready"));
    }
}
