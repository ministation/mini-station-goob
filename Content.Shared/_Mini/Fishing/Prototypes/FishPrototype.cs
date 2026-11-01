// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Mini.Fishing.Prototypes;

/// <summary>
/// All the gameplay data of a single fish species. The visual entity is a separate
/// entity prototype that points at this one with <see cref="Components.FishComponent"/>.
/// </summary>
[Prototype("fish")]
public sealed partial class FishPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Icon used for the fish item and for the catalog.
    /// </summary>
    [DataField(required: true)]
    public SpriteSpecifier Sprite = default!;

    /// <summary>
    /// Icon used when the fish is dead, if the species has a custom dead sprite.
    /// </summary>
    [DataField]
    public SpriteSpecifier? DeadSprite;

    /// <summary>
    /// Icon used when the fish swims inside an aquarium. Falls back to <see cref="Sprite"/>.
    /// </summary>
    [DataField]
    public SpriteSpecifier? AquariumSprite;

    /// <summary>
    /// Width and height of the in-aquarium sprite, in 32x32 tiles.
    /// </summary>
    [DataField]
    public Vector2i AquariumSize = new(1, 1);

    [DataField]
    public Color? AquariumColor;

    [DataField]
    public float AverageSize = 50f;

    [DataField]
    public float SizeDeviation = 0.2f;

    [DataField]
    public float AverageWeight = 1000f;

    [DataField]
    public float WeightDeviation = 0.2f;

    [DataField]
    public AquariumFluid Fluid = AquariumFluid.Freshwater;

    [DataField]
    public float TemperatureMin = 273.15f;

    [DataField]
    public float TemperatureMax = 373.15f;

    /// <summary>
    /// Reagent the fish eats. Null means the generic fish feed.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype>? Food;

    [DataField]
    public float FeedingFrequency = 300f;

    /// <summary>
    /// Random fish case weight. 1000 is the basic rarity, 0 means it is never found in random cases.
    /// </summary>
    [DataField]
    public int Rarity = 1000;

    /// <summary>
    /// How much beauty this fish provides to an aquarium or a trophy mount.
    /// </summary>
    [DataField]
    public int Beauty = 250; // tg FISH_BEAUTY_GENERIC

    [DataField]
    public List<ProtoId<FishTraitPrototype>> Traits = new();

    [DataField]
    public ProtoId<FishMovementPrototype>? Movement;

    [DataField]
    public float DifficultyModifier;

    [DataField]
    public bool ShowInCatalog = true;

    /// <summary>
    /// Inhand state prefix from the fish inhand RSI. Null means the size based prefix is used.
    /// </summary>
    [DataField]
    public string? InhandPrefix;

    [DataField]
    public List<EntProtoId> FavoriteBaits = new();

    [DataField]
    public List<EntProtoId> DislikedBaits = new();

    /// <summary>
    /// Bait reagents the fish likes, with the minimum amount required.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, float> FavoriteReagents = new();

    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, float> DislikedReagents = new();

    /// <summary>
    /// Traits on a bait fish that this fish likes.
    /// </summary>
    [DataField]
    public List<ProtoId<FishTraitPrototype>> FavoriteBaitTraits = new();

    [DataField]
    public List<ProtoId<FishTraitPrototype>> DislikedBaitTraits = new();

    // Aquarium breeding.

    [DataField]
    public int StablePopulation = 1;

    [DataField]
    public float BreedingTime = 120f;

    [DataField]
    public List<ProtoId<FishPrototype>> CompatibleTypes = new();

    [DataField]
    public List<ProtoId<FishPrototype>> SpawnTypes = new();

    [DataField]
    public List<ProtoId<FishEvolutionPrototype>> EvolutionTypes = new();

    // Eating.

    /// <summary>
    /// Whether the fish can be eaten. Holographic fish cannot, like in tg.
    /// </summary>
    [DataField]
    public bool Edible = true;

    /// <summary>
    /// Flavor ids of the raw fish, tg get_fish_taste.
    /// </summary>
    [DataField]
    public List<string> Taste = new() { "raw-fish", "scales" };

    /// <summary>
    /// Edible reagents per bite on top of the default protein and blood. Fluid saltwater fish also get salt.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, float> EdibleReagents = new();

    /// <summary>
    /// When true, <see cref="EdibleReagents"/> replaces the default protein and blood instead of adding to them.
    /// </summary>
    [DataField]
    public bool ReplaceEdibleReagents;

    // Butchering.

    [DataField]
    public EntProtoId? Fillet;

    /// <summary>
    /// Base number of fillets. Scaled by the size of the caught fish.
    /// </summary>
    [DataField]
    public int Fillets = 1; // tg num_fillets default
}