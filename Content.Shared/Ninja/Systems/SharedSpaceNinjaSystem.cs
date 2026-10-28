// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Mini.Ninja.Components;
using Content.Shared.Ninja.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Popups;
using System.Diagnostics.CodeAnalysis;

namespace Content.Shared.Ninja.Systems;

/// <summary>
/// Provides shared ninja API, handles being attacked revealing ninja and stops guns from shooting.
/// </summary>
public abstract class SharedSpaceNinjaSystem : EntitySystem
{
    [Dependency] protected readonly SharedNinjaSuitSystem Suit = default!;
    [Dependency] protected readonly SharedPopupSystem Popup = default!;

    public EntityQuery<SpaceNinjaComponent> NinjaQuery;

    public override void Initialize()
    {
        base.Initialize();

        NinjaQuery = GetEntityQuery<SpaceNinjaComponent>();

        SubscribeLocalEvent<SpaceNinjaComponent, AttackedEvent>(OnNinjaAttacked);
        SubscribeLocalEvent<SpaceNinjaComponent, MeleeAttackEvent>(OnNinjaAttack);
        SubscribeLocalEvent<SpaceNinjaComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<EntityTerminatingEvent>(OnEquipmentTerminating);
    }

    // Suit/gloves/katana may be deleted while the ninja is alive (destroyed, QDEL) and the
    // networked refs would point at a deleted entity every PVS tick.
    private void OnEquipmentTerminating(ref EntityTerminatingEvent args)
    {
        var terminated = args.Entity.Owner;
        var query = EntityQueryEnumerator<SpaceNinjaComponent>();
        while (query.MoveNext(out var uid, out var ninja))
        {
            var dirty = false;

            if (ninja.Suit == terminated)
            {
                ninja.Suit = null;
                dirty = true;
            }

            if (ninja.Gloves == terminated)
            {
                ninja.Gloves = null;
                dirty = true;
            }

            if (ninja.Katana == terminated)
            {
                ninja.Katana = null;
                dirty = true;
            }

            if (dirty)
                Dirty(uid, ninja);
        }
    }

    public bool IsNinja([NotNullWhen(true)] EntityUid? uid)
    {
        return NinjaQuery.HasComp(uid);
    }

    /// <summary>
    /// Set the ninja's worn suit entity
    /// </summary>
    public void AssignSuit(Entity<SpaceNinjaComponent> ent, EntityUid? suit)
    {
        if (ent.Comp.Suit == suit)
            return;

        ent.Comp.Suit = suit;
        Dirty(ent, ent.Comp);
    }

    /// <summary>
    /// Set the ninja's worn gloves entity
    /// </summary>
    public void AssignGloves(Entity<SpaceNinjaComponent> ent, EntityUid? gloves)
    {
        if (ent.Comp.Gloves == gloves)
            return;

        ent.Comp.Gloves = gloves;
        Dirty(ent, ent.Comp);
    }

    /// <summary>
    /// Bind a katana entity to a ninja, letting it be recalled and dash.
    /// Does nothing if the player is not a ninja or already has a katana bound.
    /// </summary>
    public void BindKatana(Entity<SpaceNinjaComponent?> ent, EntityUid katana)
    {
        if (!NinjaQuery.Resolve(ent, ref ent.Comp, false) || ent.Comp.Katana != null)
            return;

        ent.Comp.Katana = katana;
        Dirty(ent, ent.Comp);
    }

    /// <summary>
    /// Gets the user's battery and tries to use some charge from it, returning true if successful.
    /// Serverside only.
    /// </summary>
    public virtual bool TryUseCharge(EntityUid user, float charge)
    {
        return false;
    }

    /// <summary>
    /// Handle revealing ninja if cloaked when attacked.
    /// </summary>
    private void OnNinjaAttacked(Entity<SpaceNinjaComponent> ent, ref AttackedEvent args)
    {
        TryRevealNinja(ent, disable: true);
    }

    /// <summary>
    /// Handle revealing ninja if cloaked when attacking.
    /// Only reveals, there is no cooldown.
    /// </summary>
    private void OnNinjaAttack(Entity<SpaceNinjaComponent> ent, ref MeleeAttackEvent args)
    {
        TryRevealNinja(ent, disable: true); // Goob edit
    }

    private void TryRevealNinja(Entity<SpaceNinjaComponent> ent, bool disable)
    {
        if (ent.Comp.Suit is {} uid && TryComp<NinjaSuitComponent>(ent.Comp.Suit, out var suit))
            Suit.RevealNinja((uid, suit), ent, disable: disable);
    }

    /// <summary>
    /// Require ninja to fight with HONOR, no guns!
    /// </summary>
    private void OnShotAttempted(Entity<SpaceNinjaComponent> ent, ref ShotAttemptedEvent args)
    {
        // Mini: suit-integrated ninja weapons (shuriken emitter, johyo) are allowed.
        if (HasComp<NinjaWeaponComponent>(args.Used))
            return;

        Popup.PopupClient(Loc.GetString("gun-disabled"), ent, ent);
        args.Cancel();
    }
}
