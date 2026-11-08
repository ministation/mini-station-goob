// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Mobs;

namespace Content.Server._Lavaland.Megafauna;

/// <summary>
/// TG parity for <see cref="HierophantDeathBurstComponent"/>: dying releases the burst
/// before the boss body is deleted by SpawnLootOnDeath.
/// </summary>
public sealed class HierophantDeathBurstSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HierophantDeathBurstComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<HierophantDeathBurstComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        Spawn(ent.Comp.Burst, _xform.GetMapCoordinates(ent));
    }
}
