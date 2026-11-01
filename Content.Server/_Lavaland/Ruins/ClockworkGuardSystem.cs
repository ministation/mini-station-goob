// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;

namespace Content.Server._Lavaland.Ruins;

/// <summary>
/// Spawns a pair of clockwork defenders around itself. Placed into the
/// Dead God (Ratvar) ruin map, port of TG's clockwork defenders.
/// </summary>
public sealed class ClockworkGuardSystem : EntitySystem
{
    private const string Defender = "MobClockworkDefender";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClockworkGuardSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
    }

    private void OnSpawnerMapInit(Entity<ClockworkGuardSpawnerComponent> ent, ref MapInitEvent args)
    {
        // No self-delete: the prototype sweep requires every entity to stay deletable.
        var baseCoords = Transform(ent).Coordinates;
        Spawn(Defender, baseCoords.Offset(new Vector2(-1.5f, 0)));
        Spawn(Defender, baseCoords.Offset(new Vector2(1.5f, 0)));
    }
}

[RegisterComponent]
public sealed partial class ClockworkGuardSpawnerComponent : Component
{
}
