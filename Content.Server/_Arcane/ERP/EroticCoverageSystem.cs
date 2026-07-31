using Content.Shared._Arcane.ERP;
using Content.Shared._Arcane.ERP.Organs;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Robust.Shared.Containers;

namespace Content.Server._Arcane.ERP;

public sealed class EroticCoverageSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;

    // Oasis: Mini/Orion underwear uses named slots, not SlotFlags.UNDERWEAR/UNDERSHIRT
    private const SlotFlags GroinCovering = SlotFlags.INNERCLOTHING | SlotFlags.OUTERCLOTHING | SlotFlags.LEGS;
    private const SlotFlags ChestCovering = SlotFlags.INNERCLOTHING | SlotFlags.OUTERCLOTHING;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HumanoidAppearanceComponent, EroticOrgansSpawnedEvent>(OnOrgansSpawned);
        SubscribeLocalEvent<HumanoidAppearanceComponent, ClothingDidEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<HumanoidAppearanceComponent, ClothingDidUnequippedEvent>(OnUnequipped);
    }

    private void OnOrgansSpawned(Entity<HumanoidAppearanceComponent> ent, ref EroticOrgansSpawnedEvent args)
    {
        RefreshOrganVisibility(ent);
    }

    private void OnEquipped(Entity<HumanoidAppearanceComponent> ent, ref ClothingDidEquippedEvent args)
    {
        RefreshOrganVisibility(ent);
    }

    private void OnUnequipped(Entity<HumanoidAppearanceComponent> ent, ref ClothingDidUnequippedEvent args)
    {
        RefreshOrganVisibility(ent);
    }

    private void RefreshOrganVisibility(EntityUid uid)
    {
        var coverage = SlotFlags.NONE;
        var enumerator = _inventory.GetSlotEnumerator(uid, GroinCovering | ChestCovering);
        while (enumerator.NextItem(out var item))
        {
            if (TryComp<ClothingComponent>(item, out var clothing))
                coverage |= clothing.Slots;
        }

        var groinCovered = (coverage & GroinCovering) != SlotFlags.NONE;
        var chestCovered = (coverage & ChestCovering) != SlotFlags.NONE;

        foreach (var organ in _body.GetBodyOrganEntityComps<EroticOrganComponent>((uid, null)))
        {
            if (!_containers.TryGetContainingContainer(organ.Owner, out var container))
                continue;

            if (!TryComp<BodyPartComponent>(container.Owner, out var part))
                continue;

            var visible = part.PartType switch
            {
                BodyPartType.Groin => !groinCovered,
                BodyPartType.Chest => !chestCovered,
                _ => true,
            };

            if (organ.Comp1.Visible == visible)
                continue;

            organ.Comp1.Visible = visible;
            Dirty(organ.Owner, organ.Comp1);
        }
    }
}
