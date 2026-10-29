// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

namespace Content.Goobstation.Shared.Fishing;

/// <summary>
/// Fluid type a fish needs to survive in an aquarium.
/// </summary>
public enum AquariumFluid : byte
{
    AnyWater,
    Anadromous,
    Saltwater,
    Freshwater,
    SulphWater,
    Air,
}

/// <summary>
/// Where the fish lives, used to reject fishing attempts.
/// </summary>
public enum FishHabitat : byte
{
    Freshwater,
    Saltwater,
    AnyWater,
    Air,
}

/// <summary>
/// How the fish moves on the minigame bar.
/// </summary>
public enum FishMovementType : byte
{
    Slow,
    Zippy,
    Accelerando,
    Choppy,
    Plunger,
}

[Flags]
public enum FishTraitFlags : ulong
{
    None = 0,
    Wary = 1 << 0,
    ShinyLover = 1 << 1,
    PickyEater = 1 << 2,
    Nocturnal = 1 << 3,
    Heavy = 1 << 4,
    Carnivore = 1 << 5,
    Vegan = 1 << 6,
    Emulsijack = 1 << 7,
    Necrophage = 1 << 8,
    Parthenogenesis = 1 << 9,
    NoMating = 1 << 10,
    Recessive = 1 << 11,
    Revival = 1 << 12,
    Predator = 1 << 13,
    Yucky = 1 << 14,
    Toxic = 1 << 15,
    ToxinImmunity = 1 << 16,
    Crossbreeder = 1 << 17,
    Territorial = 1 << 18,
    Lubed = 1 << 19,
    Amphibious = 1 << 20,
    Mixotroph = 1 << 21,
    Antigrav = 1 << 22,
    Anxiety = 1 << 23,
    Electrogenesis = 1 << 24,
    Stunted = 1 << 25,
    Stinger = 1 << 26,
    ToxicBarbs = 1 << 27,
    Hallucinogenic = 1 << 28,
    Ink = 1 << 29,
    Camouflage = 1 << 30,
}

/// <summary>
/// Rules that may be applied to a single fishing minigame.
/// </summary>
[Flags]
public enum FishingMinigameRules : uint
{
    None = 0,
    Bidirectional = 1 << 0,
    NoEscape = 1 << 1,
    Kill = 1 << 2,
    NoExperience = 1 << 3,
    Antigrav = 1 << 4,
    Flip = 1 << 5,
    Autoreel = 1 << 6,
    Camo = 1 << 7,
}

/// <summary>
/// Extra properties of a fishing source.
/// </summary>
[Flags]
public enum FishingSourceFlags : uint
{
    None = 0,
    ExplosiveMalus = 1 << 0,
}
