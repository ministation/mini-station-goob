using Content.Shared.Forensics.Components;
using Content.Shared.Humanoid;
using Content.Shared._Trauma.Genetics.Mutations;

namespace Content.Shared._Trauma.Genetics;

public sealed partial class UniqueEnzymesSystem : EntitySystem
{
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private MutationSystem _mutation = default!;
    [Dependency] private SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private EntityQuery<FingerprintComponent> _printsQuery = default!;
    [Dependency] private EntityQuery<HumanoidAppearanceComponent> _humanoidQuery = default!;

    public void ChangeEnzymes(EntityUid mob, UniqueEnzymes enzymes)
    {
        if (!_mutation.CanMutate(mob))
            return;

        _meta.SetEntityName(mob, enzymes.Name);
        if (enzymes.Prints is { } print && _printsQuery.TryComp(mob, out var prints))
        {
            prints.Fingerprint = print;
            Dirty(mob, prints);
        }

        if (!_humanoidQuery.TryComp(mob, out var humanoid))
            return;

        if (enzymes.EyeColor is { } eyeColor)
        {
            humanoid.EyeColor = eyeColor;
            Dirty(mob, humanoid);
        }

        if (enzymes.SkinColor is { } skinColor)
            _humanoid.SetSkinColor(mob, skinColor, humanoid: humanoid);

        if (enzymes.Sex is { } sex)
            _humanoid.SetSex(mob, sex, humanoid: humanoid);
        if (enzymes.Gender is { } gender)
            _humanoid.SetGender(mob, gender, humanoid: humanoid);
    }

    public UniqueEnzymes GetEnzymes(EntityUid mob)
    {
        var humanoid = _humanoidQuery.CompOrNull(mob);
        return new UniqueEnzymes(
            Name(mob),
            _printsQuery.CompOrNull(mob)?.Fingerprint,
            humanoid?.Sex,
            humanoid?.Gender,
            humanoid?.EyeColor,
            humanoid?.SkinColor
        );
    }
}
