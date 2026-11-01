// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Mini.Fishing;

/// <summary>
/// Turns grown fish into their adult form, for example a tadpole into a frog.
/// </summary>
public sealed class FishGrowthSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishGrowthComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<FishGrowthComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.GrowAt = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.GrowthTime);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FishGrowthComponent, FishComponent>();
        while (query.MoveNext(out var uid, out var growth, out var fish))
        {
            if (!fish.Alive || now < growth.GrowAt)
                continue;

            // Never hatch a mob inside a bag or an aquarium tank. The fish waits there until it is taken out.
            if (_container.IsEntityInContainer(uid))
                continue;

            if (!_proto.HasIndex(growth.Result.Id))
            {
                RemComp<FishGrowthComponent>(uid);
                continue;
            }

            Spawn(growth.Result, Transform(uid).Coordinates);
            QueueDel(uid);
        }
    }
}
