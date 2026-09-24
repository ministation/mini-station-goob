// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Server.Implants.Components;
using Content.Shared.Implants.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Goobstation.Server.Implants.Systems;

public sealed class NutrimentPumpImplantSystem : EntitySystem
{
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly ThirstSystem _thirst = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    // Pumps act on their own ExecutionInterval (>= 1 s); scanning implant holders twice a second
    // instead of every tick is indistinguishable in gameplay.
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(0.5f);
    private TimeSpan _nextScan;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_gameTiming.CurTime < _nextScan)
            return;

        _nextScan = _gameTiming.CurTime + ScanInterval;

        var query = EntityQueryEnumerator<ImplantedComponent>();
        while (query.MoveNext(out var uid, out var implantedComponent))
        {
            foreach (var containedEntity in implantedComponent.ImplantContainer.ContainedEntities)
            {
                if (!TryComp<NutrimentPumpImplantComponent>(containedEntity, out var pumpImplant))
                    continue;

                if (pumpImplant.NextExecutionTime > _gameTiming.CurTime)
                    continue;

                if (TryComp<HungerComponent>(uid, out var hungerComponent))
                    _hunger.ModifyHunger(uid, pumpImplant.FoodRate, hungerComponent);

                if (TryComp<ThirstComponent>(uid, out var thirstComponent))
                    _thirst.ModifyThirst(uid, thirstComponent, pumpImplant.DrinkRate); // why the fuck is the order of arguments different for ModifyThirst????

                pumpImplant.NextExecutionTime = _gameTiming.CurTime + pumpImplant.ExecutionInterval;
            }
        }
    }
}
