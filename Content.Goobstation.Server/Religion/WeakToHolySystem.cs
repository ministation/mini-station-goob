// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Common.Religion;
using Content.Goobstation.Shared.Bible;
using Content.Goobstation.Shared.Devil;
using Content.Goobstation.Shared.Religion.Nullrod;
using Content.Server.Heretic.EntitySystems;
using Content.Shared._DV.CosmicCult.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Damage;
using Content.Shared.Heretic;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.RPSX.DarkForces.Ratvar.Righteous.Roles;
using Content.Shared.Timing; // Shitmed Change
using Content.Shared._Shitmed.Damage; // Shitmed Change
using Content.Shared.WhiteDream.BloodCult.BloodCultist;
using Content.Shared.WhiteDream.BloodCult.Constructs;

namespace Content.Goobstation.Shared.Religion;

public sealed class WeakToHolySystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly GoobBibleSystem _goobBible = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly WoundSystem _wound = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly HereticSystem _heretic = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WeakToHolyComponent, DamageUnholyEvent>(OnUnholyItemDamage);
        SubscribeLocalEvent<WeakToHolyComponent, InteractUsingEvent>(AfterBibleUse);

        SubscribeLocalEvent<HereticRitualRuneComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<HereticRitualRuneComponent, EndCollideEvent>(OnCollideEnd);

        SubscribeLocalEvent<DamageableComponent, DamageModifyEvent>(OnHolyDamageModify);

    }

    private void AfterBibleUse(Entity<WeakToHolyComponent> ent, ref InteractUsingEvent args)
    {
        if (!TryComp<BibleComponent>(args.Used, out var bibleComp))
            return;

        if (!TryComp(args.Used, out UseDelayComponent? useDelay)
            || _useDelay.IsDelayed((args.Used, useDelay))
            || !HasComp<BibleUserComponent>(args.User))
            return;

        _goobBible.TryDoSmite(args.Used, args.User, args.Target, useDelay);
    }

    #region Holy Damage Dealing

    private void OnHolyDamageModify(Entity<DamageableComponent> ent, ref DamageModifyEvent args)
    {
        // Resolve the body that owns WeakToHoly / antag comps (damage often hits body parts).
        var unholyTarget = args.Target;
        if (TryComp(ent, out BodyPartComponent? part) && part.Body is { } bodyUid)
            unholyTarget = bodyUid;
        else if (HasComp<BodyComponent>(ent))
            unholyTarget = ent.Owner;

        var unholyEvent = new DamageUnholyEvent(unholyTarget, args.Origin);
        RaiseLocalEvent(unholyTarget, ref unholyEvent);

        // Empty heretic vessels keep WeakToHoly after the mind leaves; mind checks alone miss them.
        if (!unholyEvent.ShouldTakeHoly
            && TryComp<WeakToHolyComponent>(unholyTarget, out var weak)
            && weak.AlwaysTakeHoly)
            unholyEvent.ShouldTakeHoly = true;

        if (!unholyEvent.ShouldTakeHoly && IsUnholyAntag(unholyTarget))
            unholyEvent.ShouldTakeHoly = true;

        // Only filter Holy on bodies / body parts — not random damageables.
        if (!HasComp<BodyComponent>(ent) && !HasComp<BodyPartComponent>(ent))
            return;

        var holyCoefficient = unholyEvent.ShouldTakeHoly ? 1f : 0f;
        args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, new DamageModifierSet
        {
            Coefficients = new Dictionary<string, float>
            {
                { "Holy", holyCoefficient },
            },
        });
    }

    private bool IsUnholyAntag(EntityUid uid)
    {
        return HasComp<BloodCultistComponent>(uid)
               || HasComp<ConstructComponent>(uid)
               || HasComp<RatvarRighteousComponent>(uid)
               || HasComp<CosmicCultComponent>(uid)
               || HasComp<DevilComponent>(uid)
               || _heretic.TryGetHereticComponent(uid, out _, out _)
               || _heretic.IsHereticOrGhoul(uid);
    }

    private void OnUnholyItemDamage(Entity<WeakToHolyComponent> uid, ref DamageUnholyEvent args)
    {
        if (uid.Comp.AlwaysTakeHoly)
        {
            args.ShouldTakeHoly = true;
            return;
        }

        // Any heretic (not only ascended) takes holy damage.
        if (_heretic.TryGetHereticComponent(uid, out _, out _))
        {
            args.ShouldTakeHoly = true;
            return;
        }

        // If any item in hand or in inventory has Unholy item, shouldtakeholy is true.
        if (_inventorySystem.GetHandOrInventoryEntities(args.Target, SlotFlags.WITHOUT_POCKET)
            .Any(HasComp<UnholyItemComponent>))
            args.ShouldTakeHoly = true;
    }

    #endregion

    #region Holy Healing

    // Passively heal on runes
    private void OnCollide(Entity<HereticRitualRuneComponent> ent, ref StartCollideEvent args)
    {
        if (!TryComp<WeakToHolyComponent>(args.OtherEntity, out var weak))
            return;

        weak.IsColliding = true;
    }

    private void OnCollideEnd(Entity<HereticRitualRuneComponent> ent, ref EndCollideEvent args)
    {
        if (!TryComp<WeakToHolyComponent>(args.OtherEntity, out var weak))
            return;

        weak.IsColliding = false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Holy damage healing.
        var query = EntityQueryEnumerator<WeakToHolyComponent, BodyComponent>();
        while (query.MoveNext(out var uid, out var weakToHoly, out var body))
        {
            if (weakToHoly.NextPassiveHealTick > _timing.CurTime)
                continue;
            weakToHoly.NextPassiveHealTick = _timing.CurTime + weakToHoly.HealTickDelay;

            if (!TryComp<DamageableComponent>(uid, out var damageable))
                continue;

            if (TerminatingOrDeleted(uid)
                || !_body.TryGetRootPart(uid, out var rootPart, body: body)
                || !damageable.Damage.DamageDict.TryGetValue("Holy", out _))
                continue;

            // Rune healing.
            if (weakToHoly.IsColliding)
                _damageableSystem.TryChangeDamage(uid, weakToHoly.HealAmount, ignoreBlockers: true, targetPart: TargetBodyPart.All, splitDamage: SplitDamageBehavior.SplitEnsureAll);

            // Passive healing.
            _damageableSystem.TryChangeDamage(uid, weakToHoly.PassiveAmount, ignoreBlockers: true, targetPart: TargetBodyPart.All, splitDamage: SplitDamageBehavior.SplitEnsureAll);
        }
    }

    #endregion
}
