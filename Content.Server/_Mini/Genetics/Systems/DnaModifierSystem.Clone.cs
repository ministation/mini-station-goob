using Content.Shared.DetailExaminable;
using Content.Shared.Forensics.Components;
using Content.Shared.Genetics;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;

namespace Content.Server.Genetics.System;

/// <summary>
/// Mini adaptation of Wega deep-clone — copies DNA modifier / forensics / name without Wega VisualBody.
/// </summary>
public sealed partial class DnaModifierSystem
{
    public bool TryCloneHumanoid(Entity<DnaModifierComponent> entity, Entity<DnaModifierComponent> target)
    {
        if (target.Comp.UniqueIdentifiers == null)
            return false;

        CloneHumanoid(entity, target);
        return true;
    }

    private void CloneHumanoid(Entity<DnaModifierComponent> entity, Entity<DnaModifierComponent> target)
    {
        if (target.Comp.UniqueIdentifiers == null)
            return;

        EnsureComp<DnaClonedComponent>(entity);

        entity.Comp.UniqueIdentifiers = CloneUniqueIdentifiers(target.Comp.UniqueIdentifiers);

        if (TryComp<DetailExaminableComponent>(entity, out var detail) &&
            TryComp<DetailExaminableComponent>(target, out var targetDetail))
            detail.Content = targetDetail.Content;

        _metaData.SetEntityName(entity, Name(target));

        if (TryComp<DnaComponent>(entity, out var dna) && TryComp<DnaComponent>(target, out var targetDna))
            dna.DNA = targetDna.DNA;

        if (TryComp<InventoryComponent>(entity, out _) &&
            TryComp<InventoryComponent>(target, out _))
        {
            // Inventory deep-clone not available on Mini ServerInventorySystem — skip.
        }

        if (TryComp<HumanoidAppearanceComponent>(entity, out var appearance) &&
            TryComp<HumanoidAppearanceComponent>(target, out var targetAppearance))
        {
            appearance.SkinColor = targetAppearance.SkinColor;
            appearance.EyeColor = targetAppearance.EyeColor;
            appearance.Gender = targetAppearance.Gender;
            appearance.Sex = targetAppearance.Sex;
            appearance.MarkingSet = new MarkingSet(targetAppearance.MarkingSet);
            Dirty(entity, appearance);
        }

        entity.Comp.UniqueIdentifiers!.Gender = target.Comp.UniqueIdentifiers!.Gender;
        Dirty(entity, entity.Comp);
        TryChangeUniqueIdentifiers(entity);
    }
}
