// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Events;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Megafauna.Systems;

/// <summary>
/// Regenerating armor pool: absorbs incoming damage before health, one to one, and
/// breaks for good once depleted.
/// </summary>
public sealed class MegafaunaArmorSystem : EntitySystem
{
    private static readonly TimeSpan HudUpdateInterval = TimeSpan.FromSeconds(0.25);

    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MegafaunaArmorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MegafaunaArmorComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<MegafaunaArmorComponent, MegafaunaShutdownEvent>(OnShutdown);
    }

    private void OnMapInit(Entity<MegafaunaArmorComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Armor = ent.Comp.MaxArmor;
        ent.Comp.Broken = false;
        RaiseArmorChanged(ent);
    }

    private void OnDamageModify(Entity<MegafaunaArmorComponent> ent, ref DamageModifyEvent args)
    {
        var damage = args.Damage.GetTotal().Float();
        if (damage <= 0f)
            return;

        // Every hit postpones regeneration, even when the pool is already broken.
        ent.Comp.RegenAt = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.RegenDelay);

        if (ent.Comp.Broken)
            return;

        if (damage <= ent.Comp.Armor)
        {
            // The pool eats the whole hit, nothing reaches health.
            ent.Comp.Armor -= damage;
            args.Damage = new DamageSpecifier();
            RaiseArmorChanged(ent);
            return;
        }

        // The pool breaks and the leftover damage continues to health.
        var remaining = damage - ent.Comp.Armor;
        args.Damage *= remaining / damage;
        ent.Comp.Armor = 0f;
        ent.Comp.Broken = true;
        RaiseArmorChanged(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<MegafaunaArmorComponent>();
        while (query.MoveNext(out var uid, out var armor))
        {
            if (armor.Broken || armor.Armor >= armor.MaxArmor || now < armor.RegenAt)
                continue;

            armor.Armor = MathF.Min(armor.Armor + armor.RegenRate * frameTime, armor.MaxArmor);

            // Throttle the HUD sync, a full regeneration would otherwise dirty the
            // networked snapshot every tick.
            if (now < armor.NextHudUpdate && armor.Armor < armor.MaxArmor)
                continue;

            armor.NextHudUpdate = now + HudUpdateInterval;
            RaiseLocalEvent(uid, new MegafaunaArmorChangedEvent());
        }
    }

    private void OnShutdown(Entity<MegafaunaArmorComponent> ent, ref MegafaunaShutdownEvent args)
    {
        // The boss resets between fights, the armor pool is part of that reset.
        ent.Comp.Armor = ent.Comp.MaxArmor;
        ent.Comp.Broken = false;
        ent.Comp.RegenAt = TimeSpan.Zero;
        RaiseArmorChanged(ent.Owner);
    }

    private void RaiseArmorChanged(EntityUid uid)
        => RaiseLocalEvent(uid, new MegafaunaArmorChangedEvent());
}

/// <summary>
/// Raised when a megafauna armor pool changes, so the boss HUD can refresh its snapshot.
/// </summary>
public sealed class MegafaunaArmorChangedEvent : EntityEventArgs;
