using Robust.Shared.Localization;

namespace Content.Goobstation.Shared.Xenobiology;

public static class XenobiologyLoc
{
    public static string GetBreedName(string breedNameId) =>
        Loc.TryGetString(breedNameId, out var name) ? name : breedNameId;

    public static string GetBreedName(BreedPrototype breed) =>
        GetBreedName(breed.BreedName);
}
