// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Lavaland.Ruins;

/// <summary>
/// Builds the seed vault landmark: a small ashwood hut with hydroponics
/// gear and a cache of seed packets. The keeper lives with the ash walker
/// tribe now, the vault itself is left for passing miners.
/// TG flavor: the benevolent creators hoped to aid fledgling
/// civilizations; the keepers mostly grow drugs.
/// </summary>
public sealed class SeedVaultSystem : EntitySystem
{
    private const string Wall = "LavalandAshwoodWall";
    private const string Floor = "LavalandAshwoodFloor";
    private const string LavalandSeedCache = "LavalandSeedCache";

    private static readonly EntProtoId[] Seeds =
    [
        "WheatSeeds", "BananaSeeds", "CarrotSeeds", "TomatoSeeds",
        "PotatoSeeds", "AppleSeeds", "CornSeeds", "ChiliSeeds",
        "TowercapSeeds", "NettleSeeds", "PoppySeeds", "CannabisSeeds",
        "AmbrosiaVulgarisSeeds", "BloodTomatoSeeds",
    ];

    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SeedVaultSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
    }

    private void OnSpawnerMapInit(Entity<SeedVaultSpawnerComponent> ent, ref MapInitEvent args)
    {
        // No self-delete: the prototype sweep requires every entity to stay deletable.
        var comp = ent.Comp;
        var baseCoords = Transform(ent).Coordinates;

        var half = comp.RoomRadius;
        for (var x = -half; x <= half; x++)
        {
            for (var y = -half; y <= half; y++)
            {
                var isWall = x == -half || x == half || y == -half || y == half;
                var isDoor = y == -half && x == 0;
                Spawn(isWall && !isDoor ? Wall : Floor, baseCoords.Offset(new System.Numerics.Vector2(x, y)));
            }
        }

        // The lavaland seed cache, the reason miners dig this ruin out.
        for (var i = 0; i < comp.LavalandSeedCacheCount; i++)
            Spawn(LavalandSeedCache, baseCoords);

        // A few ordinary seed packets scattered around, so the vault still feels farmed.
        for (var i = 0; i < comp.SeedCount; i++)
        {
            var proto = _random.Pick(Seeds);
            Spawn(proto, baseCoords.Offset(_random.NextVector2Box(-half + 1f, -half + 1f, half - 1f, half - 1f)));
        }
    }
}

[RegisterComponent]
public sealed partial class SeedVaultSpawnerComponent : Component
{
    /// <summary>
    /// Half-size of the vault room, in tiles.
    /// </summary>
    [DataField]
    public int RoomRadius = 4;

    /// <summary>
    /// How many lavaland seed caches the vault holds.
    /// </summary>
    [DataField]
    public int LavalandSeedCacheCount = 1;

    /// <summary>
    /// How many random normal seed packets the vault holds.
    /// </summary>
    [DataField]
    public int SeedCount = 4;
}
