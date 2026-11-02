// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Lavaland.Ruins;

/// <summary>
/// Builds the seed vault landmark: a small metal room with hydroponics
/// gear, a cache of seed packets, and a keeper ghost role spawner.
/// TG flavor: the benevolent creators hoped to aid fledgling
/// civilizations; the keepers mostly grow drugs.
/// </summary>
public sealed class SeedVaultSystem : EntitySystem
{
    private const string Wall = "WallReinforced";
    private const string Floor = "LavalandStoneTileBurnt";
    private const string Spawner = "LavalandSeedVaultSpawner";

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

        // The keeper, guarding the goods.
        Spawn("LavalandSeedVaultKeeperPod", baseCoords.Offset(new System.Numerics.Vector2(0, half - 1)));

        // A cache of seed packets, scattered in the middle.
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
    /// How many random seed packets the vault holds.
    /// </summary>
    [DataField]
    public int SeedCount = 10;
}
